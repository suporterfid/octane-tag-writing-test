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
        // Cycle counter
        private int cycleCount;
        // Test execution control flag
        private bool enduranceRunning;
        // Identified target tag
        private Tag? targetTag;
        // New EPC to be written in the cycle
        private string? expectedEpc;
        // Stopwatch to measure each cycle time
        private readonly Stopwatch swCycle = new Stopwatch();

        public TestCase8EnduranceStrategy(string hostname)
            : base(hostname, "TestCase8_Endurance_Log.csv")
        {
        }

        public override void RunTest()
        {
            try
            {
                Console.WriteLine("=== Endurance Test ===");
                // Configure reader (connection, settings, EPC list loading, low latency)
                ConfigureReader();

                // Subscribe to read and operation completion events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;

                reader.Start();
                Console.WriteLine("Waiting for target tag identification to start endurance test...");

                // Wait until target tag is found
                while (targetTag == null)
                {
                    Thread.Sleep(100);
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.S)
                    {
                        Console.WriteLine("Test interrupted by user.");
                        reader.Stop();
                        reader.Disconnect();
                        return;
                    }
                }

                enduranceRunning = true;
                Console.WriteLine("Target tag identified. Starting endurance test. Press 'S' to stop.");

                // Endurance loop: execute write and verification cycles
                while (enduranceRunning && cycleCount < maxCycles)
                {
                    TriggerWriteAndVerify(targetTag);
                    cycleCount++;
                    Console.WriteLine($"Cycle {cycleCount} triggered.");
                    // Wait for a brief interval between cycles (adjustable)
                    Thread.Sleep(500);
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.S)
                    {
                        Console.WriteLine("Interruption requested by user.");
                        enduranceRunning = false;
                    }
                }

                reader.Stop();
                reader.Disconnect();
                Console.WriteLine("Endurance test completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in endurance test: " + ex.Message);
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
                // Set target TID if not set yet
                if (!isTargetTidSet && !string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"Target TID set to: {tidHex}");
                }

                // If TID matches target and hasn't been set as target yet
                if (tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase) && targetTag == null)
                {
                    targetTag = tag;
                    Console.WriteLine("Target tag found: EPC={0}, TID={1}", tag.Epc.ToHexString(), tidHex);
                    // Unsubscribe to avoid target redefinition
                    reader.TagsReported -= OnTagsReported;
                    break;
                }
            }
        }

        /// <summary>
        /// Triggers write operation (with Access password update) and starts verification
        /// </summary>
        private void TriggerWriteAndVerify(Tag tag)
        {
            string oldEpc = tag.Epc.ToHexString();
            // Get new EPC via helper (generation or extraction from list)
            expectedEpc = TagOpController.GetNextEpcForTag();
            Console.WriteLine("Cycle {0}: Writing new EPC: {1} -> {2}", cycleCount + 1, oldEpc, expectedEpc);

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
        }

        /// <summary>
        /// Triggers read operation to verify written EPC
        /// </summary>
        private void TriggerVerificationRead(Tag tag)
        {
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
            if (report == null) return;
            
            foreach (TagOpResult result in report)
            {
                string tidHex = result.Tag.Tid?.ToHexString() ?? "N/A";

                // If it's a write operation result
                if (result is TagWriteOpResult writeResult)
                {
                    swCycle.Stop();
                    if (writeResult.Result != WriteResultStatus.Success)
                    {
                        Console.WriteLine("Cycle {0} - Write failure for TID {1}: {2}", cycleCount, tidHex, writeResult.Result);
                        // In an endurance test, failure can be recorded and cycle can continue
                        LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{targetTag.Epc.ToHexString()},{expectedEpc},N/A,{swCycle.ElapsedMilliseconds},0,Failure,{cycleCount}");
                        TagOpController.RecordResult(tidHex, "Write Error");
                    }
                    else
                    {
                        Console.WriteLine("Cycle {0} - Write completed for TID {1} in {2} ms", cycleCount, tidHex, swCycle.ElapsedMilliseconds);
                        // After write, trigger read operation for verification
                        TriggerVerificationRead(targetTag);
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

                    Console.WriteLine("Cycle {0} - Verification for TID {1}: EPC read = {2} ({3}) in {4} ms", cycleCount, tidHex, verifiedEpc, resultStatus, operationTime);
                    LogToCsv($"{timestamp},{tidHex},{targetTag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{operationTime},0,{resultStatus},{cycleCount}");
                    TagOpController.RecordResult(tidHex, resultStatus);
                }
            }
        }
    }
}
