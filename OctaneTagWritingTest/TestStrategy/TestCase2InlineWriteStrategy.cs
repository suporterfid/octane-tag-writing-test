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
        public override void RunTest()
        {
            try
            {
                Console.WriteLine("Executing Inline Write Test Strategy...");
                ConfigureReader();

                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                Console.WriteLine("Inline test running. Press Enter to stop.");
                Console.ReadLine();

                reader.Stop();
                reader.Disconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Test error: " + ex.Message);
            }
        }
        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null) return;
            
            foreach (Tag tag in report.Tags)
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
                    Console.WriteLine("Tag found (Inline): EPC={0}, TID={1}", tag.Epc.ToHexString(), tidHex);
                    reader.TagsReported -= OnTagsReported;
                    string novoEpc = TagOpController.GetNextEpcForTag();
                    Console.WriteLine("Writing inline: {0} -> {1}", tag.Epc.ToHexString(), novoEpc);
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
                    double resultRssi = 0;
                    if (writeResult.Tag.IsPcBitsPresent)
                        resultRssi = writeResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (writeResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = writeResult.Tag.AntennaPortNumber;

                    Console.WriteLine("Inline write completed: {0} in {1} ms", res, writeTime);
                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{writeTime},{res},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, res);
                }
            }
        }
    }
}
