using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctaneTagWritingTest.TestStrategy
{
    /// <summary>
    /// Test strategy for multiple antenna operations
    /// </summary>
    public class TestCase3MultiAntennaWriteStrategy : BaseTestStrategy
    {
        private readonly object swLock = new object();

        public TestCase3MultiAntennaWriteStrategy(string hostname, string logFile)
            : base(hostname, logFile)
        {
        }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                
                if (reader == null)
                {
                    throw new InvalidOperationException("Reader not initialized properly");
                }

                Console.WriteLine("Executing Multi-Antenna Write Test Strategy...");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                // Configure the reader and load predefined EPCs
                ConfigureReader();

                // Subscribe to read and write operation events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                // Ensure log file exists
                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,OldEPC,NewEPC,WriteTime,Result,RSSI,AntennaPort");

                // Keep the test running until cancellation is requested
                while (!IsCancellationRequested())
                {
                    Thread.Sleep(100); // Small delay to prevent CPU spinning
                }

                Console.WriteLine("\nStopping test...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in MultiAntennaTestStrategy: " + ex.Message);
            }
            finally
            {
                CleanupReader();
            }
        }

        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null || IsCancellationRequested()) return;
            
            foreach (Tag tag in report)
            {
                if (IsCancellationRequested()) return;

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                // If result already exists for this TID, skip it
                if (TagOpController.HasResult(tidHex))
                    continue;

                // Reset target TID for each new tag to enable continuous encoding
                if (!string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"\nNew target TID found: {tidHex}");
                }

                // Filter by desired TID
                if (!string.IsNullOrEmpty(tidHex) && tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Processing tag: EPC={tag.Epc.ToHexString()}, TID={tidHex}, Antenna={tag.AntennaPortNumber}");
                    // For this test, we can trigger the operation without removing the event,
                    // allowing multiple events from the same tag to be processed as needed.
                    TriggerWrite(tag);
                }
            }
        }

        private void TriggerWrite(Tag tag)
        {
            if (IsCancellationRequested()) return;

            string oldEpc = tag.Epc.ToHexString();
            string novoEpc = TagOpController.GetNextEpcForTag();
            Console.WriteLine($"Triggering write: {oldEpc} -> {novoEpc} on Antenna {tag.AntennaPortNumber}");

            // Prepare write operation with BlockWrite enabled
            TagOpSequence seq = new TagOpSequence();
            seq.BlockWriteEnabled = true;
            seq.BlockWriteWordCount = 2;
            seq.TargetTag.MemoryBank = MemoryBank.Epc;
            seq.TargetTag.BitPointer = BitPointers.Epc;
            seq.TargetTag.Data = oldEpc;

            // Update Access password (write to Reserved bank)
            TagWriteOp updateAccessPwd = new TagWriteOp();
            updateAccessPwd.AccessPassword = null;
            updateAccessPwd.MemoryBank = MemoryBank.Reserved;
            updateAccessPwd.WordPointer = WordPointers.AccessPassword;
            updateAccessPwd.Data = TagData.FromHexString(newAccessPassword);
            seq.Ops.Add(updateAccessPwd);

            // Write the new EPC
            TagWriteOp writeOp = new TagWriteOp();
            writeOp.AccessPassword = TagData.FromHexString(newAccessPassword);
            writeOp.MemoryBank = MemoryBank.Epc;
            writeOp.WordPointer = WordPointers.Epc;
            writeOp.Data = TagData.FromHexString(novoEpc);
            seq.Ops.Add(writeOp);

            lock (swLock)
            {
                sw.Restart();
            }
            reader?.AddOpSequence(seq);
        }

        private void OnTagOpComplete(ImpinjReader reader, TagOpReport? report)
        {
            if (report == null || IsCancellationRequested()) return;
            
            foreach (TagOpResult result in report)
            {
                if (IsCancellationRequested()) return;

                if (result is TagWriteOpResult writeResult)
                {
                    long writeTime;
                    lock (swLock)
                    {
                        sw.Stop();
                        writeTime = sw.ElapsedMilliseconds;
                    }
                    
                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string oldEpc = writeResult.Tag.Epc.ToHexString();
                    string newEpc = TagOpController.GetExpectedEpc(tidHex);
                    string res = writeResult.Result.ToString();
                    double resultRssi = 0;
                    if (writeResult.Tag.IsPcBitsPresent)
                        resultRssi = writeResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (writeResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = writeResult.Tag.AntennaPortNumber;

                    Console.WriteLine($"Write on Antenna {antennaPort} completed: {res} in {writeTime} ms");
                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{writeTime},{res},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, res);

                    // Reset isTargetTidSet to allow processing of new tags
                    isTargetTidSet = false;
                }
            }
        }
    }
}
