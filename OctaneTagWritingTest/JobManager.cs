using OctaneTagWritingTest.JobStrategies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace OctaneTagWritingTest
{
    internal class JobManager
    {
        private static readonly ILogger Logger = LoggingConfiguration.CreateLogger<JobManager>();
        private Dictionary<string, IJobStrategy> strategies = new Dictionary<string, IJobStrategy>();
        private Dictionary<string, ReaderSettings> readerSettings;
        private ApplicationConfig applicationConfig; // NOVO CAMPO

        // CONSTRUTOR ATUALIZADO: Adicionar ApplicationConfig
        public JobManager(string hostnameDetector, string hostnameWriter, string hostnameVerifier, 
                         string testDescription, Dictionary<string, ReaderSettings> settings, 
                         ApplicationConfig appConfig, string sku = null)  // NOVO PARÂMETRO
        {
            this.readerSettings = settings;
            this.applicationConfig = appConfig; // ARMAZENAR a configuração

            // Register available test strategies
            if(string.IsNullOrEmpty(testDescription))
            {
                testDescription = "Writing-Test-1-Round-1";
            }
            
            // Core strategies - maintained
            strategies.Add("0", new JobStrategy0ReadOnlyLogging(hostnameWriter, $"TestCase0_ReadOnlyLogging_Log-{testDescription}.csv", readerSettings));
            strategies.Add("2", new JobStrategy2MultiAntennaWriteStrategy(hostnameWriter, $"TestCase2_MultiAntenna_Log-{testDescription}.csv", readerSettings));
            strategies.Add("3", new JobStrategy3BatchSerializationPermalockStrategy(hostnameWriter, $"TestCase3_Log-BatchSerializationPermalockStrategy-{testDescription}.csv", readerSettings));
            strategies.Add("5", new JobStrategy5EnduranceStrategy(hostnameWriter, $"TestCase5_Endurance_Log-{testDescription}.csv", readerSettings));
            
            // Consolidated strategies - JobStrategy7 now handles multiple scenarios
            strategies.Add("1", new JobStrategy7OptimizedStrategy(hostnameWriter, $"TestCase1_Log-SpeedStrategy-{testDescription}.csv", readerSettings, maxRetries: 0, enableRetries: false, measureSpeedOnly: true)); // Speed test (was JobStrategy1)
            strategies.Add("4", new JobStrategy5EnduranceStrategy(hostnameWriter, $"TestCase4_VerificationCycle_Log-{testDescription}.csv", readerSettings, maxCycles: 1)); // Single cycle verification (was JobStrategy4)
            strategies.Add("6", new JobStrategy7OptimizedStrategy(hostnameWriter, $"TestCase6_Robustness_Log-{testDescription}.csv", readerSettings, maxRetries: 5, enableRetries: true)); // Robustness with 5 retries (was JobStrategy6)
            strategies.Add("7", new JobStrategy7OptimizedStrategy(hostnameWriter, $"TestCase7_Log-OptimizedStrategy-{testDescription}.csv", readerSettings)); // Default optimized strategy
            
            // STRATEGY 8 ATUALIZADA: Passar ApplicationConfig
            strategies.Add("8", new JobStrategy8MultipleReaderEnduranceStrategy(
                hostnameDetector, 
                hostnameWriter, 
                hostnameVerifier, 
                $"TestCase8_Log-DualReaderEnduranceStrategy-{testDescription}.csv", 
                readerSettings,
                appConfig));  // NOVO PARÂMETRO
                
            strategies.Add("9", new JobStrategy9CheckBox(hostnameWriter, $"TestCase9_Log-CheckBox-{testDescription}.csv", readerSettings, sku));
        }

        // CONSTRUTOR LEGACY: Para manter compatibilidade com código existente
        public JobManager(string hostnameDetector, string hostnameWriter, string hostnameVerifier, 
                         string testDescription, Dictionary<string, ReaderSettings> settings, string sku = null)
        {
            // Criar uma ApplicationConfig padrão a partir dos ReaderSettings
            var defaultConfig = CreateDefaultApplicationConfig(settings, hostnameDetector, hostnameWriter, hostnameVerifier);
            
            // Chamar o construtor principal
            var updatedJobManager = new JobManager(hostnameDetector, hostnameWriter, hostnameVerifier, 
                                                 testDescription, settings, defaultConfig, sku);
            
            // Copiar as strategies criadas
            this.strategies = updatedJobManager.strategies;
            this.readerSettings = updatedJobManager.readerSettings;
            this.applicationConfig = updatedJobManager.applicationConfig;
        }

        private ApplicationConfig CreateDefaultApplicationConfig(Dictionary<string, ReaderSettings> settings, 
                                                               string hostnameDetector, string hostnameWriter, string hostnameVerifier)
        {
            var config = new ApplicationConfig
            {
                DetectorHostname = hostnameDetector,
                WriterHostname = hostnameWriter,
                VerifierHostname = hostnameVerifier
            };

            // Configurar baseado nos ReaderSettings se disponíveis
            if (settings.ContainsKey("detector"))
            {
                var detectorSettings = settings["detector"];
                config.DetectorTxPowerInDbm = detectorSettings.TxPowerInDbm;
                config.DetectorMaxRxSensitivity = detectorSettings.MaxRxSensitivity;
                config.DetectorRxSensitivityInDbm = detectorSettings.RxSensitivityInDbm;
                config.DetectorSearchMode = detectorSettings.SearchMode;
                config.DetectorSession = detectorSettings.Session;
                config.DetectorRfMode = detectorSettings.RfMode;
            }

            if (settings.ContainsKey("writer"))
            {
                var writerSettings = settings["writer"];
                config.WriterTxPowerInDbm = writerSettings.TxPowerInDbm;
                config.WriterMaxRxSensitivity = writerSettings.MaxRxSensitivity;
                config.WriterRxSensitivityInDbm = writerSettings.RxSensitivityInDbm;
                config.WriterSearchMode = writerSettings.SearchMode;
                config.WriterSession = writerSettings.Session;
                config.WriterRfMode = writerSettings.RfMode;
            }

            if (settings.ContainsKey("verifier"))
            {
                var verifierSettings = settings["verifier"];
                config.VerifierTxPowerInDbm = verifierSettings.TxPowerInDbm;
                config.VerifierMaxRxSensitivity = verifierSettings.MaxRxSensitivity;
                config.VerifierRxSensitivityInDbm = verifierSettings.RxSensitivityInDbm;
                config.VerifierSearchMode = verifierSettings.SearchMode;
                config.VerifierSession = verifierSettings.Session;
                config.VerifierRfMode = verifierSettings.RfMode;
            }

            return config;
        }

        public void DisplayMenu()
        {
            Logger.Information("=== Test Manager ===");
            Logger.Information("Select a test to execute:");
            foreach (var kvp in strategies)
            {
                Logger.Information("[{Key}] - {StrategyName}", kvp.Key, kvp.Value.GetType().Name);
            }
        }

        public void ExecuteTest(string key, CancellationToken cancellationToken = default)
        {
            if (strategies.ContainsKey(key))
            {
                Logger.Information("Executing test strategy {Key}: {StrategyName}", key, strategies[key].GetType().Name);
                strategies[key].RunJob(cancellationToken);
            }
            else
            {
                Logger.Warning("Invalid test option selected: {Key}", key);
            }
        }
    }
}