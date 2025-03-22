using OctaneTagWritingTest.Helpers;

namespace OctaneTagWritingTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Error: Please provide the reader hostname as an argument.");
                return;
            }
            string hostnameWriter = args[0];
            string hostnameVerifier = args[1];
            string testDescription = "Writing-Test-1-Round-1";
            string epcHeader = "C300";
            //string epcPlainItemCode = "76788888888888";
            string epcPlainItemCode = "7678";
            long quantity = 1;
            EpcListManager.Instance.InitEpcData(epcHeader, epcPlainItemCode, quantity);

            string settingsFilePath = "reader_settings.json";

            Console.WriteLine($"Settings file '{settingsFilePath}' will be created or replaced. Creating default settings...");
            var writerSettings = ReaderSettings.CreateNamed("writer");
            writerSettings.Hostname = "writer.local";
            writerSettings.Hostname = "192.168.68.248";
            writerSettings.LogFile = "test_log_writer.csv";
            writerSettings.IncludeFastId = true;
            writerSettings.IncludePeakRssi = true;
            writerSettings.IncludeAntennaPortNumber = true;
            writerSettings.ReportMode = "Individual";
            writerSettings.RfMode = 0;
            writerSettings.AntennaPort = 1;
            writerSettings.TxPowerInDbm = 33;
            writerSettings.MaxRxSensitivity = true;
            writerSettings.RxSensitivityInDbm = -90;
            writerSettings.SearchMode = "SingleTarget";
            writerSettings.Session = 0;
            writerSettings.MemoryBank = "Epc";
            writerSettings.BitPointer = 32;
            writerSettings.TagMask = "0017";
            writerSettings.BitCount = 16;
            writerSettings.FilterOp = "NotMatch";
            writerSettings.FilterMode = "OnlyFilter1";
            ReaderSettingsManager.Instance.SaveSettings(writerSettings);

            Console.WriteLine($"Settings file '{settingsFilePath}' for verifier will be created or replaced. Creating default settings...");
            var verifierSettings = ReaderSettings.CreateNamed("verifier");
            verifierSettings.Hostname = "verifier.local";
            verifierSettings.Hostname = "192.168.68.94";
            verifierSettings.LogFile = "test_log_verifier.csv";
            verifierSettings.IncludeFastId = true;
            verifierSettings.IncludePeakRssi = true;
            verifierSettings.IncludeAntennaPortNumber = true;
            verifierSettings.ReportMode = "Individual";
            verifierSettings.RfMode = 0;
            verifierSettings.AntennaPort = 1;
            verifierSettings.TxPowerInDbm = 33;
            verifierSettings.MaxRxSensitivity = true;
            verifierSettings.RxSensitivityInDbm = -90;
            verifierSettings.SearchMode = "SingleTarget";
            verifierSettings.Session = 0;
            verifierSettings.MemoryBank = "Epc";
            verifierSettings.BitPointer = 32;
            verifierSettings.TagMask = "0017";
            verifierSettings.BitCount = 16;
            verifierSettings.FilterOp = "NotMatch";
            verifierSettings.FilterMode = "OnlyFilter1";
            ReaderSettingsManager.Instance.SaveSettings(verifierSettings);

            // Create dictionary of reader settings
            var readerSettings = new Dictionary<string, ReaderSettings>
            {
                { "writer", writerSettings },
                { "verifier", verifierSettings }
            };

            JobManager manager = new JobManager(hostnameWriter, hostnameVerifier, testDescription, readerSettings);


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
