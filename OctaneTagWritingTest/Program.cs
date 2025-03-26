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
            //Console.SetWindowSize(150, 50);
            //Console.BufferHeight = 1000;

            // The .NET diagnostics trace listener is used as a mechanism for
            // outputing to both the console and a file at the same time.
            // Start by clearing all listeners.
            Trace.Listeners.Clear();

            // Now add the console as a listener
            ConsoleTraceListener ctl = new ConsoleTraceListener(false);
            ctl.TraceOutputOptions = TraceOptions.DateTime;

            Trace.Listeners.Add(ctl);
            Trace.AutoFlush = true;
            //if (args.Length < 1)
            //{
            //    Console.WriteLine("Error: Please provide the reader hostname as an argument.");
            //    return;
            //}
            //string hostnameWriter = args[0];
            //string hostnameVerifier = args[1];

            string hostnameDetector= "192.168.68.93";
            string hostnameWriter = "192.168.68.248";
            string hostnameVerifier = "192.168.68.94";

            string testDescription = "Gravacao-Teste-1-Tarde-Rodada-3-";
            string epcHeader = "B023";
            //string epcPlainItemCode = "76788888888888";
            string epcPlainItemCode = "1122334455";
            long quantity = 1;
            EpcListManager.Instance.InitEpcData(epcHeader, epcPlainItemCode, quantity);

            string settingsFilePath = "reader_settings.json";

            Console.WriteLine($"Settings file '{settingsFilePath}' will be created or replaced. Creating default settings...");
            var detectorSettings = ReaderSettings.CreateNamed("detector");
            detectorSettings.Hostname = "writer.local";
            detectorSettings.Hostname = "192.168.68.248";
            detectorSettings.LogFile = "test_log_writer.csv";
            detectorSettings.IncludeFastId = true;
            detectorSettings.IncludePeakRssi = true;
            detectorSettings.IncludeAntennaPortNumber = true;
            detectorSettings.ReportMode = "Individual";
            detectorSettings.RfMode = 0;
            detectorSettings.AntennaPort = 1;
            detectorSettings.TxPowerInDbm = 30;
            detectorSettings.MaxRxSensitivity = true;
            detectorSettings.RxSensitivityInDbm = -90;
            detectorSettings.SearchMode = "SingleTarget";
            detectorSettings.Session = 0;
            detectorSettings.MemoryBank = "Epc";
            detectorSettings.BitPointer = 32;
            detectorSettings.TagMask = "0017";
            detectorSettings.BitCount = 16;
            detectorSettings.FilterOp = "NotMatch";
            detectorSettings.FilterMode = "OnlyFilter1";
            ReaderSettingsManager.Instance.SaveSettings(detectorSettings);

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
            writerSettings.TxPowerInDbm = 30;
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
            verifierSettings.TxPowerInDbm = 27;
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
                { "detector", detectorSettings },
                { "writer", writerSettings },
                { "verifier", verifierSettings }
            };

            JobManager manager = new JobManager(hostnameDetector, hostnameWriter, hostnameVerifier, testDescription, readerSettings);


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
