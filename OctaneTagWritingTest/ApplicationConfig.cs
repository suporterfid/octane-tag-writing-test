using Impinj.OctaneSdk;

/// <summary>
/// Configuration values for Octane tag writing tests.
/// Includes network, test and timing parameters.
/// </summary>
public class ApplicationConfig
{
    // Reader network settings
    // Leaving hostnames empty allows running strategies with a subset of readers

    /// <summary>
    /// Hostname or IP of the detector reader. Default: empty (disabled).
    /// </summary>
    public string DetectorHostname { get; set; } = string.Empty;

    /// <summary>
    /// Hostname or IP of the writer reader. Default: empty (disabled).
    /// </summary>
    public string WriterHostname { get; set; } = string.Empty;

    /// <summary>
    /// Hostname or IP of the verifier reader. Default: empty (disabled).
    /// </summary>
    public string VerifierHostname { get; set; } = string.Empty;

    /// <summary>
    /// Enables SGTIN-96 EPC generation. Default: <c>true</c>.
    /// </summary>
    public bool Sgtin96Enabled { get; set; } = true;

    /// <summary>
    /// Base GTIN used for SGTIN-96 generation. Default: empty.
    /// </summary>
    public string Sgtin96SourceGtin { get; set; } = "";

    // Test parameters
    /// <summary>
    /// Description used in log file names. Default: "Teste11-Aplicador-Integrado".
    /// </summary>
    public string TestDescription { get; set; } = "Teste11-Aplicador-Integrado";

    /// <summary>
    /// EPC header value in hexadecimal. Default: "B6".
    /// </summary>
    public string EpcHeader { get; set; } = "B6";

    /// <summary>
    /// EPC item identifier without the header. Default: "33449900112222".
    /// </summary>
    public string EpcPlainItemCode { get; set; } = "33449900112222";

    /// <summary>
    /// Stock keeping unit identifier. Default: "334499001122".
    /// </summary>
    public string Sku { get; set; } = "334499001122";

    /// <summary>
    /// Number of tags to generate. Default: 1 tag.
    /// </summary>
    public long Quantity { get; set; } = 1;

    // Settings file path
    /// <summary>
    /// Path to the reader settings file. Default: "reader_settings.json".
    /// </summary>
    public string SettingsFilePath { get; set; } = "reader_settings.json";

    /// <summary>
    /// Expected GPI state to trigger verification. Default: <c>false</c>.
    /// </summary>
    public bool GpiTriggerStateToProcessVerification { get; set; } = false;

    /// <summary>
    /// Determines whether GPI should be used for verification. Default: <c>true</c>.
    /// </summary>
    public bool UseGpiForVerification { get; set; } = true;

    /// <summary>
    /// GPI port monitored for verification events. Default: port 1.
    /// </summary>
    public int GpiPortToProcessVerification { get; set; } = 1;

    /// <summary>
    /// GPO port pulsed for feedback. Default: port 1.
    /// </summary>
    public int GpoPortPulsed { get; set; } = 1;

    /// <summary>
    /// GPO port held static for feedback. Default: port 2.
    /// </summary>
    public int GpoPortStatic { get; set; } = 2;

    // New: make GPI debounce and GPO pulse duration configurable
    /// <summary>
    /// Debounce time for GPI events in milliseconds. Default: 100 ms.
    /// </summary>
    public int GpiDebounceInMs { get; set; } = 100;

    /// <summary>
    /// Duration of GPO pulse in milliseconds. Default: 100 ms.
    /// </summary>
    public int GpoPulseDurationMs { get; set; } = 100;

    // NOVA SEÇÃO: Configuração de Antenas
    /// <summary>
    /// Antenna configuration for the detector reader.
    /// </summary>
    public AntennaConfig DetectorAntennas { get; set; } = new AntennaConfig();

    /// <summary>
    /// Antenna configuration for the writer reader.
    /// </summary>
    public AntennaConfig WriterAntennas { get; set; } = new AntennaConfig();

    /// <summary>
    /// Antenna configuration for the verifier reader.
    /// </summary>
    public AntennaConfig VerifierAntennas { get; set; } = new AntennaConfig();

