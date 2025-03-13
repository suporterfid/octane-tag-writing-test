using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;

namespace OctaneTagWritingTest.TestStrategy
{
    public class TestCase5EnduranceStrategy : BaseTestStrategy
    {
        private const int MaxCycles = 10000;
        private readonly ConcurrentDictionary<string, int> cycleCount = new();
        private readonly ConcurrentDictionary<string, Stopwatch> swWriteTimers = new();
        private readonly ConcurrentDictionary<string, Stopwatch> swVerifyTimers = new();

        public TestCase5EnduranceStrategy(string hostname, string logFile, ReaderSettings readerSettings) : base(hostname, logFile, readerSettings)
        {
        }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("=== Endurance Test ===");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                ConfigureReader();

                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;

                reader.Start();

                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,Previous_EPC,Expected_EPC,Verified_EPC,WriteTime_ms,VerifyTime_ms,Result,CycleCount,RSSI,AntennaPort");

                while (!IsCancellationRequested())
                {
                    Thread.Sleep(100);
                }

                Console.WriteLine("\nStopping test...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in endurance test: " + ex.Message);
            }
            finally
            {
                CleanupReader();
            }
        }

        private void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            if (report == null || IsCancellationRequested()) return;

            foreach (var tag in report.Tags)
            {
                var tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (string.IsNullOrEmpty(tidHex) || TagOpController.Instance.IsTidProcessed(tidHex))
                    continue;

                cycleCount.TryAdd(tidHex, 0);

                if (cycleCount[tidHex] >= MaxCycles)
                {
                    Console.WriteLine($"Max cycles reached for TID {tidHex}, skipping further processing.");
                    continue;
                }

                var currentEpc = tag.Epc.ToHexString();
                var expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);

                if (expectedEpc != null && expectedEpc.Equals(currentEpc, StringComparison.OrdinalIgnoreCase))
                {
                    TagOpController.Instance.HandleVerifiedTag(tag, tidHex, expectedEpc, swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()), swVerifyTimers.GetOrAdd(tidHex, _ => new Stopwatch()), cycleCount, tag, TagOpController.Instance.GetChipModel(tag), logFile);
                    continue;
                }

                if (string.IsNullOrEmpty(expectedEpc))
                {
                    expectedEpc = TagOpController.Instance.GetNextEpcForTag();
                    TagOpController.Instance.RecordExpectedEpc(tidHex, expectedEpc);
                }

                TagOpController.Instance.TriggerWriteAndVerify(tag, expectedEpc, reader, cancellationToken, swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()), newAccessPassword, true);
            }
        }

        private void OnTagOpComplete(ImpinjReader sender, TagOpReport report)
        {
            if (report == null || IsCancellationRequested()) return;

            foreach (TagOpResult result in report)
            {
                var tidHex = result.Tag.Tid?.ToHexString() ?? "N/A";

                if (result is TagWriteOpResult writeResult)
                {
                    swWriteTimers[tidHex].Stop();

                    if (writeResult.Result == WriteResultStatus.Success)
                    {
                        TagOpController.Instance.TriggerVerificationRead(result.Tag, reader, cancellationToken, swVerifyTimers.GetOrAdd(tidHex, _ => new Stopwatch()), newAccessPassword);
                    }
                    else
                    {
                        LogFailure(tidHex, "Write failure", result.Tag);
                    }
                }
                else if (result is TagReadOpResult readResult)
                {
                    swVerifyTimers[tidHex].Stop();

                    var expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);
                    var verifiedEpc = readResult.Data?.ToHexString() ?? "N/A";
                    var success = verifiedEpc.Equals(expectedEpc, StringComparison.OrdinalIgnoreCase);
                    var status = success ? "Success" : "Failure";

                    LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{result.Tag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{swWriteTimers[tidHex].ElapsedMilliseconds},{swVerifyTimers[tidHex].ElapsedMilliseconds},{status},{cycleCount[tidHex]},RSSI,AntennaPort");
                    TagOpController.Instance.RecordResult(tidHex, status, success);

                    cycleCount[tidHex]++;
                }
            }
        }

        private void LogFailure(string tidHex, string reason, Tag tag)
        {
            LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{tag.Epc.ToHexString()},N/A,N/A,{swWriteTimers[tidHex].ElapsedMilliseconds},0,{reason},{cycleCount[tidHex]},RSSI,AntennaPort");
            TagOpController.Instance.RecordResult(tidHex, reason, false);
        }

        private void LogToCsv(string logLine)
        {
            TagOpController.Instance.LogToCsv(logFile, logLine);
        }
    }
}