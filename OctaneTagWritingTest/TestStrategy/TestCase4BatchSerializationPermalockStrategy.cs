using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;

namespace OctaneTagWritingTest.TestStrategy
{
    public class TestCase4BatchSerializationPermalockStrategy : BaseTestStrategy
    {
        private readonly List<Tag> loteTags = new();
        private readonly object batchLock = new();
        private int serialCounter = 0;

        public TestCase4BatchSerializationPermalockStrategy(string hostname, string logFile) : base(hostname, logFile) { }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;

                ConfigureReader();
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,OldEPC,NewEPC,SerialCounter,WriteTime,Result,RSSI,AntennaPort");

                while (!IsCancellationRequested())
                {
                    ProcessAccumulatedTags();
                    Thread.Sleep(100);
                }
            }
            finally
            {
                CleanupReader();
            }
        }

        private void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            if (report == null || IsCancellationRequested()) return;

            lock (loteTags)
            {
                foreach (Tag tag in report.Tags)
                {
                    string tidHex = tag.Tid?.ToHexString() ?? string.Empty;

                    if (TagOpController.HasResult(tidHex))
                    {
                        Console.WriteLine($"Skipping tag {tidHex}, EPC already assigned.");
                        continue;
                    }

                    string expectedEpc = TagOpController.GetExpectedEpc(tidHex);
                    if (!string.IsNullOrEmpty(expectedEpc) && expectedEpc.Equals(tag.Epc.ToHexString(), StringComparison.OrdinalIgnoreCase))
                    {
                        TagOpController.RecordResult(tidHex, tag.Epc.ToHexString());
                        Console.WriteLine($"Tag {tidHex} already has expected EPC: {expectedEpc}");
                        continue;
                    }

                    if (!loteTags.Exists(t => t.Tid.ToHexString() == tidHex))
                    {
                        Console.WriteLine($"Adding new tag to batch: TID={tidHex}, EPC={tag.Epc.ToHexString()}");
                        loteTags.Add(tag);
                    }
                }
            }
        }



        private void ProcessAccumulatedTags()
        {
            if (IsCancellationRequested()) return;

            List<Tag> tagsToProcess;
            lock (batchLock)
            {
                tagsToProcess = new List<Tag>(loteTags);
                loteTags.Clear();
            }

            foreach (var tag in tagsToProcess)
            {
                if (IsCancellationRequested()) return;

                string tidHex = tag.Tid.ToHexString();
                string newEpc = TagOpController.GetNextEpcForTag();
                TagOpController.RecordExpectedEpc(tidHex, newEpc);

                Console.WriteLine($"Batch writing EPC {newEpc} to tag {tidHex}");

                TagOpSequence seq = new()
                {
                    BlockWriteEnabled = true,
                    BlockWriteWordCount = 2,
                    TargetTag = new TargetTag
                    {
                        MemoryBank = MemoryBank.Epc,
                        BitPointer = BitPointers.Epc,
                        Data = tag.Epc.ToHexString()
                    }
                };

                seq.Ops.Add(new TagWriteOp
                {
                    AccessPassword = TagData.FromHexString(newAccessPassword),
                    MemoryBank = MemoryBank.Epc,
                    WordPointer = WordPointers.Epc,
                    Data = TagData.FromHexString(newEpc)
                });


                // Create a lock operation
                seq.Ops.Add(new TagLockOp
                {
                    AccessPasswordLockType = TagLockState.Permalock,
                    EpcLockType = TagLockState.Permalock,
                });

                TagOpController.RecordExpectedEpc(tidHex, newEpc);
                sw.Restart();
                reader.AddOpSequence(seq);
            }
        }

        private void OnTagOpComplete(ImpinjReader sender, TagOpReport report)
        {
            if (report == null || IsCancellationRequested()) return;

            foreach (var result in report)
            {
                if (result is TagWriteOpResult writeResult)
                {
                    sw.Stop();
                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string oldEpc = writeResult.Tag.Epc.ToHexString();
                    string newEpc = TagOpController.GetExpectedEpc(tidHex);
                    string res = writeResult.Result.ToString();
                    double rssi = writeResult.Tag.IsPeakRssiInDbmPresent ? writeResult.Tag.PeakRssiInDbm : 0;
                    ushort antennaPort = writeResult.Tag.IsAntennaPortNumberPresent ? writeResult.Tag.AntennaPortNumber : (ushort)0;

                    sw.Stop();
                    long writeTime = sw.ElapsedMilliseconds;

                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{serialCounter++},{writeTime},{res},{rssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, res);

                    Console.WriteLine($"Write complete for TID={tidHex}: Result={res}, Time={writeTime}ms");
                }
            }
        }
    }
}
