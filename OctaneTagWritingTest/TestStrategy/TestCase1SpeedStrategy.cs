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
        /// - Runs continuously until cancellation is requested
        /// - Logs results to a CSV file
        /// </remarks>
        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("Executing Speed Test Strategy...");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");
                
                ConfigureReader();

                // Subscribe to read and write events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                // Keep the test running until cancellation is requested
                while (!IsCancellationRequested())
                {
                    Thread.Sleep(100); // Small delay to prevent CPU spinning
                }

                Console.WriteLine("\nStopping test...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Test error: " + ex.Message);
            }
            finally
            {
                CleanupReader();
            }
        }

        /// <summary>
        /// Handles the TagsReported event from the reader
        /// </summary>
        /// <param name="sender">The reader that generated the event</param>
        /// <param name="report">The report containing tag data</param>
        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null || IsCancellationRequested()) return;
            
            foreach (Tag tag in report)
            {
                if (IsCancellationRequested()) return;

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (TagOpController.HasResult(tidHex))
                {
                    Console.WriteLine($"Tag {tidHex} has been successfully updated with EPC {tag.Epc.ToHexString()}");
                    continue;
                }

                string newEpcExpected = TagOpController.GetExpectedEpc(tidHex);
                if(!string.IsNullOrEmpty(newEpcExpected) && newEpcExpected.Equals(tag.Epc.ToHexString(), StringComparison.OrdinalIgnoreCase))
                {
                    TagOpController.RecordResult(tidHex, tag.Epc.ToHexString());
                    Console.WriteLine($"On-read Tag {tidHex} has been successfully updated with EPC {newEpcExpected}");
                    continue;
                }
                    

                // Reset target TID for each new tag to enable continuous encoding
                if (!string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"\nNew target TID found: {tidHex}");
                }

                if (!string.IsNullOrEmpty(tidHex) && tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Processing tag: EPC={tag.Epc.ToHexString()}, TID={tidHex}");
                    
                    string newEpcToWrite = TagOpController.GetNextEpcForTag();
                    Console.WriteLine($"Writing new EPC: {tag.Epc.ToHexString()} -> {newEpcToWrite}");
                    TagOpController.RecordExpectedEpc(tidHex, newEpcToWrite);

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
                    writeOp.Data = TagData.FromHexString(newEpcToWrite);
                    seq.Ops.Add(writeOp);

                    sw.Restart();
                    reader.AddOpSequence(seq);

                    if (!File.Exists(logFile))
                        LogToCsv("Timestamp,TID,OldEPC,NewEPC,WriteTime,Result,RSSI,AntennaPort");
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
            if (report == null || IsCancellationRequested()) return;
            
            foreach (TagOpResult result in report)
            {
                if (IsCancellationRequested()) return;

                if (result is TagWriteOpResult writeResult)
                {
                    sw.Stop();
                    string tidHex = writeResult.Tag.Tid != null ? writeResult.Tag.Tid.ToHexString() : "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string oldEpc = writeResult.Tag.Epc.ToHexString();
                    string newEpc = TagOpController.GetExpectedEpc(tidHex);
                    long writeTime = sw.ElapsedMilliseconds;
                    string res = writeResult.Result.ToString();
                    double resultRssi = 0;
                    if(writeResult.Tag.IsPcBitsPresent)
                        resultRssi = writeResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (writeResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = writeResult.Tag.AntennaPortNumber;

                    Console.WriteLine($"Write completed: {res} in {writeTime} ms");
                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{writeTime},{res},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, res);

                    // Reset isTargetTidSet to allow processing of new tags
                    isTargetTidSet = false;
                }
            }
        }
    }
}
