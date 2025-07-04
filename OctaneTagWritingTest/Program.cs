using OctaneTagWritingTest.Helpers;
using System.Diagnostics;

namespace OctaneTagWritingTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Configure the console window
            Console.Clear();
            Console.Title = "Serializer";

            // The .NET diagnostics trace listener configuration
            ConfigureTraceListeners();

            // Check for help request
            if (args.Contains("--help") || args.Contains("-h"))
            {
                CommandLineParser.ShowHelp();
                return;
            }

            // Initialize configuration
            ApplicationConfig config;
            bool isRunningInDebugMode = false;

#if DEBUG
            isRunningInDebugMode = true;
            // When debugging in Visual Studio, you might want to use specific settings
            // or enter interactive mode automatically for convenience
            if (args.Length == 0)
            {
                Console.WriteLine("Debug mode detected with no arguments - entering interactive mode");
                config = InteractiveConfig.Configure();
            }
            else
            {
                config = CommandLineParser.ParseArgs(args);
            }
#endif
            if (!isRunningInDebugMode)
            {
                // Check for interactive mode
                if (args.Contains("--interactive") || args.Contains("-i"))
                {
                    config = InteractiveConfig.Configure();
                }
                else
                {
                    // Parse command line args
                    config = CommandLineParser.ParseArgs(args);
                }
            }

            // Initialize EPC Manager
            EpcListManager.Instance.InitEpcData(config.EpcHeader, config.EpcPlainItemCode, config.Quantity);

            // Create reader settings
            var readerSettings = CreateReaderSettings(config);

            // CRIAR JobManager PASSANDO A ApplicationConfig
            JobManager manager = new JobManager(
                config.DetectorHostname,
                config.WriterHostname,
                config.VerifierHostname,
                config.TestDescription,
                readerSettings,
                config,              // NOVO PARÂMETRO: Passar a ApplicationConfig
                config.Sku);

            // Main application loop
            RunApplicationLoop(manager);
        }

        private static void ConfigureTraceListeners()
        {
            Trace.Listeners.Clear();
            ConsoleTraceListener ctl = new ConsoleTraceListener(false);
            ctl.TraceOutputOptions = TraceOptions.DateTime;
            Trace.Listeners.Add(ctl);
            Trace.AutoFlush = true;
        }

        private static Dictionary<string, ReaderSettings> CreateReaderSettings(ApplicationConfig config)
        {
            // Create detector settings
            var detectorSettings = ReaderSettings.CreateNamed("detector");
            detectorSettings.Hostname = config.DetectorHostname;
            detectorSettings.LogFile = $"detector_log_{config.TestDescription}.csv";
            detectorSettings.IncludeFastId = true;
            detectorSettings.IncludePeakRssi = true;
            detectorSettings.IncludeAntennaPortNumber = true;
            detectorSettings.ReportMode = "Individual";
            detectorSettings.RfMode = config.DetectorRfMode;
            detectorSettings.AntennaPort = 1;
            detectorSettings.TxPowerInDbm = config.DetectorTxPowerInDbm;
            detectorSettings.MaxRxSensitivity = config.DetectorMaxRxSensitivity;
            detectorSettings.RxSensitivityInDbm = config.DetectorRxSensitivityInDbm;
            detectorSettings.SearchMode = config.DetectorSearchMode;
            detectorSettings.Session = config.DetectorSession;
            detectorSettings.MemoryBank = config.DetectorMemoryBank;
            detectorSettings.BitPointer = config.DetectorBitPointer;
            detectorSettings.TagMask = config.DetectorTagMask;
            detectorSettings.BitCount = config.DetectorBitCount;
            detectorSettings.FilterOp = config.DetectorFilterOp;
            detectorSettings.FilterMode = config.DetectorFilterMode;
            ReaderSettingsManager.Instance.SaveSettings(detectorSettings);

            // Create writer settings
            var writerSettings = ReaderSettings.CreateNamed("writer");
            writerSettings.Hostname = config.WriterHostname;
            writerSettings.LogFile = $"writer_log_{config.TestDescription}.csv";
            writerSettings.IncludeFastId = true;
            writerSettings.IncludePeakRssi = true;
            writerSettings.IncludeAntennaPortNumber = true;
            writerSettings.ReportMode = "Individual";
            writerSettings.RfMode = config.WriterRfMode;
            writerSettings.AntennaPort = 1;
            writerSettings.TxPowerInDbm = config.WriterTxPowerInDbm;
            writerSettings.MaxRxSensitivity = config.WriterMaxRxSensitivity;
            writerSettings.RxSensitivityInDbm = config.WriterRxSensitivityInDbm;
            writerSettings.SearchMode = config.WriterSearchMode;
            writerSettings.Session = config.WriterSession;
            writerSettings.MemoryBank = config.WriterMemoryBank;
            writerSettings.BitPointer = config.WriterBitPointer;
            writerSettings.TagMask = config.WriterTagMask;
            writerSettings.BitCount = config.WriterBitCount;
            writerSettings.FilterOp = config.WriterFilterOp;
            writerSettings.FilterMode = config.WriterFilterMode;
            ReaderSettingsManager.Instance.SaveSettings(writerSettings);

            // Create verifier settings
            var verifierSettings = ReaderSettings.CreateNamed("verifier");
            verifierSettings.Hostname = config.VerifierHostname;
            verifierSettings.LogFile = $"verifier_log_{config.TestDescription}.csv";
            verifierSettings.IncludeFastId = true;
            verifierSettings.IncludePeakRssi = true;
            verifierSettings.IncludeAntennaPortNumber = true;
            verifierSettings.ReportMode = "Individual";
            verifierSettings.RfMode = config.VerifierRfMode;
            verifierSettings.AntennaPort = 1;
            verifierSettings.TxPowerInDbm = config.VerifierTxPowerInDbm;
            verifierSettings.MaxRxSensitivity = config.VerifierMaxRxSensitivity;
            verifierSettings.RxSensitivityInDbm = config.VerifierRxSensitivityInDbm;
            verifierSettings.SearchMode = config.VerifierSearchMode;
            verifierSettings.Session = config.VerifierSession;
            verifierSettings.MemoryBank = config.VerifierMemoryBank;
            verifierSettings.BitPointer = config.VerifierBitPointer;
            verifierSettings.TagMask = config.VerifierTagMask;
            verifierSettings.BitCount = config.VerifierBitCount;
            verifierSettings.FilterOp = config.VerifierFilterOp;
            verifierSettings.FilterMode = config.VerifierFilterMode;
            ReaderSettingsManager.Instance.SaveSettings(verifierSettings);

            // Return as dictionary
            return new Dictionary<string, ReaderSettings>
    {
        { "detector", detectorSettings },
        { "writer", writerSettings },
        { "verifier", verifierSettings }
    };
        }

        private static void RunApplicationLoop(JobManager manager)
        {
            while (true)
            {
                manager.DisplayMenu();
                Console.Write("Choose an option (or 'q' to quit): ");
                string option = Console.ReadLine();
                if (option?.ToLower() == "q")
                    break;

                // Create cancellation token source for the test
                using var cts = new CancellationTokenSource();

                // Start a task to monitor for the 'q' key press
                var keyMonitorTask = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.KeyChar == 'q')
                            {
                                cts.Cancel();
                                return;
                            }
                        }
                        Thread.Sleep(100); // Reduce CPU usage
                    }
                });

                try
                {
                    // Execute the test with cancellation support
                    manager.ExecuteTest(option, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nTest cancelled by user.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError executing test: {ex.Message}");
                }

                // Wait for the key monitoring task to complete
                keyMonitorTask.Wait();
            }
        }
    }
}
