using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;

namespace OctaneTagWritingTest.TestStrategy
{
    /// <summary>
    /// Strategy for robustness testing: write + verification with retries
    /// </summary>
    public class TestCase6RobustnessStrategy : BaseTestStrategy
    {
        // Maximum number of retries for verification operation
        private const int maxRetries = 3;
        // Stores retry count for each TID
        private readonly Dictionary<string, int> retryCount = new Dictionary<string, int>();

        private readonly Stopwatch swWrite = new Stopwatch();
        private readonly Stopwatch swVerify = new Stopwatch();
        private Tag? currentTargetTag;
        private string? expectedEpc;

        public TestCase6RobustnessStrategy(string hostname, string logFile)
            : base(hostname, logFile)
        {
        }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("Starting robustness test (write-verify with retries)...");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                // Configure reader (connection, settings, EPC list loading and low latency)
                ConfigureReader();

                // Subscribe to read and operation completion events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                // Create log file if it doesn't exist
                if (!File.Exists(logFile))
                {
                    LogToCsv("Timestamp,TID,Previous_EPC,Expected_EPC,Verified_EPC,WriteTime_ms,VerifyTime_ms,Result,Retries,RSSI,AntennaPort");
                }

                // Keep the test running until cancellation is requested
                while (!IsCancellationRequested())
                {
                    Thread.Sleep(100); // Small delay to prevent CPU spinning
                }

                Console.WriteLine("\nStopping test...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in robustness test: " + ex.Message);
            }
            finally
            {
                CleanupReader();
            }
        }

        /// <summary>
        /// Event handler called when reader reports tags
        /// </summary>
        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null || IsCancellationRequested()) return;
            
            foreach (Tag tag in report)
            {
                if (IsCancellationRequested()) return;

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (string.IsNullOrEmpty(tidHex))
                    continue;

                // If result already exists for this TID, skip the event
                if (TagOpController.HasResult(tidHex))
                    continue;

                // Reset target TID for each new tag to enable continuous encoding
                if (!string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"\nNew target TID found: {tidHex}");
                }

                // Filter by target TID
                if (tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Processing tag: EPC={tag.Epc.ToHexString()}, TID={tidHex}");
                    currentTargetTag = tag;
                    retryCount[tidHex] = 0;
                    TriggerWriteAndVerify(tag);
                    break;
                }
            }
        }

        /// <summary>
        /// Method that triggers write operation with password update and new EPC writing
        /// </summary>
        private void TriggerWriteAndVerify(Tag tag)
        {
            if (IsCancellationRequested()) return;

            string oldEpc = tag.Epc.ToHexString();
            // Get new EPC to be written (using TagOpController helper)
            expectedEpc = TagOpController.GetNextEpcForTag();
            Console.WriteLine($"Attempting robust operation for TID {tag.Tid.ToHexString()}: {oldEpc} -> {expectedEpc}");

            // Create operation sequence with BlockWrite enabled
            TagOpSequence seq = new TagOpSequence();
            seq.BlockWriteEnabled = true;
            seq.BlockWriteWordCount = 2;
            seq.TargetTag.MemoryBank = MemoryBank.Epc;
            seq.TargetTag.BitPointer = BitPointers.Epc;
            seq.TargetTag.Data = oldEpc;

            // Operation to update Access password (in Reserved bank)
            TagWriteOp updateAccessPwd = new TagWriteOp();
            updateAccessPwd.AccessPassword = null; // Default current password (00000000)
            updateAccessPwd.MemoryBank = MemoryBank.Reserved;
            updateAccessPwd.WordPointer = WordPointers.AccessPassword;
            updateAccessPwd.Data = TagData.FromHexString(newAccessPassword);
            seq.Ops.Add(updateAccessPwd);

            // Operation to write new EPC to EPC bank
            TagWriteOp writeOp = new TagWriteOp();
            writeOp.AccessPassword = TagData.FromHexString(newAccessPassword);
            writeOp.MemoryBank = MemoryBank.Epc;
            writeOp.WordPointer = WordPointers.Epc;
            writeOp.Data = TagData.FromHexString(expectedEpc);
            seq.Ops.Add(writeOp);

            swWrite.Restart();
            reader.AddOpSequence(seq);

            string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
            TagOpController.RecordExpectedEpc(tidHex, expectedEpc);
        }

        /// <summary>
        /// Method that triggers a read operation to verify if the new EPC was written correctly
        /// </summary>
        private void TriggerVerificationRead(Tag tag)
        {
            if (IsCancellationRequested()) return;

            TagOpSequence seq = new TagOpSequence();
            TagReadOp readOp = new TagReadOp();
            readOp.AccessPassword = TagData.FromHexString(newAccessPassword);
            readOp.MemoryBank = MemoryBank.Epc;
            readOp.WordPointer = WordPointers.Epc;
            ushort wordCount = (ushort)(expectedEpc.Length / 4); // each word = 4 hex characters
            readOp.WordCount = wordCount;
            seq.Ops.Add(readOp);

            swVerify.Restart();
            reader.AddOpSequence(seq);
        }

        /// <summary>
        /// Event handler called when operations (write or read) are completed
        /// </summary>
        private void OnTagOpComplete(ImpinjReader reader, TagOpReport? report)
        {
            if (report == null || IsCancellationRequested()) return;
            
            foreach (TagOpResult result in report)
            {
                if (IsCancellationRequested()) return;

                string tidHex = result.Tag.Tid?.ToHexString() ?? "N/A";
                // If it's a write operation result
                if (result is TagWriteOpResult writeResult)
                {
                    swWrite.Stop();
                    Console.WriteLine($"Write completed for TID {tidHex} in {swWrite.ElapsedMilliseconds} ms");
                    // After write, trigger read operation for verification
                    TriggerVerificationRead(currentTargetTag);
                }
                // If it's a read operation result (verification)
                else if (result is TagReadOpResult readResult)
                {
                    swVerify.Stop();
                    string verifiedEpc = readResult.Data?.ToHexWordString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string resultStatus = verifiedEpc.Equals(expectedEpc, StringComparison.OrdinalIgnoreCase) ? "Success" : "Failure";
                    long writeTime = swWrite.ElapsedMilliseconds;
                    long verifyTime = swVerify.ElapsedMilliseconds;

                    // Increment retry count if necessary
                    int retries = retryCount.ContainsKey(tidHex) ? retryCount[tidHex] : 0;
                    double resultRssi = 0;
                    if (readResult.Tag.IsPcBitsPresent)
                        resultRssi = readResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (readResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = readResult.Tag.AntennaPortNumber;

                    Console.WriteLine($"Verification for TID {tidHex}: EPC read = {verifiedEpc} ({resultStatus}) in {verifyTime} ms");

                    if (resultStatus == "Failure" && retries < maxRetries)
                    {
                        retryCount[tidHex] = retries + 1;
                        Console.WriteLine($"Verification failed, retry {retryCount[tidHex]} for TID {tidHex}");
                        // Re-execute write and verification cycle
                        TriggerWriteAndVerify(currentTargetTag);
                    }
                    else
                    {
                        LogToCsv($"{timestamp},{tidHex},{currentTargetTag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{writeTime},{verifyTime},{resultStatus},{retries},{resultRssi},{antennaPort}");
                        TagOpController.RecordResult(tidHex, resultStatus);

                        // Reset isTargetTidSet to allow processing of new tags
                        isTargetTidSet = false;
                    }
                }
            }
        }
    }
}
