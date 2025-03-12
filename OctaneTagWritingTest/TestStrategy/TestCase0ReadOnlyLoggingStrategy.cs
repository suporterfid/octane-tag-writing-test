using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace OctaneTagWritingTest.TestStrategy
{
    public class TestCase0ReadOnlyLoggingStrategy : BaseTestStrategy
    {
        private readonly Dictionary<string, int> tagReadCounts = new();

        public TestCase0ReadOnlyLoggingStrategy(string hostname, string logFile)
            : base(hostname, logFile)
        {
        }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;

                Console.WriteLine("Starting Read Logging Test Strategy...");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                ConfigureReader();

                reader.TagsReported += OnTagsReported;
                reader.Start();

                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,EPC,ReadCount,RSSI,AntennaPort");

                while (!IsCancellationRequested())
                    Thread.Sleep(100);

                Console.WriteLine("\nStopping test...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during reading test: " + ex.Message);
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
                string tidHex = tag.Tid?.ToHexString() ?? "N/A";
                string epcHex = tag.Epc.ToHexString();

                if (!tagReadCounts.ContainsKey(tidHex))
                    tagReadCounts[tidHex] = 0;

                tagReadCounts[tidHex]++;

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                double rssi = tag.PeakRssiInDbm;
                ushort antennaPort = tag.AntennaPortNumber;

                Console.WriteLine($"Read tag TID={tidHex}, EPC={epcHex}, Count={tagReadCounts[tidHex]}");

                LogToCsv($"{timestamp},{tidHex},{epcHex},{tagReadCounts[tidHex]},{rssi},{antennaPort}");
            }
        }

        private void LogToCsv(string data)
        {
            lock (this)
            {
                File.AppendAllText(logFile, data + Environment.NewLine);
            }
        }

        protected override Settings ConfigureReader()
        {
            reader.Connect(hostname);
            Settings settings = reader.QueryDefaultSettings();

            settings.Report.IncludeAntennaPortNumber = true;
            settings.Report.IncludePeakRssi = true;
            settings.Report.IncludeFastId = true;
            settings.Report.Mode = ReportMode.Individual;
            settings.RfMode = 2;
            settings.SearchMode = SearchMode.SingleTarget;
            settings.Session = 1;
            settings.TagPopulationEstimate = 32;

            settings.Antennas.DisableAll();
            settings.Antennas.GetAntenna(1).IsEnabled = true;
            settings.Antennas.GetAntenna(1).TxPowerInDbm = 30;
            settings.Antennas.GetAntenna(1).MaxRxSensitivity = true;

            EnableLowLatencyReporting(settings);
            reader.ApplySettings(settings);

            return settings;
        }
    }
}
