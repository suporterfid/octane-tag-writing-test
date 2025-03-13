using OctaneTagWritingTest.Helpers;

namespace OctaneTagWritingTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Erro: Informe o hostname do leitor como argumento.");
                return;
            }
            string hostname = args[0];
            string testDescription = "Gravacao-Teste-1-Rodada-1";
            string epcHeader = "F002";
            string epcPlainItemCode = "99999999999999";
            long quantity = 1;
            EpcListManager.InitEpcData(epcHeader, epcPlainItemCode, quantity);

            string settingsFilePath = "reader_settings.json";

            bool forceCreateFile = true;

            if (!File.Exists(settingsFilePath) || forceCreateFile)
            {
                Console.WriteLine($"Settings file '{settingsFilePath}' not found. Creating default settings...");

                var defaultSettings = new ReaderSettings
                {
                    Hostname = "192.168.68.248",
                    LogFile = "test_log.csv",
                    IncludeFastId = true,
                    IncludePeakRssi = true,
                    IncludeAntennaPortNumber = true,
                    ReportMode = "Individual",
                    RfMode = 1002,
                    TxPowerInDbm = 29,
                    MaxRxSensitivity = false,
                    RxSensitivityInDbm = -70,
                    SearchMode = "DualTarget",
                    Session = 2,
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
                Console.Write("Escolha uma opção (ou 'q' para sair): ");
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
