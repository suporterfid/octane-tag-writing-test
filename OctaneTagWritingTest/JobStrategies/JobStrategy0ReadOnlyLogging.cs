using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Serilog;

namespace OctaneTagWritingTest.JobStrategies
{
    public class JobStrategy0ReadOnlyLogging : BaseTestStrategy
    {
        private static readonly ILogger Logger = LoggingConfiguration.CreateStrategyLogger("ReadOnlyLogging");
        private readonly Dictionary<string, int> tagReadCounts = new();

        public JobStrategy0ReadOnlyLogging(string hostname, string logFile, Dictionary<string, ReaderSettings> readerSettings)
            : base(hostname, logFile, readerSettings)
        {
            TagOpController.Instance.CleanUp();
        }

        public override void RunJob(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                LogFlowStart();

                Logger.Information("Starting Read Logging Test Strategy");
                Logger.Information("Press 'q' to stop the test and return to menu");

                ConfigureReader();

                reader.TagsReported += OnTagsReported;
                reader.Start();
                LogFlowRun();

                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,EPC,ReadCount,RSSI,AntennaPort");

                while (!IsCancellationRequested())
                    Thread.Sleep(100);

                Logger.Information("Stopping read logging test");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during reading test: {ErrorMessage}", ex.Message);
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

                Logger.Information("Read tag TID={TID}, EPC={EPC}, Count={ReadCount}, RSSI={RSSI}, Antenna={AntennaPort}", 
                    tidHex, epcHex, tagReadCounts[tidHex], rssi, antennaPort);

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

        
    }
}



