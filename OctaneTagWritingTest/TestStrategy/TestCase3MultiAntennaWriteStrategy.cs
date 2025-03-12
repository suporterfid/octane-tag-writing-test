using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using System;
using System.IO;
using System.Threading;

namespace OctaneTagWritingTest.TestStrategy
{
    public class TestCase3MultiAntennaWriteStrategy : BaseTestStrategy
    {
        public TestCase3MultiAntennaWriteStrategy(string hostname, string logFile)
            : base(hostname, logFile)
        {
        }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;

                if (reader == null)
                    reader = new ImpinjReader();

                ConfigureReader();

                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,OldEPC,NewEPC,WriteTime,Result,RSSI,AntennaPort");

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

            settings.RfMode = 0;
            settings.SearchMode = SearchMode.DualTarget;
            settings.Session = 2;
            EnableLowLatencyReporting(settings);
            reader.ApplySettings(settings);

            return settings;
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
                    Console.WriteLine($"Skipping tag {tidHex}, EPC already assigned.");
                    continue;
                }

                string expectedEpc = TagOpController.GetExpectedEpc(tidHex);
                string currentEpc = tag.Epc.ToHexString();

                if (!string.IsNullOrEmpty(expectedEpc) && expectedEpc.Equals(currentEpc, StringComparison.OrdinalIgnoreCase))
                {
                    TagOpController.RecordResult(tidHex, currentEpc);
                    continue;
                }

                if (!isTargetTidSet || !tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"\nNew target TID found: {tidHex}");

                    string newEpcToWrite = TagOpController.GetNextEpcForTag();
                    TagOpController.RecordExpectedEpc(tidHex, newEpcToWrite);
                    TriggerWrite(tag, newEpcToWrite);
                }
            }
        }

        private void TriggerWrite(Tag tag, string newEpcToWrite)
        {
            TagOpSequence seq = new TagOpSequence
            {
                BlockWriteEnabled = true,
                BlockWriteWordCount = 2,
                TargetTag = {
                    MemoryBank = MemoryBank.Epc,
                    BitPointer = BitPointers.Epc,
                    Data = tag.Epc.ToHexString()
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
                    string res = writeResult.Result.ToString();
                    double resultRssi = writeResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = writeResult.Tag.AntennaPortNumber;

                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{sw.ElapsedMilliseconds},{res},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, res);
                    isTargetTidSet = false;
                }
            }
        }
    }
}
