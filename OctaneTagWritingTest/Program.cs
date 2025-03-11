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
            TestManager manager = new TestManager(hostname);

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
