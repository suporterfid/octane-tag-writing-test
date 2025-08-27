public static class InteractiveConfig
{
    public static ApplicationConfig Configure(ApplicationConfig initialConfig = null)
    {
        ApplicationConfig config = initialConfig ?? new ApplicationConfig();

        Console.WriteLine("=== Interactive Configuration Mode ===");
        Console.WriteLine("Press Enter to keep current values or input new ones.");

        config.DetectorHostname = PromptForValue("Detector Reader Hostname", config.DetectorHostname);
        config.WriterHostname = PromptForValue("Writer Reader Hostname", config.WriterHostname);
        config.VerifierHostname = PromptForValue("Verifier Reader Hostname", config.VerifierHostname);
        config.TestDescription = PromptForValue("Test Description", config.TestDescription);
        config.EpcHeader = PromptForValue("EPC Header (2 chars)", config.EpcHeader);
        config.EpcPlainItemCode = PromptForValue("EPC Plain Item Code", config.EpcPlainItemCode);
        config.Sku = PromptForValue("SKU (12 digits)", config.Sku);

        string qtyStr = PromptForValue("Quantity", config.Quantity.ToString());
        if (long.TryParse(qtyStr, out long qty))
            config.Quantity = qty;

        // Configuração básica de potência (mantendo compatibilidade)
        string dPowerStr = PromptForValue("Detector TX Power (dBm)", config.DetectorTxPowerInDbm.ToString());
        if (int.TryParse(dPowerStr, out int dPower))
            config.DetectorTxPowerInDbm = dPower;

        string wPowerStr = PromptForValue("Writer TX Power (dBm)", config.WriterTxPowerInDbm.ToString());
        if (int.TryParse(wPowerStr, out int wPower))
            config.WriterTxPowerInDbm = wPower;

        string vPowerStr = PromptForValue("Verifier TX Power (dBm)", config.VerifierTxPowerInDbm.ToString());
        if (int.TryParse(vPowerStr, out int vPower))
            config.VerifierTxPowerInDbm = vPower;

        // NOVA SEÇÃO: Configuração GPI
        Console.WriteLine("\n=== GPI Configuration (for JobStrategy8) ===");

        string useGpiStr = PromptForBoolValue("Use GPI for verification", config.UseGpiForVerification);
        config.UseGpiForVerification = bool.Parse(useGpiStr);

        string gpiPortStr = PromptForValue("GPI port number to process verification (1 or 2)", config.GpiPortToProcessVerification.ToString());
        if (int.TryParse(gpiPortStr, out int gpiPort))
            config.GpiPortToProcessVerification = gpiPort;

        string gpiTriggerStr = PromptForBoolValue("GPI trigger state to process verification", config.GpiTriggerStateToProcessVerification);
        config.GpiTriggerStateToProcessVerification = bool.Parse(gpiTriggerStr);

        string gpoPulsePortStr = PromptForValue("GPO port number for pulsed signal", config.GpoPortPulsed.ToString());
        if (int.TryParse(gpoPulsePortStr, out int gpoPulse))
            config.GpoPortPulsed = gpoPulse;

        string gpoStaticPortStr = PromptForValue("GPO port number for static signal", config.GpoPortStatic.ToString());
        if (int.TryParse(gpoStaticPortStr, out int gpoStatic))
            config.GpoPortStatic = gpoStatic;

        // Configuração avançada de antenas
        Console.WriteLine("\n=== Advanced Antenna Configuration ===");
        Console.Write("Configure individual antennas? (y/n) [n]: ");
        string configAntennas = Console.ReadLine();

        if (configAntennas?.ToLower() == "y")
        {
            Console.WriteLine("\nConfiguring Detector Antennas:");
            config.DetectorAntennas = ConfigureAntennaSet("Detector", config.DetectorAntennas);

            Console.WriteLine("\nConfiguring Writer Antennas:");
            config.WriterAntennas = ConfigureAntennaSet("Writer", config.WriterAntennas);

            Console.WriteLine("\nConfiguring Verifier Antennas:");
            config.VerifierAntennas = ConfigureAntennaSet("Verifier", config.VerifierAntennas);
        }

        Console.WriteLine("\nConfiguration complete!");

        // Ask if user wants to save this as a configuration file
        Console.Write("\nSave this configuration to file? (y/n) [n]: ");
        string saveResponse = Console.ReadLine();
        if (saveResponse?.ToLower() == "y")
        {
            string fileName = PromptForValue("Configuration file name", "config.json");
            try
            {
                config.SaveToFile(fileName);
                Console.WriteLine($"Configuration saved to {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
            }
        }

        return config;
    }

    // NOVO MÉTODO: Prompt específico para valores booleanos
    private static string PromptForBoolValue(string prompt, bool defaultValue)
    {
        Console.Write($"{prompt} (true/false) [{defaultValue}]: ");
        string input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            return defaultValue.ToString();

        if (bool.TryParse(input, out bool result))
            return result.ToString();

        // Se não conseguir fazer parse, retorna o valor padrão
        Console.WriteLine($"Invalid input. Using default value: {defaultValue}");
        return defaultValue.ToString();
    }

    private static AntennaConfig ConfigureAntennaSet(string readerType, AntennaConfig currentConfig)
    {
        AntennaConfig newConfig = new AntennaConfig();
        newConfig.Antennas.Clear();

        Console.WriteLine($"Current {readerType} antenna configuration:");
        if (currentConfig.Antennas.Any())
        {
            foreach (var antenna in currentConfig.Antennas)
            {
                if (antenna.IsEnabled)
                {
                    Console.WriteLine($"  Port {antenna.Port}: Enabled, TxPower={antenna.TxPowerInDbm}dBm, " +
                                    $"MaxRx={antenna.MaxRxSensitivity}, RxSens={antenna.RxSensitivityInDbm}dBm");
                }
            }
        }
        else
        {
            Console.WriteLine("  No antennas currently configured");
        }

        Console.Write($"\nConfigure {readerType} antennas? (y/n) [n]: ");
        string configThisReader = Console.ReadLine();
        
        if (configThisReader?.ToLower() != "y")
        {
            return currentConfig; // Keep current configuration
        }

        for (int port = 1; port <= 4; port++)
        {
            Console.Write($"\nEnable antenna port {port}? (y/n) [n]: ");
            string enableAntenna = Console.ReadLine();
            
            if (enableAntenna?.ToLower() == "y")
            {
                var antennaSettings = new AntennaSettings { Port = port, IsEnabled = true };

                string txPowerStr = PromptForValue($"TX Power for port {port} (dBm)", "30");
                if (int.TryParse(txPowerStr, out int txPower))
                    antennaSettings.TxPowerInDbm = txPower;

                Console.Write($"Use maximum RX sensitivity for port {port}? (y/n) [y]: ");
                string maxRxStr = Console.ReadLine();
                antennaSettings.MaxRxSensitivity = string.IsNullOrEmpty(maxRxStr) || maxRxStr.ToLower() == "y";

                if (!antennaSettings.MaxRxSensitivity)
                {
                    string rxSensStr = PromptForValue($"RX Sensitivity for port {port} (dBm)", "-90");
                    if (int.TryParse(rxSensStr, out int rxSens))
                        antennaSettings.RxSensitivityInDbm = rxSens;
                }

                newConfig.Antennas.Add(antennaSettings);
                Console.WriteLine($"Port {port} configured: TxPower={antennaSettings.TxPowerInDbm}dBm, " +
                                $"MaxRx={antennaSettings.MaxRxSensitivity}, RxSens={antennaSettings.RxSensitivityInDbm}dBm");
            }
        }

        if (!newConfig.Antennas.Any())
        {
            Console.WriteLine($"No antennas enabled for {readerType}. Using default configuration.");
            return currentConfig;
        }

        return newConfig;
    }

    private static string PromptForValue(string prompt, string defaultValue)
    {
        Console.Write($"{prompt} [{defaultValue}]: ");
        string input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }
}