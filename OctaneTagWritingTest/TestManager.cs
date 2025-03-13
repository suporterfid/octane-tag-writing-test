using OctaneTagWritingTest.TestStrategy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctaneTagWritingTest
{
    internal class TestManager
    {
        private Dictionary<string, ITestStrategy> strategies = new Dictionary<string, ITestStrategy>();

        private ReaderSettings readerSettings;

        public TestManager(string hostname, string testDescription, ReaderSettings settings)
        {
            this.readerSettings = settings;

            // Register available test strategies
            if(string.IsNullOrEmpty(testDescription))
            {
                testDescription = "Gravacao-Teste-1-Rodada-1";
            }
            
            strategies.Add("0", new TestCase0ReadOnlyLoggingStrategy(hostname, $"TestCase0_Log-{testDescription}.csv", readerSettings));
            strategies.Add("1", new TestCase1SpeedStrategy(hostname, $"TestCase1_Log-{testDescription}.csv", readerSettings));    
            strategies.Add("2", new TestCase2MultiAntennaWriteStrategy(hostname, $"TestCase3_MultiAntenna_Log-{testDescription}.csv", readerSettings));
            strategies.Add("3", new TestCase3BatchSerializationPermalockStrategy(hostname, $"TestCase3_Log-{testDescription}.csv", readerSettings));
            strategies.Add("4", new TestCase4VerificationCycleStrategy(hostname, $"TestCase4_VerificationCycle_Log-{testDescription}.csv", readerSettings));
            strategies.Add("5", new TestCase5EnduranceStrategy(hostname, $"TestCase8_Endurance_Log-{testDescription}.csv", readerSettings));
            strategies.Add("6", new TestCase6RobustnessStrategy(hostname, $"TestCase6_Robustness_Log-{testDescription}.csv", readerSettings));
            strategies.Add("7", new TestCase7OptimizedStrategy(hostname, $"TestCase7_Log-{testDescription}.csv", readerSettings));

            
        }


        public void DisplayMenu()
        {
            Console.WriteLine("\n=== Test Manager ===");
            Console.WriteLine("Select a test to execute:");
            foreach (var kvp in strategies)
            {
                Console.WriteLine($"[{kvp.Key}] - {kvp.Value.GetType().Name}");
            }
        }

        public void ExecuteTest(string key, CancellationToken cancellationToken = default)
        {
            if (strategies.ContainsKey(key))
            {
                
                strategies[key].RunTest(cancellationToken);
            }
            else
            {
                Console.WriteLine("Invalid option.");
            }
        }
    }
}
