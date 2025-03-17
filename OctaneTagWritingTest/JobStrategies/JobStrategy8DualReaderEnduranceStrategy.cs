using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using Org.LLRP.LTK.LLRPV1.Impinj;

namespace OctaneTagWritingTest.JobStrategies
{
    /// <summary>
    /// Dual-reader endurance strategy.
    /// The first reader (writerReader) reads tags, generates or retrieves a new EPC for the read TID,
    /// and sends a write operation. The second reader (verifierReader) monitors tag reads,
    /// comparing each tag’s EPC to its expected value; if a mismatch is found, it triggers a re-write
    /// using the expected EPC.
    /// </summary>
    public class JobStrategy8DualReaderEnduranceStrategy : BaseTestStrategy
    {
        private const int MaxCycles = 10000;
        private readonly ConcurrentDictionary<string, int> cycleCount = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, Stopwatch> swWriteTimers = new ConcurrentDictionary<string, Stopwatch>();
        private readonly ConcurrentDictionary<string, Stopwatch> swVerifyTimers = new ConcurrentDictionary<string, Stopwatch>();

        // Two separate readers: one for writing and one for verifying.
        private ImpinjReader writerReader;
        private ImpinjReader verifierReader;
        private string writerAddress;
        private string verifierAddress;

        public JobStrategy8DualReaderEnduranceStrategy(string hostnameWriter, string hostnameVerifier, string logFile, ReaderSettings readerSettings)
            : base(hostnameWriter, logFile, readerSettings)
        {
            writerAddress = hostnameWriter;
            verifierAddress = hostnameVerifier;
            // Create two separate reader instances.
            writerReader = new ImpinjReader();
            verifierReader = new ImpinjReader();

            // Clean up any previous tag operation state.
            TagOpController.Instance.CleanUp();
        }

        public override void RunJob(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("=== Dual Reader Endurance Test ===");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                // Configure both readers.
                ConfigureWriterReader();
                ConfigureVerifierReader();

                // Register event handlers for the writer reader.
                writerReader.TagsReported += OnTagsReportedWriter;
                writerReader.TagOpComplete += OnTagOpComplete;

                // Register event handlers for the verifier reader.
                verifierReader.TagsReported += OnTagsReportedVerifier;
                verifierReader.TagOpComplete += OnTagOpComplete;

                // Start both readers.
                writerReader.Start();
                verifierReader.Start();

                // Create CSV header if the log file does not exist.
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
                Console.WriteLine("Error in dual reader endurance test: " + ex.Message);
            }
            finally
            {
                CleanupReaders();
            }
        }

