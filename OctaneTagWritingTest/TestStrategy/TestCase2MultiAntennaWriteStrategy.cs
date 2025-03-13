using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;

namespace OctaneTagWritingTest.TestStrategy
{
    public class TestCase2MultiAntennaWriteStrategy : BaseTestStrategy
    {
        private readonly ConcurrentDictionary<string, Stopwatch> writeTimers = new ConcurrentDictionary<string, Stopwatch>();

        public TestCase2MultiAntennaWriteStrategy(string hostname, string logFile, ReaderSettings readerSettings)
            : base(hostname, logFile, readerSettings)
        {
        }

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
                    TagOpController.Instance.LogToCsv(logFile, "Timestamp,TID,OldEPC,NewEPC,WriteTime,Result,RSSI,AntennaPort");

                while (!IsCancellationRequested())
                    Thread.Sleep(100);

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

        protected override Settings ConfigureReader()
        {
            EpcListManager.LoadEpcList("epc_list.txt");

            reader.Connect(hostname);
            reader.ApplyDefaultSettings();

            Settings settings = reader.QueryDefaultSettings();
            settings.Report.IncludeFastId = true;
            settings.Report.IncludePeakRssi = true;
            settings.Report.IncludeAntennaPortNumber = true;
            settings.Report.Mode = ReportMode.Individual;

            settings.Antennas.DisableAll();
            for (ushort port = 1; port <= 2; port++)
            {
                settings.Antennas.GetAntenna(port).IsEnabled = true;
                settings.Antennas.GetAntenna(port).TxPowerInDbm = 30;
                settings.Antennas.GetAntenna(port).MaxRxSensitivity = true;
            }

            settings.RfMode = 1111;
            settings.SearchMode = SearchMode.DualTarget;
            settings.Session = 2;
            EnableLowLatencyReporting(settings);
            reader.ApplySettings(settings);

            return settings;
        }

        private void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            if (report == null || IsCancellationRequested()) return;

            foreach (Tag tag in report.Tags)
            {
                if (IsCancellationRequested()) return;

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;

                if (TagOpController.Instance.IsTidProcessed(tidHex))
                {
                    Console.WriteLine($"Skipping tag {tidHex}, EPC already assigned.");
                    continue;
                }

                string currentEpc = tag.Epc.ToHexString();
                string expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);

                if (!string.IsNullOrEmpty(expectedEpc) && expectedEpc.Equals(currentEpc, StringComparison.OrdinalIgnoreCase))
                {
                    TagOpController.Instance.RecordResult(tidHex, currentEpc, true);
                    continue;
                }

                if (!TagOpController.Instance.IsLocalTargetTidSet || !tidHex.Equals(TagOpController.Instance.LocalTargetTid))
                {
                    string newEpcToWrite = TagOpController.Instance.GetNextEpcForTag();
                    TagOpController.Instance.RecordExpectedEpc(tidHex, newEpcToWrite);
                    Console.WriteLine($"New target TID: {tidHex}, writing new EPC: {newEpcToWrite}");

                    writeTimers[tidHex] = Stopwatch.StartNew();
                    TagOpController.Instance.TriggerWriteAndVerify(tag, newEpcToWrite, reader, cancellationToken, writeTimers[tidHex], newAccessPassword, true);
                }
            }
        }

        private void OnTagOpComplete(ImpinjReader reader, TagOpReport report)
        {
            if (report == null || IsCancellationRequested()) return;

            foreach (TagOpResult result in report)
            {
                if (result is TagWriteOpResult writeResult)
                {
                    writeTimers.TryGetValue(writeResult.Tag.Tid.ToHexString(), out Stopwatch writeTimer);
                    writeTimer?.Stop();

                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string oldEpc = writeResult.Tag.Epc.ToHexString();
                    string newEpc = TagOpController.Instance.GetExpectedEpc(tidHex);
                    string res = writeResult.Result.ToString();
                    double resultRssi = writeResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = writeResult.Tag.AntennaPortNumber;

                    bool wasSuccess = res == "Success";
                    TagOpController.Instance.LogToCsv(logFile, $"{timestamp},{tidHex},{oldEpc},{newEpc},{writeTimer?.ElapsedMilliseconds},{res},{resultRssi},{antennaPort}");
                    TagOpController.Instance.RecordResult(tidHex, res, wasSuccess);
                }
            }
        }
    }
}
