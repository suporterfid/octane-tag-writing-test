using Impinj.OctaneSdk;

public class ApplicationConfig
{
    // Reader network settings
    public string DetectorHostname { get; set; } = "192.168.68.80";
    public string WriterHostname { get; set; } = "192.168.68.81";
    public string VerifierHostname { get; set; } = "192.168.68.82";

    public bool Sgtin96Enabled { get; set; } = true;

    public string Sgtin96SourceGtin { get; set; } = "";

    // Test parameters
    public string TestDescription { get; set; } = "Teste11-Aplicador-Integrado";
    public string EpcHeader { get; set; } = "B6";
    public string EpcPlainItemCode { get; set; } = "33449900112222";
    public string Sku { get; set; } = "334499001122";
    public long Quantity { get; set; } = 1;

    // Settings file path
    public string SettingsFilePath { get; set; } = "reader_settings.json";

    public bool GpiTriggerStateToProcessVerification { get; set; } = false;
    public bool UseGpiForVerification { get; set; } = true;

    // NOVA SEÇÃO: Configuração de Antenas
    public AntennaConfig DetectorAntennas { get; set; } = new AntennaConfig();
    public AntennaConfig WriterAntennas { get; set; } = new AntennaConfig();
    public AntennaConfig VerifierAntennas { get; set; } = new AntennaConfig();

    // Detector reader settings (mantendo compatibilidade)
    public int DetectorTxPowerInDbm { get; set; } = 16;
    public string DetectorSearchMode { get; set; } = "SingleTarget";
    public int DetectorSession { get; set; } = 0;
    public int DetectorRfMode { get; set; } = 0;
    public bool DetectorMaxRxSensitivity { get; set; } = false;
    public int DetectorRxSensitivityInDbm { get; set; } = -60;
    public string DetectorMemoryBank { get; set; } = "Epc";
    public int DetectorBitPointer { get; set; } = 32;
    public string DetectorTagMask { get; set; } = "0017";
    public int DetectorBitCount { get; set; } = 16;
    public string DetectorFilterOp { get; set; } = "NotMatch";
    public string DetectorFilterMode { get; set; } = "OnlyFilter1";

    // Writer reader settings (mantendo compatibilidade)
    public int WriterTxPowerInDbm { get; set; } = 33;
    public string WriterSearchMode { get; set; } = "SingleTarget";
    public int WriterSession { get; set; } = 0;
    public int WriterRfMode { get; set; } = 0;
    public bool WriterMaxRxSensitivity { get; set; } = true;
    public int WriterRxSensitivityInDbm { get; set; } = -90;
    public string WriterMemoryBank { get; set; } = "Epc";
    public int WriterBitPointer { get; set; } = 32;
    public string WriterTagMask { get; set; } = "0017";
    public int WriterBitCount { get; set; } = 16;
    public string WriterFilterOp { get; set; } = "NotMatch";
    public string WriterFilterMode { get; set; } = "OnlyFilter1";

    // Verifier reader settings (mantendo compatibilidade)
    public int VerifierTxPowerInDbm { get; set; } = 30;
    public string VerifierSearchMode { get; set; } = "SingleTarget";
    public int VerifierSession { get; set; } = 0;
    public int VerifierRfMode { get; set; } = 0;
    public bool VerifierMaxRxSensitivity { get; set; } = true;
    public int VerifierRxSensitivityInDbm { get; set; } = -90;
    public string VerifierMemoryBank { get; set; } = "Epc";
    public int VerifierBitPointer { get; set; } = 32;
    public string VerifierTagMask { get; set; } = "0017";
    public int VerifierBitCount { get; set; } = 16;
    public string VerifierFilterOp { get; set; } = "NotMatch";
    public string VerifierFilterMode { get; set; } = "OnlyFilter1";

    // Load configuration from JSON file
    public static ApplicationConfig LoadFromFile(string configPath)
    {
        if (!File.Exists(configPath))
            return new ApplicationConfig();

        string json = File.ReadAllText(configPath);
        return System.Text.Json.JsonSerializer.Deserialize<ApplicationConfig>(json);
    }

    // Save configuration to JSON file
    public void SaveToFile(string configPath)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
    }
}

public class AntennaConfig
{
    public List<AntennaSettings> Antennas { get; set; } = new List<AntennaSettings>
    {
        new AntennaSettings { Port = 1, IsEnabled = true, TxPowerInDbm = 30, MaxRxSensitivity = true, RxSensitivityInDbm = -90 }
    };
}

public class AntennaSettings
{
    public int Port { get; set; }
    public bool IsEnabled { get; set; } = false;
    public int TxPowerInDbm { get; set; } = 30;
    public bool MaxRxSensitivity { get; set; } = true;
    public int RxSensitivityInDbm { get; set; } = -90;
}