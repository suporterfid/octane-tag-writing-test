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

        public override void RunTest()
        {
            try
            {
                Console.WriteLine("Starting error recovery test (Error Recovery Strategy)...");
                // Configure reader (connection, settings, EPC list, low latency)
                ConfigureReader();

                // Subscribe to read and operation completion events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                // Create log file if it doesn't exist
                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,Previous_EPC,Expected_EPC,Verified_EPC,WriteTime_ms,VerifyTime_ms,Result,RecoveryAttempts,RSSI,AntennaPort");

                Console.WriteLine("Error recovery test running. Press Enter to stop.");
                Console.ReadLine();

                reader.Stop();
                reader.Disconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in error recovery test: " + ex.Message);
            }
        }

        /// <summary>
        /// Event handler called when reader reports tags
        /// </summary>
        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null) return;
            
            foreach (Tag tag in report)
            {
                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (string.IsNullOrEmpty(tidHex))
                    continue;

                // If result already exists for this TID, skip the event
                if (TagOpController.HasResult(tidHex))
                    continue;

                // Set target TID if not set yet
                if (!isTargetTidSet && !string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"Target TID set to: {tidHex}");
                }

                // Filter by target TID
                if (tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Target tag identified: EPC={0}, TID={1}", tag.Epc.ToHexString(), tidHex);
                    // Unsubscribe to avoid multiple executions for the same tag
                    reader.TagsReported -= OnTagsReported;
                    currentTargetTag = tag;
                    recoveryCount[tidHex] = 0;
                    TriggerWrite(currentTargetTag);
                    break;
                }
            }
        }

        /// <summary>
        /// Method that triggers write operation (with Access password update and new EPC writing)
        /// </summary>
        private void TriggerWrite(Tag tag)
        {
            string oldEpc = tag.Epc.ToHexString();
            // Get new EPC to be written (using TagOpController helper)
            expectedEpc = TagOpController.GetNextEpcForTag();
            Console.WriteLine("Attempting write operation for TID {0}: {1} -> {2}", tag.Tid.ToHexString(), oldEpc, expectedEpc);

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
        }

        /// <summary>
        /// Method to trigger a read operation to verify the written EPC
        /// </summary>
        private void TriggerVerificationRead(Tag tag)
        {
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
            if (report == null) return;
            
            foreach (TagOpResult result in report)
            {
                string tidHex = result.Tag.Tid?.ToHexString() ?? "N/A";

                // If it's a write operation result
                if (result is TagWriteOpResult writeResult)
                {
                    swWrite.Stop();
                    if (writeResult.Result != WriteResultStatus.Success)
                    {
                        Console.WriteLine("Write error for TID {0}: {1}", tidHex, writeResult.Result);
                        // Increment attempt count
                        if (recoveryCount.ContainsKey(tidHex) && recoveryCount[tidHex] < maxRecoveryAttempts)
                        {
                            recoveryCount[tidHex]++;
                            Console.WriteLine("Recovery attempt {0} for TID {1}", recoveryCount[tidHex], tidHex);
                            // Re-execute write operation
                            TriggerWrite(currentTargetTag);
                        }
                        else
                        {
                            Console.WriteLine("Maximum number of attempts reached for TID {0}.", tidHex);
                            LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{currentTargetTag.Epc.ToHexString()},{expectedEpc},N/A,{swWrite.ElapsedMilliseconds},0,Failure,{recoveryCount[tidHex]}");
                            TagOpController.RecordResult(tidHex, "Error");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Write completed for TID {0} in {1} ms", tidHex, swWrite.ElapsedMilliseconds);
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

                    Console.WriteLine("Verification for TID {0}: EPC read = {1} ({2}) in {3} ms", tidHex, verifiedEpc, resultStatus, verifyTime);
                    LogToCsv($"{timestamp},{tidHex},{currentTargetTag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{writeTime},{verifyTime},{resultStatus},{attempts},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, resultStatus);
                }
            }
        }
    }
}
