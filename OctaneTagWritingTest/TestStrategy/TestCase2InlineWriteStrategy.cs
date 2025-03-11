using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using System;
using System.IO;
using System.Threading;

namespace OctaneTagWritingTest.TestStrategy
{
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

                while (!IsCancellationRequested())
                {
                    Thread.Sleep(100);
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
                {
                    continue;
                }

                string expectedEpc = TagOpController.GetExpectedEpc(tidHex);
                string currentEpc = tag.Epc.ToHexString();

                if (!string.IsNullOrEmpty(expectedEpc) && expectedEpc.Equals(currentEpc, StringComparison.OrdinalIgnoreCase))
                {
                    TagOpController.RecordResult(tidHex, currentEpc);
                    Console.WriteLine($"Tag {tidHex} already has expected EPC: {currentEpc}");
                    continue;
                }

                if (string.IsNullOrEmpty(targetTid) || !tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"\nNew target TID found: {tidHex}");

                    string newEpcToWrite = TagOpController.GetNextEpcForTag();
                    Console.WriteLine($"Assigning new EPC: {currentEpc} -> {newEpcToWrite}");
                    TagOpController.RecordExpectedEpc(tidHex, newEpcToWrite);

                    TagOpSequence seq = new TagOpSequence
                    {
                        BlockWriteEnabled = true,
                        BlockWriteWordCount = 2,
                        TargetTag = {
                            MemoryBank = MemoryBank.Epc,
                            BitPointer = BitPointers.Epc,
                            Data = currentEpc
                        }
                    };

                    TagWriteOp writeOp = new TagWriteOp
                    {
                        AccessPassword = TagData.FromHexString(newAccessPassword),
                        MemoryBank = MemoryBank.Epc,
                        WordPointer = WordPointers.Epc,
                        Data = TagData.FromHexString(newEpcToWrite)
                    };
                    seq.Ops.Add(writeOp);

                    sw.Restart();
                    reader.AddOpSequence(seq);

                    if (!File.Exists(logFile))
                        LogToCsv("Timestamp,TID,OldEPC,NewEPC,WriteTime,Result,RSSI,AntennaPort");
                }
            }
        }

        private void OnTagOpComplete(ImpinjReader reader, TagOpReport? report)
        {
            if (report == null || IsCancellationRequested()) return;

            foreach (TagOpResult result in report)
            {
                if (result is TagWriteOpResult writeResult)
                {
                    sw.Stop();
                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string oldEpc = writeResult.Tag.Epc.ToHexString();
                    string newEpc = TagOpController.GetExpectedEpc(tidHex);
                    long writeTime = sw.ElapsedMilliseconds;
                    string res = writeResult.Result.ToString();
                    double resultRssi = writeResult.Tag.IsPcBitsPresent ? writeResult.Tag.PeakRssiInDbm : 0;
                    ushort antennaPort = writeResult.Tag.IsAntennaPortNumberPresent ? writeResult.Tag.AntennaPortNumber : (ushort)0;

                    Console.WriteLine($"Inline write completed: {res} in {writeTime} ms");
                    LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{oldEpc},{newEpc},{writeTime},{res},{resultRssi},{antennaPort}");

                    if (res.Equals("Success"))
                    {
                        TagOpController.RecordResult(tidHex, newEpc);
                    }
                    else
                    {
                        TagOpController.RecordResult(tidHex, res);
                    }

                    isTargetTidSet = false;
                }
            }
        }
    }
}
