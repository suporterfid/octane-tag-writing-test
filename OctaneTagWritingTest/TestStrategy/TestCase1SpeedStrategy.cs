using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctaneTagWritingTest.TestStrategy
{
    /// <summary>
    /// Test Strategy - Example 1: Optimal Write Speed Test
    /// This strategy measures the speed of writing new EPCs to tags
    /// </summary>
    public class TestCase1SpeedStrategy : BaseTestStrategy
    {
        /// <summary>
        /// Initializes a new instance of the TestCase1SpeedStrategy class
        /// </summary>
        /// <param name="hostname">The hostname of the RFID reader</param>
        /// <param name="logFile">The path to the log file for test results</param>
        public TestCase1SpeedStrategy(string hostname, string logFile) : base(hostname, logFile) { }

        /// <summary>
        /// Executes the speed test strategy
        /// </summary>
        /// <remarks>
        /// This method:
        /// - Configures and starts the reader
        /// - Sets up event handlers for tag reports and operations
        /// - Runs until user presses Enter
        /// - Logs results to a CSV file
        /// </remarks>
        public override void RunTest()
        {
            try
            {
                Console.WriteLine("Executing Speed Test Strategy...");
                ConfigureReader();

                // Subscribe to read and write events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                Console.WriteLine("Speed test running. Press Enter to stop.");
                Console.ReadLine();

                reader.Stop();
                reader.Disconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Test error: " + ex.Message);
            }
        }

        /// <summary>
        /// Handles the TagsReported event from the reader
        /// </summary>
        /// <param name="sender">The reader that generated the event</param>
        /// <param name="report">The report containing tag data</param>
        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null) return;
            
            foreach (Tag tag in report)
            {
                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (TagOpController.HasResult(tidHex))
                    continue;

                // Set target TID if not set yet
                if (!isTargetTidSet && !string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"Target TID set to: {tidHex}");
                }

                if (!string.IsNullOrEmpty(tidHex) && tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                Console.WriteLine("Tag found: EPC={0}, TID={1}", tag.Epc.ToHexString(), tidHex);
                    reader.TagsReported -= OnTagsReported;

                    string novoEpc = TagOpController.GetNextEpcForTag();
                    Console.WriteLine("Writing new EPC: {0} -> {1}", tag.Epc.ToHexString(), novoEpc);
                    TagOpController.RecordExpectedEpc(tidHex, novoEpc);

                    TagOpSequence seq = new TagOpSequence();
                    seq.BlockWriteEnabled = true;
                    seq.BlockWriteWordCount = 2;
                    seq.TargetTag.MemoryBank = MemoryBank.Epc;
                    seq.TargetTag.BitPointer = BitPointers.Epc;
                    seq.TargetTag.Data = tag.Epc.ToHexString();

                    // Update Access password
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

                    sw.Restart();
                    reader.AddOpSequence(seq);

                    if (!File.Exists(logFile))
                        LogToCsv("Timestamp,TID,OldEPC,NewEPC,WriteTime,Result");
                    break;
                }
            }
        }

        /// <summary>
        /// Handles the TagOpComplete event from the reader
        /// </summary>
        /// <param name="reader">The reader that generated the event</param>
        /// <param name="report">The report containing operation results</param>
        private void OnTagOpComplete(ImpinjReader reader, TagOpReport? report)
        {
            if (report == null) return;
            
            foreach (TagOpResult result in report)
            {
                if (result is TagWriteOpResult writeResult)
                {
                    sw.Stop();
                    string tidHex = writeResult.Tag.Tid != null ? writeResult.Tag.Tid.ToHexString() : "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string oldEpc = writeResult.Tag.Epc.ToHexString();
                    string newEpc = TagOpController.GetExpectedEpc(tidHex);
                    long writeTime = sw.ElapsedMilliseconds;
                    string res = writeResult.Result.ToString();

                    Console.WriteLine("Write completed: {0} in {1} ms", res, writeTime);
                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{writeTime},{res}");
                    TagOpController.RecordResult(tidHex, res);
                }
            }
        }
    }
}
