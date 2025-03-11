using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;

namespace OctaneTagWritingTest.TestStrategy
{
    /// <summary>
    /// Endurance strategy for continuous write and verification testing
    /// </summary>
    public class TestCase8EnduranceStrategy : BaseTestStrategy
    {
        // Maximum number of test cycles to execute (or undefined)
        private const int maxCycles = 10000;
        // Cycle counter per TID
        private readonly Dictionary<string, int> cycleCount = new Dictionary<string, int>();
        // Test execution control flag
        private bool enduranceRunning;
        // Current target tag
        private Tag? currentTargetTag;
        // New EPC to be written in the cycle
        private string? expectedEpc;
        // Stopwatch to measure each cycle time
        private readonly Stopwatch swCycle = new Stopwatch();

        public TestCase8EnduranceStrategy(string hostname, string logFile) : base(hostname, logFile)
        {
        }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("=== Endurance Test ===");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                // Configure reader (connection, settings, EPC list loading, low latency)
                ConfigureReader();

                // Subscribe to read and operation completion events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;

                reader.Start();
                
                // Create log file if it doesn't exist
                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,Previous_EPC,Expected_EPC,Verified_EPC,WriteTime_ms,VerifyTime_ms,Result,CycleCount,RSSI,AntennaPort");

                enduranceRunning = true;

                // Keep the test running until cancellation is requested
                while (!IsCancellationRequested())
                {
                    Thread.Sleep(100); // Small delay to prevent CPU spinning
                }

                Console.WriteLine("\nStopping test...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in endurance test: " + ex.Message);
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

                // Initialize cycle count for new TID
                if (!cycleCount.ContainsKey(tidHex))
                {
                    cycleCount[tidHex] = 0;
                }

                // Skip if max cycles reached for this TID
                if (cycleCount[tidHex] >= maxCycles)
                {
                    continue;
                }

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
                    currentTargetTag = tag;
                    Console.WriteLine($"Processing tag (Cycle {cycleCount[tidHex] + 1}): EPC={tag.Epc.ToHexString()}, TID={tidHex}");
                    TriggerWriteAndVerify(tag);
                    break;
                }
            }
        }

        /// <summary>
        /// Triggers write operation (with Access password update) and starts verification
        /// </summary>
        private void TriggerWriteAndVerify(Tag tag)
        {
            if (IsCancellationRequested()) return;

            string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
            string oldEpc = tag.Epc.ToHexString();
            expectedEpc = TagOpController.GetNextEpcForTag();
            Console.WriteLine($"Cycle {cycleCount[tidHex] + 1}: Writing new EPC: {oldEpc} -> {expectedEpc}");

            // Create operation sequence
            TagOpSequence seq = new TagOpSequence();
            seq.BlockWriteEnabled = true;
            seq.BlockWriteWordCount = 2;
            seq.TargetTag.MemoryBank = MemoryBank.Epc;
            seq.TargetTag.BitPointer = BitPointers.Epc;
            seq.TargetTag.Data = oldEpc;

            // Operation to update Access password (Reserved Bank)
            TagWriteOp updateAccessPwd = new TagWriteOp();
            updateAccessPwd.AccessPassword = null; // default password (00000000)
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

            swCycle.Restart();
            reader.AddOpSequence(seq);

            TagOpController.RecordExpectedEpc(tidHex, expectedEpc);
        }

        /// <summary>
        /// Triggers read operation to verify written EPC
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

            swCycle.Restart();
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
                    swCycle.Stop();
                    double resultRssi = 0;
                    if (writeResult.Tag.IsPcBitsPresent)
                        resultRssi = writeResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (writeResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = writeResult.Tag.AntennaPortNumber;

                    if (writeResult.Result != WriteResultStatus.Success)
                    {
                        Console.WriteLine($"Cycle {cycleCount[tidHex]} - Write failure for TID {tidHex}: {writeResult.Result}");
                        LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{currentTargetTag.Epc.ToHexString()},{expectedEpc},N/A,{swCycle.ElapsedMilliseconds},0,Failure,{cycleCount[tidHex]},{resultRssi},{antennaPort}");
                        TagOpController.RecordResult(tidHex, "Write Error");

                        // Reset isTargetTidSet to allow processing of new tags
                        isTargetTidSet = false;
                    }
                    else
                    {
                        Console.WriteLine($"Cycle {cycleCount[tidHex]} - Write completed for TID {tidHex} in {swCycle.ElapsedMilliseconds} ms");
                        // After write, trigger read operation for verification
                        TriggerVerificationRead(currentTargetTag);
                    }
                }
                // If it's a read operation result (verification)
                else if (result is TagReadOpResult readResult)
                {
                    swCycle.Stop();
                    string verifiedEpc = readResult.Data?.ToHexWordString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string resultStatus = verifiedEpc.Equals(expectedEpc, StringComparison.OrdinalIgnoreCase) ? "Success" : "Failure";
                    long operationTime = swCycle.ElapsedMilliseconds;

                    double resultRssi = 0;
                    if (readResult.Tag.IsPcBitsPresent)
                        resultRssi = readResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (readResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = readResult.Tag.AntennaPortNumber;

                    Console.WriteLine($"Cycle {cycleCount[tidHex]} - Verification for TID {tidHex}: EPC read = {verifiedEpc} ({resultStatus}) in {operationTime} ms");
                    LogToCsv($"{timestamp},{tidHex},{currentTargetTag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{operationTime},0,{resultStatus},{cycleCount[tidHex]},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, resultStatus);

                    // Increment cycle count for this TID
                    cycleCount[tidHex]++;

                    // Reset isTargetTidSet to allow processing of new tags
                    isTargetTidSet = false;
                }
            }
        }
    }
}
