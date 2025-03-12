using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace OctaneTagWritingTest.TestStrategy
{
    /// <summary>
    /// Optimized strategy for high-performance manufacturing line with robust inline write, verification, and error handling.
    /// </summary>
    public class TestCase10OptimizedStrategy : BaseTestStrategy
    {
        private readonly Stopwatch swWrite = new Stopwatch();
        private readonly Stopwatch swVerify = new Stopwatch();
        private readonly Dictionary<string, int> retryCount = new Dictionary<string, int>();
        private const int maxRetries = 3;

        public TestCase10OptimizedStrategy(string hostname, string logFile)
            : base(hostname, logFile)
        { }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("Starting optimized inline write, permalock, and verification strategy...");
                ConfigureReader();

                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,OldEPC,NewEPC,WriteTime,VerifyTime,Result,Retries,RSSI,AntennaPort");

                while (!IsCancellationRequested())
                    Thread.Sleep(100);

                Console.WriteLine("\nStopping test...");
            }
            finally
            {
                CleanupReader();
            }
        }

        private void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            if (report == null || IsCancellationRequested()) return;

            foreach (Tag tag in report.Tags)
            {
                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (string.IsNullOrEmpty(tidHex) || TagOpController.HasResult(tidHex))
                    continue;

                ExecuteWriteAndVerify(tag);
            }
        }

        private void ExecuteWriteAndVerify(Tag tag)
        {
            string tidHex = tag.Tid.ToHexString();
            string oldEpc = tag.Epc.ToHexString();
            string newEpc = TagOpController.GetNextEpcForTag();

            TagOpSequence seq = new TagOpSequence
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

            // Update Access password
            seq.Ops.Add(new TagWriteOp
            {
                AccessPassword = null,
                MemoryBank = MemoryBank.Reserved,
                WordPointer = WordPointers.AccessPassword,
                Data = TagData.FromHexString(newAccessPassword)
            });

            // Write new EPC
            seq.Ops.Add(new TagWriteOp
            {
                AccessPassword = TagData.FromHexString(newAccessPassword),
                MemoryBank = MemoryBank.Epc,
                WordPointer = WordPointers.Epc,
                Data = TagData.FromHexString(TagOpController.GetNextEpcForTag())
            });

            // Permalock operation
            seq.Ops.Add(new TagLockOp
            {
                AccessPasswordLockType = TagLockState.Permalock,
                EpcLockType = TagLockState.Permalock
            });

            swWrite.Restart();
            reader.AddOpSequence(seq);
            TagOpController.RecordExpectedEpc(tidHex, TagOpController.GetNextEpcForTag());
        }

        private void OnTagOpComplete(ImpinjReader sender, TagOpReport report)
        {
            if (report == null || IsCancellationRequested()) return;

            foreach (TagOpResult result in report)
            {
                if (result is TagWriteOpResult writeResult)
                {
                    swWrite.Stop();
                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string expectedEpc = TagOpController.GetExpectedEpc(tidHex);

                    if (writeResult.Result == WriteResultStatus.Success)
                    {
                        TriggerVerificationRead(writeResult.Tag);
                    }
                    else
                    {
                        LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{writeResult.Tag.Epc.ToHexString()},{expectedEpc},N/A,{swWrite.ElapsedMilliseconds},0,Write Failure,0,0,0");
                        TagOpController.RecordResult(tidHex, "Write Error");
                    }
                }
                else if (result is TagReadOpResult readResult)
                {
                    swVerify.Stop();
                    string tidHex = readResult.Tag.Tid.ToHexString();
                    string verifiedEpc = readResult.Data.ToHexWordString();
                    string expectedEpc = TagOpController.GetExpectedEpc(tidHex);
                    string resultStatus = verifiedEpc.Equals(expectedEpc) ? "Success" : "Verification Failure";

                    LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{readResult.Tag.Epc.ToHexString()},{expectedEpc},{swWrite.ElapsedMilliseconds},{swVerify.ElapsedMilliseconds},{resultStatus},0,{readResult.Tag.PeakRssiInDbm},{readResult.Tag.AntennaPortNumber}");
                    TagOpController.RecordResult(tidHex, resultStatus);
                }
            }
        }

        private void TriggerVerificationRead(Tag tag)
        {
            TagOpSequence seq = new TagOpSequence();
            TagReadOp readOp = new TagReadOp
            {
                AccessPassword = TagData.FromHexString(newAccessPassword),
                MemoryBank = MemoryBank.Epc,
                WordPointer = WordPointers.Epc,
                WordCount = 6 // 96 bits EPC = 6 words
            };

            seq.Ops.Add(readOp);
            swVerify.Restart();
            reader.AddOpSequence(seq);
        }
    }
}