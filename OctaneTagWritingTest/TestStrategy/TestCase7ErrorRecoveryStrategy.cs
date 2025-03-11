using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;

namespace OctaneTagWritingTest.TestStrategy
{
    /// <summary>
    /// Strategy for error recovery testing that re-executes write operation in case of failure
    /// </summary>
    public class TestCase7ErrorRecoveryStrategy : BaseTestStrategy
    {
        // Maximum number of recovery attempts for write operation
        private const int maxRecoveryAttempts = 3;
        // Dictionary to track number of attempts per TID
        private readonly Dictionary<string, int> recoveryCount = new Dictionary<string, int>();

        private readonly Stopwatch swWrite = new Stopwatch();
        private readonly Stopwatch swVerify = new Stopwatch();
        private Tag? currentTargetTag;
        private string? expectedEpc;

        public TestCase7ErrorRecoveryStrategy(string hostname, string logFile)
            : base(hostname, logFile)
        {
        }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("Starting error recovery test (Error Recovery Strategy)...");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                // Configure reader (connection, settings, EPC list, low latency)
                ConfigureReader();

                // Subscribe to read and operation completion events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                // Create log file if it doesn't exist
                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,Previous_EPC,Expected_EPC,Verified_EPC,WriteTime_ms,VerifyTime_ms,Result,RecoveryAttempts,RSSI,AntennaPort");

                // Keep the test running until cancellation is requested
                while (!IsCancellationRequested())
                {
                    Thread.Sleep(100); // Small delay to prevent CPU spinning
                }

                Console.WriteLine("\nStopping test...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in error recovery test: " + ex.Message);
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

            foreach (Tag tag in report.Tags)
            {
                if (IsCancellationRequested()) return;

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (string.IsNullOrEmpty(tidHex)) continue;

                if (TagOpController.HasResult(tidHex))
                {
                    Console.WriteLine($"Skipping tag {tidHex}, EPC already assigned.");
                    continue;
                }

                string currentEpc = tag.Epc.ToHexString();
                string expectedEpc = TagOpController.GetExpectedEpc(tidHex);

                if (!string.IsNullOrEmpty(expectedEpc) && expectedEpc.Equals(currentEpc, StringComparison.OrdinalIgnoreCase))
                {
                    TagOpController.RecordResult(tidHex, currentEpc);
                    Console.WriteLine($"Tag {tidHex} already has expected EPC: {expectedEpc}");
                    continue;
                }

                if (!isTargetTidSet || !tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"\nNew target TID found: {tidHex}");

                    string newEpcToWrite = TagOpController.GetNextEpcForTag();
                    Console.WriteLine($"Assigning new EPC: {currentEpc} -> {newEpcToWrite}");

                    TagOpController.RecordExpectedEpc(tidHex, newEpcToWrite);

                    currentTargetTag = tag;
                    recoveryCount[tidHex] = 0;
                    TriggerWrite(currentTargetTag);
                }
            }
        }

        /// <summary>
        /// Method that triggers write operation (with Access password update and new EPC writing)
        /// </summary>
        private void TriggerWrite(Tag tag)
        {
            if (IsCancellationRequested()) return;

            string oldEpc = tag.Epc.ToHexString();
            // Get new EPC to be written (using TagOpController helper)
            expectedEpc = TagOpController.GetNextEpcForTag();
            Console.WriteLine($"Attempting write operation for TID {tag.Tid.ToHexString()}: {oldEpc} -> {expectedEpc}");

            TagOpSequence seq = new TagOpSequence();
            seq.BlockWriteEnabled = true;
            seq.BlockWriteWordCount = 2;
            seq.TargetTag.MemoryBank = MemoryBank.Epc;
            seq.TargetTag.BitPointer = BitPointers.Epc;
            seq.TargetTag.Data = oldEpc;

            // Operation to update Access password in Reserved bank
            TagWriteOp updateAccessPwd = new TagWriteOp();
            updateAccessPwd.AccessPassword = null; // Default password (00000000)
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
        /// Method to trigger a read operation to verify the written EPC
        /// </summary>
        private void TriggerVerificationRead(Tag tag)
        {
            if (IsCancellationRequested()) return;

            TagOpSequence seq = new TagOpSequence();
            TagReadOp readOp = new TagReadOp();
            readOp.AccessPassword = TagData.FromHexString(newAccessPassword);
            readOp.MemoryBank = MemoryBank.Epc;
            readOp.WordPointer = WordPointers.Epc;
            ushort wordCount = (ushort)(expectedEpc.Length / 4);
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
                    if (writeResult.Result != WriteResultStatus.Success)
                    {
                        Console.WriteLine($"Write error for TID {tidHex}: {writeResult.Result}");
                        // Increment attempt count
                        if (recoveryCount.ContainsKey(tidHex) && recoveryCount[tidHex] < maxRecoveryAttempts)
                        {
                            recoveryCount[tidHex]++;
                            Console.WriteLine($"Recovery attempt {recoveryCount[tidHex]} for TID {tidHex}");
                            // Re-execute write operation
                            TriggerWrite(currentTargetTag);
                        }
                        else
                        {
                            Console.WriteLine($"Maximum number of attempts reached for TID {tidHex}.");
                            LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{currentTargetTag.Epc.ToHexString()},{expectedEpc},N/A,{swWrite.ElapsedMilliseconds},0,Failure,{recoveryCount[tidHex]}");
                            TagOpController.RecordResult(tidHex, "Error");

                            // Reset isTargetTidSet to allow processing of new tags
                            isTargetTidSet = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Write completed for TID {tidHex} in {swWrite.ElapsedMilliseconds} ms");
                        // After write, start read operation for verification
                        TriggerVerificationRead(currentTargetTag);
                    }
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
                    int attempts = recoveryCount.ContainsKey(tidHex) ? recoveryCount[tidHex] : 0;

                    double resultRssi = 0;
                    if (readResult.Tag.IsPcBitsPresent)
                        resultRssi = readResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (readResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = readResult.Tag.AntennaPortNumber;

                    Console.WriteLine($"Verification for TID {tidHex}: EPC read = {verifiedEpc} ({resultStatus}) in {verifyTime} ms");
                    LogToCsv($"{timestamp},{tidHex},{currentTargetTag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{writeTime},{verifyTime},{resultStatus},{attempts},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, resultStatus);

                    // Reset isTargetTidSet to allow processing of new tags
                    isTargetTidSet = false;
                }
            }
        }
    }
}