    // Detector reader settings (mantendo compatibilidade)
    /// <summary>
    /// Transmit power for the detector reader in dBm. Default: 16 dBm.
    /// </summary>
    public int DetectorTxPowerInDbm { get; set; } = 16;

    /// <summary>
    /// Search mode used by the detector reader. Default: "SingleTarget".
    /// </summary>
    public string DetectorSearchMode { get; set; } = "SingleTarget";

    /// <summary>
    /// Session for detector inventory operations. Default: 0.
    /// </summary>
    public int DetectorSession { get; set; } = 0;

    /// <summary>
    /// RF mode index for the detector reader. Default: 0.
    /// </summary>
    public int DetectorRfMode { get; set; } = 0;

    /// <summary>
    /// Indicates whether the detector uses maximum receive sensitivity. Default: <c>false</c>.
    /// </summary>
    public bool DetectorMaxRxSensitivity { get; set; } = false;

    /// <summary>
    /// Receive sensitivity for the detector in dBm. Default: -60 dBm.
    /// </summary>
    public int DetectorRxSensitivityInDbm { get; set; } = -60;

    /// <summary>
    /// Memory bank used by the detector. Default: "Epc".
    /// </summary>
    public string DetectorMemoryBank { get; set; } = "Epc";

    /// <summary>
    /// Bit pointer for detector filtering. Default: 32 bits.
    /// </summary>
    public int DetectorBitPointer { get; set; } = 32;

    /// <summary>
    /// Tag mask used by the detector. Default: "0017".
    /// </summary>
    public string DetectorTagMask { get; set; } = "0017";

    /// <summary>
    /// Bit count for detector filtering. Default: 16 bits.
    /// </summary>
    public int DetectorBitCount { get; set; } = 16;

    /// <summary>
    /// Filter operation used by the detector. Default: "NotMatch".
    /// </summary>
    public string DetectorFilterOp { get; set; } = "NotMatch";

    /// <summary>
    /// Filter mode used by the detector. Default: "OnlyFilter1".
    /// </summary>
    public string DetectorFilterMode { get; set; } = "OnlyFilter1";

    // Writer reader settings (mantendo compatibilidade)
    /// <summary>
    /// Transmit power for the writer reader in dBm. Default: 33 dBm.
    /// </summary>
    public int WriterTxPowerInDbm { get; set; } = 33;

    /// <summary>
    /// Search mode used by the writer reader. Default: "SingleTarget".
    /// </summary>
    public string WriterSearchMode { get; set; } = "SingleTarget";

    /// <summary>
    /// Session for writer inventory operations. Default: 0.
    /// </summary>
    public int WriterSession { get; set; } = 0;

    /// <summary>
    /// RF mode index for the writer reader. Default: 0.
    /// </summary>
    public int WriterRfMode { get; set; } = 0;

    /// <summary>
    /// Indicates whether the writer uses maximum receive sensitivity. Default: <c>true</c>.
    /// </summary>
    public bool WriterMaxRxSensitivity { get; set; } = true;

    /// <summary>
    /// Receive sensitivity for the writer in dBm. Default: -90 dBm.
    /// </summary>
    public int WriterRxSensitivityInDbm { get; set; } = -90;

    /// <summary>
    /// Memory bank used by the writer. Default: "Epc".
    /// </summary>
    public string WriterMemoryBank { get; set; } = "Epc";

    /// <summary>
    /// Bit pointer for writer filtering. Default: 32 bits.
    /// </summary>
    public int WriterBitPointer { get; set; } = 32;

    /// <summary>
    /// Tag mask used by the writer. Default: "0017".
    /// </summary>
    public string WriterTagMask { get; set; } = "0017";

    /// <summary>
    /// Bit count for writer filtering. Default: 16 bits.
    /// </summary>
    public int WriterBitCount { get; set; } = 16;

    /// <summary>
    /// Filter operation used by the writer. Default: "NotMatch".
    /// </summary>
    public string WriterFilterOp { get; set; } = "NotMatch";

    /// <summary>
    /// Filter mode used by the writer. Default: "OnlyFilter1".
    /// </summary>
    public string WriterFilterMode { get; set; } = "OnlyFilter1";

