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
    /// Test Strategy - Example 2: Inline Write Test
    /// </summary>
    public class TestCase2InlineWriteStrategy : BaseTestStrategy
    {
        public TestCase2InlineWriteStrategy(string hostname, string logFile) : base(hostname, logFile) { }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("Executing Inline Write Test Strategy...");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                ConfigureReader();

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

        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null || IsCancellationRequested()) return;
            
            foreach (Tag tag in report.Tags)
            {
                if (IsCancellationRequested()) return;

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (TagOpController.HasResult(tidHex))
                    continue;

                // Reset target TID for each new tag to enable continuous encoding
                if (!string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"\nNew target TID found: {tidHex}");
                }

                if (!string.IsNullOrEmpty(tidHex) && tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Processing tag (Inline): EPC={tag.Epc.ToHexString()}, TID={tidHex}");
                    
                    string novoEpc = TagOpController.GetNextEpcForTag();
                    Console.WriteLine($"Writing inline: {tag.Epc.ToHexString()} -> {novoEpc}");
                    TagOpController.RecordExpectedEpc(tidHex, novoEpc);

                    TagOpSequence seq = new TagOpSequence();
                    seq.BlockWriteEnabled = true;
                    seq.BlockWriteWordCount = 2;
                    seq.TargetTag.MemoryBank = MemoryBank.Epc;
                    seq.TargetTag.BitPointer = BitPointers.Epc;
                    seq.TargetTag.Data = tag.Epc.ToHexString();

                    TagWriteOp updateAccessPwd = new TagWriteOp();
                    updateAccessPwd.AccessPassword = null;
                    updateAccessPwd.MemoryBank = MemoryBank.Reserved;
                    updateAccessPwd.WordPointer = WordPointers.AccessPassword;
                    updateAccessPwd.Data = TagData.FromHexString(newAccessPassword);
                    seq.Ops.Add(updateAccessPwd);

                    TagWriteOp writeOp = new TagWriteOp();
                    writeOp.AccessPassword = TagData.FromHexString(newAccessPassword);
                    writeOp.MemoryBank = MemoryBank.Epc;
                    writeOp.WordPointer = WordPointers.Epc;
                    writeOp.Data = TagData.FromHexString(novoEpc);
                    seq.Ops.Add(writeOp);

                    sw.Restart();
                    reader.AddOpSequence(seq);

                    if (!File.Exists(logFile))
                        LogToCsv("Timestamp,TID,OldEPC,NewEPC,WriteTime,Result,RSSI,AntennaPort");
                    break;
                }
            }
        }

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
                    if (writeResult.Tag.IsPcBitsPresent)
                        resultRssi = writeResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (writeResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = writeResult.Tag.AntennaPortNumber;

                    Console.WriteLine($"Inline write completed: {res} in {writeTime} ms");
                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{writeTime},{res},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, res);

                    // Reset isTargetTidSet to allow processing of new tags
                    isTargetTidSet = false;
                }
            }
        }
    }
}
