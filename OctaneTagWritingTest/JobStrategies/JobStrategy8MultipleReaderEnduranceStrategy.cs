using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    public class JobStrategy8MultipleReaderEnduranceStrategy : BaseTestStrategy
    {
        private readonly ApplicationConfig applicationConfig;

        private const int MaxCycles = 10000;
        private readonly ConcurrentDictionary<string, int> cycleCount = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, Stopwatch> swWriteTimers = new ConcurrentDictionary<string, Stopwatch>();
        private readonly ConcurrentDictionary<string, Stopwatch> swVerifyTimers = new ConcurrentDictionary<string, Stopwatch>();

        private ImpinjReader detectorReader;
        // Two separate readers: one for writing and one for verifying.
        private ImpinjReader writerReader;
        private ImpinjReader verifierReader;
        private string detectorAddress;
        private string writerAddress;
        private string verifierAddress;

        private Timer successCountTimer;

        // Flag to indicate if the GPI processing is already running.
        private int gpiProcessingFlag = 0;
        private bool useGpiForVerification = true;
        private bool gpiTriggerStateToProccessVerification = false;
        // Separate dictionary for capturing tags during the verification phase.
        private static ConcurrentDictionary<string, Tag> verificationTags = new ConcurrentDictionary<string, Tag>();

        public JobStrategy8MultipleReaderEnduranceStrategy(
        string hostnameDetector,
        string hostnameWriter,
        string hostnameVerifier,
        string logFile,
        Dictionary<string, ReaderSettings> readerSettings,
        ApplicationConfig appConfig)  // NOVO PARÂMETRO
        : base(hostnameWriter, logFile, readerSettings)
        {
            detectorAddress = hostnameDetector;
            writerAddress = hostnameWriter;
            verifierAddress = hostnameVerifier;

            // Armazenar a configuração da aplicação
            applicationConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));

            // CONFIGURAR as variáveis GPI a partir do ApplicationConfig
            useGpiForVerification = appConfig.UseGpiForVerification;
            gpiTriggerStateToProccessVerification = appConfig.GpiTriggerStateToProcessVerification;


            detectorReader = new ImpinjReader();
            writerReader = new ImpinjReader();
            verifierReader = new ImpinjReader();

            // Clean up any previous tag operation state.
            TagOpController.Instance.CleanUp();

            // Create a timer that will display the progress every 10 seconds.
            successCountTimer = new Timer(DisplayProgress, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }

        private ApplicationConfig GetApplicationConfig()
        {
            return applicationConfig;
        }

        public override void RunJob(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("=== Multiple Reader Endurance Test ===");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                // Configure readers.

                try
                {
                    ConfigureDetectorReader();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ConfigureDetectorReader - Error: " + ex.Message);
                    throw;
                }

                try
                {
                    ConfigureWriterReader();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ConfigureWriterReader - Error in dual reader endurance test: " + ex.Message);
                    throw ex;
                }
                try
                {
                    ConfigureVerifierReader();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ConfigureVerifierReader - Error in dual reader endurance test: " + ex.Message);
                    throw ex;
                }


                // Register event handlers.
                detectorReader.TagsReported += OnTagsReportedDetector;
                // Register event handlers for the writer reader.
                writerReader.TagsReported += OnTagsReportedWriter;
                writerReader.TagOpComplete += OnTagOpComplete;

                // Register event handlers for the verifier reader.
                verifierReader.TagsReported += OnTagsReportedVerifier;
                verifierReader.TagOpComplete += OnTagOpComplete;
                verifierReader.GpiChanged += OnGpiEvent;

                // Start readers.

                try
                {
                    detectorReader.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("detectorReader - Error in dual reader endurance test: " + ex.Message);
                    throw ex;
                }
                try
                {
                    writerReader.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("writerReader - Error in dual reader endurance test: " + ex.Message);
                    throw ex;
                }
                //try
                //{
                //    verifierReader.Start();
                //}
                //catch (Exception ex)
                //{
                //    Console.WriteLine("verifierReader - Error in dual reader endurance test: " + ex.Message);
                //    throw ex;
                //}
                
                
                

                // Create CSV header if the log file does not exist.
                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,Previous_EPC,Expected_EPC,Verified_EPC,WriteTime_ms,VerifyTime_ms,Result,CycleCount,RSSI,AntennaPort");

                // Initialize and start the timer to log success count every 5 seconds
                successCountTimer = new Timer(LogSuccessCount, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));


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

        private void LogSuccessCount(object state)
        {
            try
            {
                int successCount = TagOpController.Instance.GetSuccessCount();
                int totalReadCount = TagOpController.Instance.GetTotalReadCount();
                Console.WriteLine($"!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine($"!!!!!!!!!!!!!!!!!!!!!!!!!! Total Read [{totalReadCount}] Success count: [{successCount}] !!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine($"!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                try
                {
                    Console.Title = $"Serializer: {totalReadCount} - {successCount}";

                }
                catch (Exception)
                {

                   
                }
            }
            catch (Exception)
            {
            }

        }

        private void ConfigureDetectorReader()
        {
            // Load the predefined EPC list if needed.
            EpcListManager.Instance.LoadEpcList("epc_list.txt");

            detectorReader.Connect(detectorAddress);
            detectorReader.ApplyDefaultSettings();

            var detectorSettings = detectorReader.QueryDefaultSettings();
            var detectorReaderSettings = GetSettingsForRole("detector");
            detectorSettings.Report.IncludeFastId = detectorReaderSettings.IncludeFastId;
            detectorSettings.Report.IncludePeakRssi = detectorReaderSettings.IncludePeakRssi;
            detectorSettings.Report.IncludePcBits = true;
            detectorSettings.Report.IncludeAntennaPortNumber = detectorReaderSettings.IncludeAntennaPortNumber;
            detectorSettings.Report.Mode = (ReportMode)Enum.Parse(typeof(ReportMode), detectorReaderSettings.ReportMode);
            detectorSettings.RfMode = (uint)detectorReaderSettings.RfMode;

            // SUBSTITUIR configuração hard-coded das antenas pelo AntennaConfigurator
            AntennaConfigurator.ConfigureAntennasHybrid(detectorSettings, GetApplicationConfig(), "detector");

            detectorSettings.SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), detectorReaderSettings.SearchMode);
            detectorSettings.Session = (ushort)detectorReaderSettings.Session;

            EnableLowLatencyReporting(detectorSettings, detectorReader);
            detectorReader.ApplySettings(detectorSettings);
        }

        private void ConfigureWriterReader()
        {
            // Load the predefined EPC list.
            EpcListManager.Instance.LoadEpcList("epc_list.txt");

            writerReader.Connect(writerAddress);
            writerReader.ApplyDefaultSettings();

            var writerSettings = writerReader.QueryDefaultSettings();
            var writerReaderSettings = GetSettingsForRole("writer");
            writerSettings.Report.IncludeFastId = writerReaderSettings.IncludeFastId;
            writerSettings.Report.IncludePeakRssi = writerReaderSettings.IncludePeakRssi;
            writerSettings.Report.IncludePcBits = true;
            writerSettings.Report.IncludeAntennaPortNumber = writerReaderSettings.IncludeAntennaPortNumber;
            writerSettings.Report.Mode = (ReportMode)Enum.Parse(typeof(ReportMode), writerReaderSettings.ReportMode);
            writerSettings.RfMode = (uint)writerReaderSettings.RfMode;

            // SUBSTITUIR configuração hard-coded das antenas pelo AntennaConfigurator
            AntennaConfigurator.ConfigureAntennasHybrid(writerSettings, GetApplicationConfig(), "writer");

            writerSettings.SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), writerReaderSettings.SearchMode);
            writerSettings.Session = (ushort)writerReaderSettings.Session;

            EnableLowLatencyReporting(writerSettings, writerReader);
            writerReader.ApplySettings(writerSettings);
        }

        private void ConfigureVerifierReader()
        {
            verifierReader.Connect(verifierAddress);
            verifierReader.ApplyDefaultSettings();

            var verifierSettings = verifierReader.QueryDefaultSettings();
            var verifierReaderSettings = GetSettingsForRole("verifier");
            verifierSettings.Report.IncludeFastId = verifierReaderSettings.IncludeFastId;
            verifierSettings.Report.IncludePeakRssi = verifierReaderSettings.IncludePeakRssi;
            verifierSettings.Report.IncludePcBits = true;
            verifierSettings.Report.IncludeAntennaPortNumber = verifierReaderSettings.IncludeAntennaPortNumber;
            verifierSettings.Report.Mode = (ReportMode)Enum.Parse(typeof(ReportMode), verifierReaderSettings.ReportMode);
            verifierSettings.RfMode = (uint)verifierReaderSettings.RfMode;

            // SUBSTITUIR configuração hard-coded das antenas pelo AntennaConfigurator
            AntennaConfigurator.ConfigureAntennasHybrid(verifierSettings, GetApplicationConfig(), "verifier");

            verifierSettings.SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), verifierReaderSettings.SearchMode);
            verifierSettings.Session = (ushort)verifierReaderSettings.Session;

            verifierSettings.Gpis.EnableAll();
            verifierSettings.Gpis.GetGpi(1).DebounceInMs = 100;
            verifierSettings.Gpis.GetGpi(2).DebounceInMs = 100;

            verifierSettings.Gpos.GetGpo(1).Mode = GpoMode.Pulsed;
            verifierSettings.Gpos.GetGpo(1).GpoPulseDurationMsec = 500;

            verifierSettings.Gpos.GetGpo(2).Mode = GpoMode.Pulsed;
            verifierSettings.Gpos.GetGpo(2).GpoPulseDurationMsec = 500;

            verifierSettings.AutoStart.Mode = AutoStartMode.GpiTrigger;
            verifierSettings.AutoStart.GpiPortNumber = 1;
            verifierSettings.AutoStart.GpiLevel = gpiTriggerStateToProccessVerification;

            verifierSettings.AutoStop.Mode = AutoStopMode.GpiTrigger;
            verifierSettings.AutoStop.GpiPortNumber = 1;
            verifierSettings.AutoStop.GpiLevel = !gpiTriggerStateToProccessVerification;

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

       
        /// <summary>
        /// Handles GPI events for the reader.
        /// Only processes events for Port 1.
        /// If the event State is true and not already processing, starts the tag collection flow.
        /// When the state is false, resets the processing flag.
        /// </summary>
        private async void OnGpiEvent(ImpinjReader sender, GpiEvent e)
        {
            if (e.PortNumber != 1)
                return;
            try
            {
                if(useGpiForVerification)
                {
                    if (e.State == gpiTriggerStateToProccessVerification)
                    {
                        // Use Interlocked.CompareExchange to ensure only one processing instance runs.
                        if (Interlocked.CompareExchange(ref gpiProcessingFlag, 1, 0) == 0)
                        {
                            //sender.TagsReported += OnTagsReportedVerifier;
                            Console.WriteLine($"GPI Port 1 is {e.State} - setting processing flag.");
                        }
                        else
                        {
                            Console.WriteLine("GPI Port 1 event received while processing already in progress. Ignoring duplicate trigger.");
                        }
                    }
                    else
                    {
                        // When GPI state becomes false, reset the processing flag.
                        Console.WriteLine($"GPI Port 1 is {e.State} - resetting processing flag.");
                        //sender.TagsReported -= OnTagsReportedVerifier;
                        if(verificationTags.Count == 0)
                        {
                            sender.SetGpo(1, true);
                        }
                        verificationTags.Clear();
                        Interlocked.Exchange(ref gpiProcessingFlag, 0);
                    }
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
            
        }
        private void OnTagsReportedDetector(ImpinjReader sender, TagReport report)
        {
            if (report == null || IsCancellationRequested())
                return;

            foreach (var tag in report.Tags)
            {
                var tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                var epcHex = tag.Epc?.ToHexString() ?? string.Empty;
                if (epcHex.Length < 24)
                    continue;

                if (string.IsNullOrEmpty(tidHex) || TagOpController.Instance.IsTidProcessed(tidHex))
                    continue;

                // Here, simply record and log the detection.
                Console.WriteLine($"Detector: New tag detected. TID: {tidHex}, Current EPC: {epcHex}");
                // Generate a new EPC (if one is not already recorded) using the detector logic.
                var expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);
                if (string.IsNullOrEmpty(expectedEpc))
                {
                    expectedEpc = TagOpController.Instance.GetNextEpcForTag(epcHex, tidHex);
                    TagOpController.Instance.RecordExpectedEpc(tidHex, expectedEpc);
                    Console.WriteLine($"Detector: Assigned new EPC for TID {tidHex}: {expectedEpc}");

                    // Trigger the write operation using the writer reader.
                    TagOpController.Instance.TriggerWriteAndVerify(
                        tag,
                        expectedEpc,
                        writerReader,
                        cancellationToken,
                        swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()),
                        newAccessPassword,
                        true,
                        1,
                        true,
                        0);
                }
                // (Optionally, you might also update any UI or log this detection.)
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
                var epcHex = tag.Epc?.ToHexString() ?? string.Empty;
                if (epcHex.Length < 24)
                    continue;
                if (string.IsNullOrEmpty(tidHex) || TagOpController.Instance.IsTidProcessed(tidHex))
                    continue;

                cycleCount.TryAdd(tidHex, 0);

                if (cycleCount[tidHex] >= MaxCycles)
                {
                    Console.WriteLine($"Max cycles reached for TID {tidHex}, skipping further processing.");
                    continue;
                }

                var expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);

                // If no expected EPC exists, generate one using the writer logic.
                if (string.IsNullOrEmpty(expectedEpc))
                {
                    Console.WriteLine($">>>>>>>>>>New target TID found: {tidHex} Chip {TagOpController.Instance.GetChipModel(tag)}");
                    expectedEpc = TagOpController.Instance.GetNextEpcForTag(epcHex,tidHex);
                    TagOpController.Instance.RecordExpectedEpc(tidHex, expectedEpc);
                    Console.WriteLine($">>>>>>>>>>New tag found. TID: {tidHex}. Assigning new EPC: {epcHex} -> {expectedEpc}");
                }

                
                if (!expectedEpc.Equals(epcHex, StringComparison.InvariantCultureIgnoreCase))
                {
                    // Trigger the write operation using the writer reader.
                    TagOpController.Instance.TriggerWriteAndVerify(
                        tag,
                        expectedEpc,
                        sender,
                        cancellationToken,
                        swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()),
                        newAccessPassword,
                        true, 
                        1,
                        true,
                        3);

                    //TagOpController.Instance.TriggerPartialWriteAndVerify(
                    //    tag,
                    //    expectedEpc,
                    //    writerReader,
                    //    cancellationToken,
                    //    swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()),
                    //    newAccessPassword,
                    //    true,
                    //    14,
                    //    1,
                    //    true,
                    //    3);
                }
                else
                {
                    if (expectedEpc != null && expectedEpc.Equals(epcHex, StringComparison.OrdinalIgnoreCase))
                    {
                        verificationTags.TryAdd(tidHex, tag);
                        TagOpController.Instance.HandleVerifiedTag(tag, tidHex, expectedEpc, swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()), swVerifyTimers.GetOrAdd(tidHex, _ => new Stopwatch()), cycleCount, tag, TagOpController.Instance.GetChipModel(tag), logFile);
                        //Console.WriteLine($"TID {tidHex} verified successfully on writer reader. Current EPC: {epcHex}");
                        continue;
                    }                    
                }


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
                {
                    Console.WriteLine($"OnTagsReportedVerifier>>>>>>>>>>TID is EMPTY.");
                    continue;
                }
                    


                var epcHex = tag.Epc.ToHexString() ?? string.Empty;
                if (epcHex.Length != 24)
                {
                    Console.WriteLine($"OnTagsReportedVerifier>>>>>>>>>>Unexpected EPC length {epcHex.Length}");
                    continue;
                }

                var expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);

                // If no expected EPC exists, generate one using the writer logic.
                if (string.IsNullOrEmpty(expectedEpc))
                {
                    Console.WriteLine($"OnTagsReportedVerifier>>>>>>>>>>TID not found. Considering re-write for target TID found: {tidHex} Chip {TagOpController.Instance.GetChipModel(tag)}");
                    expectedEpc = TagOpController.Instance.GetNextEpcForTag(epcHex, tidHex);
                    TagOpController.Instance.RecordExpectedEpc(tidHex, expectedEpc);
                    Console.WriteLine($"OnTagsReportedVerifier>>>>>>>>>> TID: {tidHex}. Assigning EPC: {epcHex} -> {expectedEpc}");
                }

                bool success = expectedEpc.Equals(epcHex, StringComparison.InvariantCultureIgnoreCase);
                var writeStatus = success ? "Success" : "Failure";
                Console.WriteLine(".........................................");
                Console.WriteLine($"OnTagsReportedVerifier - TID {tidHex} - current EPC: {epcHex} Expected EPC: {expectedEpc} Operation Status [{writeStatus}]" );
                Console.WriteLine(".........................................");

                if (success)
                {
                    TagOpController.Instance.RecordResult(tidHex, writeStatus, success);
                    verificationTags.TryAdd(tidHex, tag);
                    Console.WriteLine($"OnTagsReportedVerifier - TID {tidHex} verified successfully on verifier reader. Current EPC: {epcHex} - Written tags regitered {TagOpController.Instance.GetSuccessCount()} (TIDs processed)");
                }
                else if (!string.IsNullOrEmpty(expectedEpc))
                {
                    
                    if (!expectedEpc.Equals(epcHex, StringComparison.InvariantCultureIgnoreCase))
                    {
                        Console.WriteLine($"Verification mismatch for TID {tidHex}: expected {expectedEpc}, read {epcHex}. Retrying write operation using expected EPC.");
                        // Retry writing using the expected EPC (without generating a new one) via the verifier reader.
                        TagOpController.Instance.TriggerWriteAndVerify(
                            tag,
                            expectedEpc,
                            sender,
                            cancellationToken,
                            swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()),
                            newAccessPassword,
                            true,
                            1,
                            false,
                            3);
                    }
                    else
                    {
                        verificationTags.TryAdd(tidHex, tag);
                        Console.WriteLine($"OnTagsReportedVerifier - TID {tidHex} verified successfully on verifier reader. Current EPC: {epcHex} - Written tags regitered {TagOpController.Instance.GetSuccessCount()} (TIDs processed)");
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
                var tempEpc = result.Tag.Epc.ToHexString();

                if (result is TagWriteOpResult writeResult)
                {
                    swWriteTimers[tidHex].Stop();

                    var expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);
                    var verifiedEpc = writeResult.Tag.Epc?.ToHexString() ?? "N/A";
                    bool success = !string.IsNullOrEmpty(expectedEpc) && expectedEpc.Equals(verifiedEpc, StringComparison.InvariantCultureIgnoreCase);
                    var writeStatus = success ? "Success" : "Failure";
                    if (success)
                    {
                        verificationTags.TryAdd(tidHex, writeResult.Tag);
                        TagOpController.Instance.RecordResult(tidHex, writeStatus, success);
                    }
                    else if (writeResult.Result == WriteResultStatus.Success)
                    {
                        Console.WriteLine($"OnTagOpComplete - Write operation succeeded for TID {tidHex} on reader {sender.Name}.");
                        // After a successful write, trigger a verification read on the verifier reader.
                        
                        TagOpController.Instance.TriggerVerificationRead(
                            result.Tag,
                            sender,
                            cancellationToken,
                            swVerifyTimers.GetOrAdd(tidHex, _ => new Stopwatch()),
                            newAccessPassword);
                    }
                    else
                    {
                        Console.WriteLine($"OnTagOpComplete - Write operation failed for TID {tidHex} on reader {sender.Name}.");
                    }
                }
                else if (result is TagReadOpResult readResult)
                {
                    swVerifyTimers[tidHex].Stop();

                    var expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);
                    if (string.IsNullOrEmpty(expectedEpc))
                    {
                        expectedEpc = TagOpController.Instance.GetNextEpcForTag(readResult.Tag.Epc.ToHexString(), tidHex);
                    }
                    var verifiedEpc = readResult.Tag.Epc?.ToHexString() ?? "N/A";
                    var success = verifiedEpc.Equals(expectedEpc, StringComparison.InvariantCultureIgnoreCase);
                    var status = success ? "Success" : "Failure";

                    if(success)
                    {
                        verificationTags.TryAdd(tidHex, readResult.Tag);
                    }
                    LogToCsv($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{result.Tag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{swWriteTimers[tidHex].ElapsedMilliseconds},{swVerifyTimers[tidHex].ElapsedMilliseconds},{status},{cycleCount.GetOrAdd(tidHex, 0)},RSSI,AntennaPort");
                    TagOpController.Instance.RecordResult(tidHex, status, success);

                    Console.WriteLine($"OnTagOpComplete - Verification result for TID {tidHex} on reader {sender.Address}: {status}");

                    cycleCount.AddOrUpdate(tidHex, 1, (key, oldValue) => oldValue + 1);

                    if (!success)
                    {
                        try
                        {
                            TagOpController.Instance.TriggerWriteAndVerify(
                            readResult.Tag,
                            expectedEpc,
                            sender,
                            cancellationToken,
                            swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()),
                            newAccessPassword,
                            true,
                            1,
                            true,
                            3);
                        }
                        catch (Exception)
                        {

                        }
                    }

                    
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

        /// <summary>
        /// Displays progress information including total read count and success count.
        /// This method is called by the timer every 10 seconds.
        /// </summary>
        /// <param name="state">Timer state (not used)</param>
        private void DisplayProgress(object state)
        {
            try
            {
                int successCount = TagOpController.Instance.GetSuccessCount();
                int totalReadCount = TagOpController.Instance.GetTotalReadCount();

                Console.WriteLine($"!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine($"!!!!!!!!!!!!!!!!!!!!!!!!!! Total Read [{totalReadCount}] Success count: [{successCount}] !!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine($"!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

                try
                {
                    // Update console title with current statistics
                    Console.Title = $"Serializer: {totalReadCount} - {successCount}";
                }
                catch (Exception)
                {
                    // Ignore exceptions when setting console title (can fail in some environments)
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                Console.WriteLine($"Error in DisplayProgress: {ex.Message}");
            }
        }

        // Também é necessário adicionar o método CleanupReaders se não existir:

        /// <summary>
        /// Cleans up all reader resources (detector, writer, and verifier)
        /// </summary>
        private void CleanupReaders()
        {
            try
            {
                // Stop and dispose the timer
                successCountTimer?.Dispose();

                // Cleanup detector reader
                if (detectorReader != null)
                {
                    try
                    {
                        detectorReader.Stop();
                        detectorReader.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error cleaning up detector reader: {ex.Message}");
                    }
                }

                // Cleanup writer reader
                if (writerReader != null)
                {
                    try
                    {
                        writerReader.Stop();
                        writerReader.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error cleaning up writer reader: {ex.Message}");
                    }
                }

                // Cleanup verifier reader
                if (verifierReader != null)
                {
                    try
                    {
                        verifierReader.Stop();
                        verifierReader.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error cleaning up verifier reader: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during readers cleanup: {ex.Message}");
            }
        }
    }
}