        private void ConfigureWriterReader()
        {
            // Load the predefined EPC list.
            EpcListManager.Instance.LoadEpcList("epc_list.txt");

            writerReader.Connect(writerAddress);
            writerReader.ApplyDefaultSettings();

            var writerSettings = writerReader.QueryDefaultSettings();
            writerSettings.Report.IncludeFastId = settings.IncludeFastId;
            writerSettings.Report.IncludePeakRssi = settings.IncludePeakRssi;
            writerSettings.Report.IncludePcBits = true;
            writerSettings.Report.IncludeAntennaPortNumber = settings.IncludeAntennaPortNumber;
            writerSettings.Report.Mode = (ReportMode)Enum.Parse(typeof(ReportMode), settings.ReportMode);
            writerSettings.RfMode = (uint)settings.RfMode;

            writerSettings.Antennas.DisableAll();
            writerSettings.Antennas.GetAntenna((ushort)settings.AntennaPort).IsEnabled = true;
            writerSettings.Antennas.GetAntenna((ushort)settings.AntennaPort).TxPowerInDbm = settings.TxPowerInDbm;
            writerSettings.Antennas.GetAntenna((ushort)settings.AntennaPort).MaxRxSensitivity = settings.MaxRxSensitivity;
            writerSettings.Antennas.GetAntenna((ushort)settings.AntennaPort).RxSensitivityInDbm = settings.RxSensitivityInDbm;

            writerSettings.SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), settings.SearchMode);
            writerSettings.Session = (ushort)settings.Session;

            EnableLowLatencyReporting(writerSettings, writerReader);
            writerReader.ApplySettings(writerSettings);
        }

        private void ConfigureVerifierReader()
        {
            verifierReader.Connect(verifierAddress);
            verifierReader.ApplyDefaultSettings();

            var verifierSettings = verifierReader.QueryDefaultSettings();
            verifierSettings.Report.IncludeFastId = settings.IncludeFastId;
            verifierSettings.Report.IncludePeakRssi = settings.IncludePeakRssi;
            verifierSettings.Report.IncludePcBits = true;
            verifierSettings.Report.IncludeAntennaPortNumber = settings.IncludeAntennaPortNumber;
            verifierSettings.Report.Mode = (ReportMode)Enum.Parse(typeof(ReportMode), settings.ReportMode);
            verifierSettings.RfMode = (uint)settings.RfMode;

            verifierSettings.Antennas.DisableAll();
            // Use a different antenna port for the verifier (e.g., port 2).
            verifierSettings.Antennas.GetAntenna(2).IsEnabled = true;
            verifierSettings.Antennas.GetAntenna(2).TxPowerInDbm = settings.TxPowerInDbm;
            verifierSettings.Antennas.GetAntenna(2).MaxRxSensitivity = settings.MaxRxSensitivity;
            verifierSettings.Antennas.GetAntenna(2).RxSensitivityInDbm = settings.RxSensitivityInDbm;

            verifierSettings.SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), settings.SearchMode);
            verifierSettings.Session = (ushort)settings.Session;

            EnableLowLatencyReporting(verifierSettings, verifierReader);
            verifierReader.ApplySettings(verifierSettings);
        }

        private void EnableLowLatencyReporting(Settings settings, ImpinjReader reader)
        {
            var addRoSpecMessage = reader.BuildAddROSpecMessage(settings);
            var setReaderConfigMessage = reader.BuildSetReaderConfigMessage(settings);
            setReaderConfigMessage.AddCustomParameter(new PARAM_ImpinjReportBufferConfiguration()
            {
                ReportBufferMode = ENUM_ImpinjReportBufferMode.Low_Latency
            });
            reader.ApplySettings(setReaderConfigMessage, addRoSpecMessage);
        }

        private void CleanupReaders()
        {
            try
            {
                if (writerReader != null)
                {
                    writerReader.Stop();
                    writerReader.Disconnect();
                }
                if (verifierReader != null)
                {
                    verifierReader.Stop();
                    verifierReader.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during reader cleanup: " + ex.Message);
            }
        }

        /// <summary>
        /// Event handler for tag reports from the writer reader.
        /// Captures the EPC and TID, generates or retrieves a new EPC for the TID, and sends the write operation.
        /// </summary>
        private void OnTagsReportedWriter(ImpinjReader sender, TagReport report)
        {
            if (report == null || IsCancellationRequested())
                return;

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

                // If no expected EPC exists, generate one using the writer logic.
                if (string.IsNullOrEmpty(expectedEpc))
                {
                    Console.WriteLine($"New target TID found: {tidHex} Chip {TagOpController.Instance.GetChipModel(tag)}");
                    expectedEpc = TagOpController.Instance.GetNextEpcForTag();
                    TagOpController.Instance.RecordExpectedEpc(tidHex, expectedEpc);
                }

                // Trigger the write operation using the writer reader.
                TagOpController.Instance.TriggerWriteAndVerify(
                    tag,
                    expectedEpc,
                    writerReader,
                    cancellationToken,
                    swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()),
                    newAccessPassword,
                    true);
            }
        }

        /// <summary>
        /// Event handler for tag reports from the verifier reader.
        /// Compares the tag's EPC with the expected EPC; if they do not match, triggers a write operation retry using the expected EPC.
        /// </summary>
        private void OnTagsReportedVerifier(ImpinjReader sender, TagReport report)
        {
            if (report == null || IsCancellationRequested())
                return;

            foreach (var tag in report.Tags)
            {
                var tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (string.IsNullOrEmpty(tidHex))
                    continue;

                var expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);
                if (!string.IsNullOrEmpty(expectedEpc))
                {
                    var currentEpc = tag.Epc.ToHexString();
                    if (!expectedEpc.Equals(currentEpc, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Verification mismatch for TID {tidHex}: expected {expectedEpc}, read {currentEpc}. Retrying write operation using expected EPC.");
                        // Retry writing using the expected EPC (without generating a new one) via the verifier reader.
                        TagOpController.Instance.TriggerWriteAndVerify(
                            tag,
                            expectedEpc,
                            verifierReader,
                            cancellationToken,
                            swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()),
                            newAccessPassword,
                            true);
                    }
                    else
                    {
                        Console.WriteLine($"TID {tidHex} verified successfully on verifier reader.");
                    }
                }
            }
        }

        /// <summary>
        /// Common event handler for tag operation completions from both readers.
        /// Processes write and read operations, logs the result, and updates the cycle count.
        /// </summary>
        private void OnTagOpComplete(ImpinjReader sender, TagOpReport report)
        {
            if (report == null || IsCancellationRequested())
                return;

            foreach (TagOpResult result in report)
            {
                var tidHex = result.Tag.Tid?.ToHexString() ?? "N/A";

                if (result is TagWriteOpResult writeResult)
                {
                    swWriteTimers[tidHex].Stop();

                    if (writeResult.Result == WriteResultStatus.Success)
                    {
                        Console.WriteLine($"Write operation succeeded for TID {tidHex} on reader {sender.Name}.");
                        // After a successful write, trigger a verification read on the verifier reader.
                        TagOpController.Instance.TriggerVerificationRead(
                            result.Tag,
                            verifierReader,
                            cancellationToken,
                            swVerifyTimers.GetOrAdd(tidHex, _ => new Stopwatch()),
                            newAccessPassword);
                    }
                    else
                    {
                        Console.WriteLine($"Write operation failed for TID {tidHex} on reader {sender.Name}.");
                    }
                }
                else if (result is TagReadOpResult readResult)
                {
                    swVerifyTimers[tidHex].Stop();

                    var expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);
                    var verifiedEpc = readResult.Tag.Epc?.ToHexString() ?? "N/A";
                    var success = verifiedEpc.Equals(expectedEpc, StringComparison.OrdinalIgnoreCase);
                    var status = success ? "Success" : "Failure";

                    LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{result.Tag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{swWriteTimers[tidHex].ElapsedMilliseconds},{swVerifyTimers[tidHex].ElapsedMilliseconds},{status},{cycleCount.GetOrAdd(tidHex, 0)},RSSI,AntennaPort");
                    TagOpController.Instance.RecordResult(tidHex, status, success);

                    Console.WriteLine($"Verification result for TID {tidHex} on reader {sender.Name}: {status}");

                    cycleCount.AddOrUpdate(tidHex, 1, (key, oldValue) => oldValue + 1);
                }
            }
        }

        /// <summary>
        /// Appends a line to the CSV log file.
        /// </summary>
        /// <param name="line">The CSV line to append.</param>
        private void LogToCsv(string line)
        {
            TagOpController.Instance.LogToCsv(logFile, line);
        }
    }
}
