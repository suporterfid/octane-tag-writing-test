﻿using System;
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

                Console.WriteLine("Starting robustness test (write with retries using 2 antennas)...");

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

        /// <summary>
        /// Configures the reader including connection, settings, and EPC list loading
        /// </summary>
        /// <returns>The configured reader settings</returns>
        /// <remarks>
        /// This method:
        /// - Loads the predefined EPC list
        /// - Connects to the reader
        /// - Applies default settings
        /// - Enables FastId and Individual reporting mode
        /// - Enables low latency reporting
        /// </remarks>
        protected virtual Settings ConfigureReader()
        {
            EpcListManager.LoadEpcList("epc_list.txt");

            reader.Connect(settings.Hostname);
            reader.ApplyDefaultSettings();

            Settings readerSettings = reader.QueryDefaultSettings();
            readerSettings.Report.IncludeFastId = settings.IncludeFastId;
            readerSettings.Report.IncludePeakRssi = settings.IncludePeakRssi;
            readerSettings.Report.IncludeAntennaPortNumber = settings.IncludeAntennaPortNumber;
            readerSettings.Report.Mode = (ReportMode)Enum.Parse(typeof(ReportMode), settings.ReportMode);
            readerSettings.RfMode = (uint)settings.RfMode;

            readerSettings.Antennas.DisableAll();
            readerSettings.Antennas.GetAntenna(1).IsEnabled = true;
            readerSettings.Antennas.GetAntenna(1).TxPowerInDbm = settings.TxPowerInDbm;
            readerSettings.Antennas.GetAntenna(1).MaxRxSensitivity = settings.MaxRxSensitivity;
            readerSettings.Antennas.GetAntenna(1).RxSensitivityInDbm = settings.RxSensitivityInDbm;

            readerSettings.Antennas.GetAntenna(2).IsEnabled = true;
            readerSettings.Antennas.GetAntenna(2).TxPowerInDbm = 33.0;
            readerSettings.Antennas.GetAntenna(2).MaxRxSensitivity = true;
            readerSettings.Antennas.GetAntenna(2).RxSensitivityInDbm = settings.RxSensitivityInDbm;

            readerSettings.SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), settings.SearchMode);
            readerSettings.Session = (ushort)settings.Session;

            readerSettings.Filters.TagFilter1.MemoryBank = (MemoryBank)Enum.Parse(typeof(MemoryBank), settings.MemoryBank);
            readerSettings.Filters.TagFilter1.BitPointer = (ushort)settings.BitPointer;
            readerSettings.Filters.TagFilter1.TagMask = settings.TagMask;
            readerSettings.Filters.TagFilter1.BitCount = settings.BitCount;
            readerSettings.Filters.TagFilter1.FilterOp = (TagFilterOp)Enum.Parse(typeof(TagFilterOp), settings.FilterOp);
            readerSettings.Filters.Mode = (TagFilterMode)Enum.Parse(typeof(TagFilterMode), settings.FilterMode);

            EnableLowLatencyReporting(readerSettings);
            reader.ApplySettings(readerSettings);

            return readerSettings;
        }

        private void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            if (report == null || IsCancellationRequested()) return;



            foreach (Tag tag in report.Tags)
            {
                if (IsCancellationRequested()) return;

                // Antenna 2 should only be used for writing
                if (tag.AntennaPortNumber == 2) continue;

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

                if (string.IsNullOrEmpty(expectedEpc))
                {
                    string newEpcToWrite = TagOpController.Instance.GetNextEpcForTag();
                    TagOpController.Instance.RecordExpectedEpc(tidHex, newEpcToWrite);
                    Console.WriteLine($"New target TID: {tidHex}, writing new EPC: {newEpcToWrite}");

                    writeTimers[tidHex] = Stopwatch.StartNew();
                    TagOpController.Instance.TriggerWriteAndVerify(tag, newEpcToWrite, reader, cancellationToken, writeTimers[tidHex], newAccessPassword, true, 2, true, 0);
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
                    Console.WriteLine($"Success TID: {tidHex} new EPC: {result.Tag.Epc.ToHexString()} - Success count: {TagOpController.Instance.GetSuccessCount()}");
                }
            }
        }
    }
}
