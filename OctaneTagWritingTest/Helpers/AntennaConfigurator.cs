using Impinj.OctaneSdk;

namespace OctaneTagWritingTest.Helpers
{
    public static class AntennaConfigurator
    {
        /// <summary>
        /// Configura as antenas de um reader baseado na configuração fornecida
        /// </summary>
        /// <param name="settings">Settings do reader</param>
        /// <param name="antennaConfig">Configuração das antenas</param>
        public static void ConfigureAntennas(Settings settings, AntennaConfig antennaConfig)
        {
            // Primeiro, desabilita todas as antenas
            settings.Antennas.DisableAll();

            // Configura cada antena conforme especificado
            foreach (var antennaSettings in antennaConfig.Antennas)
            {
                if (antennaSettings.IsEnabled && antennaSettings.Port >= 1 && antennaSettings.Port <= 4)
                {
                    var antenna = settings.Antennas.GetAntenna((ushort)antennaSettings.Port);
                    antenna.IsEnabled = true;
                    antenna.MaxTxPower = false; // Para permitir configuração manual
                    antenna.TxPowerInDbm = antennaSettings.TxPowerInDbm;
                    antenna.MaxRxSensitivity = antennaSettings.MaxRxSensitivity;
                    antenna.RxSensitivityInDbm = antennaSettings.RxSensitivityInDbm;
                }
            }
        }

        /// <summary>
        /// Configuração de fallback baseada nas configurações legadas
        /// Mantém compatibilidade com o código existente
        /// </summary>
        /// <param name="settings">Settings do reader</param>
        /// <param name="config">Configuração da aplicação</param>
        /// <param name="role">Papel do reader (detector, writer, verifier)</param>
        public static void ConfigureAntennasLegacy(Settings settings, ApplicationConfig config, string role)
        {
            settings.Antennas.DisableAll();

            switch (role.ToLower())
            {
                case "detector":
                    settings.Antennas.GetAntenna(1).IsEnabled = true;
                    settings.Antennas.GetAntenna(1).TxPowerInDbm = config.DetectorTxPowerInDbm;
                    settings.Antennas.GetAntenna(1).MaxRxSensitivity = config.DetectorMaxRxSensitivity;
                    settings.Antennas.GetAntenna(1).RxSensitivityInDbm = config.DetectorRxSensitivityInDbm;
                    break;

                case "writer":
                    settings.Antennas.GetAntenna(1).IsEnabled = true;
                    settings.Antennas.GetAntenna(1).MaxTxPower = false;
                    settings.Antennas.GetAntenna(1).TxPowerInDbm = config.WriterTxPowerInDbm;
                    settings.Antennas.GetAntenna(1).MaxRxSensitivity = config.WriterMaxRxSensitivity;
                    settings.Antennas.GetAntenna(1).RxSensitivityInDbm = config.WriterRxSensitivityInDbm;
                    break;

                case "verifier":
                    settings.Antennas.GetAntenna(1).IsEnabled = true;
                    settings.Antennas.GetAntenna(1).MaxTxPower = false;
                    settings.Antennas.GetAntenna(1).TxPowerInDbm = config.VerifierTxPowerInDbm;
                    settings.Antennas.GetAntenna(1).MaxRxSensitivity = config.VerifierMaxRxSensitivity;
                    settings.Antennas.GetAntenna(1).RxSensitivityInDbm = config.VerifierRxSensitivityInDbm;
                    break;
            }
        }

        /// <summary>
        /// Configuração híbrida que usa a nova configuração se disponível,
        /// senão usa a configuração legada
        /// </summary>
        public static void ConfigureAntennasHybrid(Settings settings, ApplicationConfig config, string role)
        {
            AntennaConfig antennaConfig = null;

            switch (role.ToLower())
            {
                case "detector":
                    antennaConfig = config.DetectorAntennas;
                    break;
                case "writer":
                    antennaConfig = config.WriterAntennas;
                    break;
                case "verifier":
                    antennaConfig = config.VerifierAntennas;
                    break;
            }

            // Se existe configuração nova e tem antenas configuradas, usa a nova
            if (antennaConfig != null && antennaConfig.Antennas.Any(a => a.IsEnabled))
            {
                ConfigureAntennas(settings, antennaConfig);
            }
            else
            {
                // Senão, usa a configuração legada
                ConfigureAntennasLegacy(settings, config, role);
            }
        }
    }
}