    // Verifier reader settings (mantendo compatibilidade)
    /// <summary>
    /// Transmit power for the verifier reader in dBm. Default: 30 dBm.
    /// </summary>
    public int VerifierTxPowerInDbm { get; set; } = 30;

    /// <summary>
    /// Search mode used by the verifier reader. Default: "SingleTarget".
    /// </summary>
    public string VerifierSearchMode { get; set; } = "SingleTarget";

    /// <summary>
    /// Session for verifier inventory operations. Default: 0.
    /// </summary>
    public int VerifierSession { get; set; } = 0;

    /// <summary>
    /// RF mode index for the verifier reader. Default: 0.
    /// </summary>
    public int VerifierRfMode { get; set; } = 0;

    /// <summary>
    /// Indicates whether the verifier uses maximum receive sensitivity. Default: <c>true</c>.
    /// </summary>
    public bool VerifierMaxRxSensitivity { get; set; } = true;

    /// <summary>
    /// Receive sensitivity for the verifier in dBm. Default: -90 dBm.
    /// </summary>
    public int VerifierRxSensitivityInDbm { get; set; } = -90;

    /// <summary>
    /// Memory bank used by the verifier. Default: "Epc".
    /// </summary>
    public string VerifierMemoryBank { get; set; } = "Epc";

    /// <summary>
    /// Bit pointer for verifier filtering. Default: 32 bits.
    /// </summary>
    public int VerifierBitPointer { get; set; } = 32;

    /// <summary>
    /// Tag mask used by the verifier. Default: "0017".
    /// </summary>
    public string VerifierTagMask { get; set; } = "0017";

    /// <summary>
    /// Bit count for verifier filtering. Default: 16 bits.
    /// </summary>
    public int VerifierBitCount { get; set; } = 16;

    /// <summary>
    /// Filter operation used by the verifier. Default: "NotMatch".
    /// </summary>
    public string VerifierFilterOp { get; set; } = "NotMatch";

    /// <summary>
    /// Filter mode used by the verifier. Default: "OnlyFilter1".
    /// </summary>
    public string VerifierFilterMode { get; set; } = "OnlyFilter1";

    // Load configuration from JSON file
    /// <summary>
    /// Loads <see cref="ApplicationConfig"/> from the specified JSON file.
    /// Returns defaults when the file is missing.
    /// </summary>
    public static ApplicationConfig LoadFromFile(string configPath)
    {
        if (!File.Exists(configPath))
            return new ApplicationConfig();

        string json = File.ReadAllText(configPath);
        return System.Text.Json.JsonSerializer.Deserialize<ApplicationConfig>(json);
    }

    // Save configuration to JSON file
    /// <summary>
    /// Saves this configuration instance to the given JSON file path.
    /// </summary>
    public void SaveToFile(string configPath)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
    }
}

/// <summary>
/// Collection of antenna settings for a reader.
/// </summary>
public class AntennaConfig
{
    /// <summary>
    /// List of antenna configurations. Defaults to a single enabled antenna on port 1.
    /// </summary>
    public List<AntennaSettings> Antennas { get; set; } = new List<AntennaSettings>
    {
        new AntennaSettings { Port = 1, IsEnabled = true, TxPowerInDbm = 30, MaxRxSensitivity = true, RxSensitivityInDbm = -90 }
    };
}

/// <summary>
/// Configuration values for a single antenna port.
/// </summary>
public class AntennaSettings
{
    /// <summary>
    /// Antenna port number. No default value.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Indicates whether the antenna port is enabled. Default: <c>false</c>.
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// Transmit power for the antenna in dBm. Default: 30 dBm.
    /// </summary>
    public int TxPowerInDbm { get; set; } = 30;

    /// <summary>
    /// Whether maximum receive sensitivity is used. Default: <c>true</c>.
    /// </summary>
    public bool MaxRxSensitivity { get; set; } = true;

    /// <summary>
    /// Receive sensitivity for the antenna in dBm. Default: -90 dBm.
    /// </summary>
    public int RxSensitivityInDbm { get; set; } = -90;
}