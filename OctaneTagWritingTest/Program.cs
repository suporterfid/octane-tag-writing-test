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
            string hostname = args[0];
            string testDescription = "Writing-Test-1-Round-1";
            string epcHeader = "C300";
            string epcPlainItemCode = "76788888888888";
            long quantity = 1;
            EpcListManager.InitEpcData(epcHeader, epcPlainItemCode, quantity);

            string settingsFilePath = "reader_settings.json";

            bool forceCreateFile = true;
            Console.WriteLine($"epcHeader '{epcHeader}' - Test Description {testDescription}");

            if (!File.Exists(settingsFilePath) || forceCreateFile)
            {
                Console.WriteLine($"Settings file '{settingsFilePath}' will be created or replaced. Creating default settings...");

                var defaultSettings = new ReaderSettings
                {
                    Hostname = "192.168.68.248",
                    LogFile = "test_log.csv",
                    IncludeFastId = true,
                    IncludePeakRssi = true,
                    IncludeAntennaPortNumber = true,
                    ReportMode = "Individual",
                    RfMode = 0,
                    AntennaPort = 1,
                    TxPowerInDbm = 33,
                    MaxRxSensitivity = true,
                    RxSensitivityInDbm = -90,
                    SearchMode = "SingleTarget",
                    Session = 0,
                    MemoryBank = "Epc",
                    BitPointer = 32,
                    TagMask = "0017",
                    BitCount = 16,
                    FilterOp = "NotMatch",
                    FilterMode = "OnlyFilter1"
                };

                defaultSettings.Save(settingsFilePath);
            }

            var settings = ReaderSettings.Load(settingsFilePath);

            TestManager manager = new TestManager(hostname, testDescription, settings);


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
