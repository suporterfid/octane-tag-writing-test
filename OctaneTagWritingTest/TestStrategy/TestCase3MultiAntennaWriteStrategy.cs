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
        private readonly Stopwatch sw = new Stopwatch();
        private readonly object swLock = new object();

        public TestCase3MultiAntennaWriteStrategy(string hostname)
            : base(hostname, "TestCase3_MultiAntenna_Log.csv")
        {
        }

        public override void RunTest()
        {
            try
            {
                if (reader == null)
                {
                    throw new InvalidOperationException("Reader not initialized properly");
                }

                // Configure the reader and load predefined EPCs
                ConfigureReader();

                // Subscribe to read and write operation events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                // Ensure log file exists
                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,OldEPC,NewEPC,WriteTime_ms,Result,Antenna");

                Console.WriteLine("Multi-Antenna test active. Waiting for tag reads. Press Enter to stop...");
                Console.ReadLine();

                reader.Stop();
                reader.Disconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in MultiAntennaTestStrategy: " + ex.Message);
            }
        }

        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null) return;
            
            foreach (Tag tag in report)
            {
                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                // If result already exists for this TID, skip it
                if (TagOpController.HasResult(tidHex))
                    continue;

                // Set target TID if not set yet
                if (!isTargetTidSet && !string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"Target TID set to: {tidHex}");
                }

                // Filter by desired TID
                if (!string.IsNullOrEmpty(tidHex) && tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Tag read: EPC={0}, TID={1}, Antenna={2}",
                        tag.Epc.ToHexString(), tidHex, tag.AntennaPortNumber);
                    // For this test, we can trigger the operation without removing the event,
                    // allowing multiple events from the same tag to be processed as needed.
                    TriggerWrite(tag);
                }
            }
        }

        private void TriggerWrite(Tag tag)
        {
            string oldEpc = tag.Epc.ToHexString();
            string novoEpc = TagOpController.GetNextEpcForTag();
            Console.WriteLine("Triggering write: {0} -> {1} on Antenna {2}",
                oldEpc, novoEpc, tag.AntennaPortNumber);

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
            if (report == null) return;
            
            foreach (TagOpResult result in report)
            {
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
                    int antenna = writeResult.Tag.AntennaPortNumber;

                    Console.WriteLine("Write on Antenna {0} completed: {1} in {2} ms",
                        antenna, res, writeTime);
                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{writeTime},{res},{antenna}");
                    TagOpController.RecordResult(tidHex, res);
                }
            }
        }
    }
}
