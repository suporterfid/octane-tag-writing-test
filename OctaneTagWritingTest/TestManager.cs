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

        public TestManager(string hostname)
        {
            // Register available test strategies
            
            strategies.Add("0", new TestCase0ReadOnlyLoggingStrategy(hostname, "TestCase0_Log.csv"));
            strategies.Add("1", new TestCase1SpeedStrategy(hostname, "TestCase1_Log.csv"));
            strategies.Add("2", new TestCase2InlineWriteStrategy(hostname, "TestCase2_Log.csv"));
            strategies.Add("3", new TestCase3MultiAntennaWriteStrategy(hostname, "TestCase3_MultiAntenna_Log.csv"));
            strategies.Add("4", new TestCase4BatchSerializationPermalockStrategy(hostname, "TestCase4_Log.csv"));
            strategies.Add("5", new TestCase5VerificationCycleStrategy(hostname, "TestCase5_VerificationCycle_Log.csv"));
            strategies.Add("6", new TestCase6RobustnessStrategy(hostname, "TestCase6_Robustness_Log.csv"));
            strategies.Add("7", new TestCase7ErrorRecoveryStrategy(hostname, "TestCase7_ErrorRecovery_Log.csv"));
            strategies.Add("8", new TestCase8EnduranceStrategy(hostname, "TestCase8_Endurance_Log.csv"));
            strategies.Add("9", new TestCase9BulkEncodingLockStrategy(hostname, "TestCase9_Log.csv"));
            strategies.Add("10", new TestCase10OptimizedStrategy(hostname, "TestCase10_Log.csv"));

            
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
