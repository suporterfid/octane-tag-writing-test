using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using Org.LLRP.LTK.LLRPV1;
using Org.LLRP.LTK.LLRPV1.Impinj;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctaneTagWritingTest
{
    /// <summary>
    /// Base class for all test strategies implementing common functionality
    /// </summary>
    public abstract class BaseTestStrategy : ITestStrategy
    {
        protected ImpinjReader reader;
        protected string hostname;
        protected string newAccessPassword = "AABBCCDD";
        protected string targetTid;  // Will be set with first TID read
        protected bool isTargetTidSet = false;
        protected string logFile;
        protected Stopwatch sw = new Stopwatch();
        protected CancellationToken cancellationToken;

        /// <summary>
        /// Initializes a new instance of the BaseTestStrategy class
        /// </summary>
        /// <param name="hostname">The hostname of the RFID reader</param>
        /// <param name="logFile">The path to the log file for test results</param>
        public BaseTestStrategy(string hostname, string logFile)
        {
            this.hostname = hostname;
            this.logFile = logFile;
            reader = new ImpinjReader();
        }

        /// <summary>
        /// Enables low latency reporting mode for the reader
        /// </summary>
        /// <param name="settings">The reader settings to modify</param>
        protected void EnableLowLatencyReporting(Settings settings)
        {
            MSG_ADD_ROSPEC addRoSpecMessage = reader.BuildAddROSpecMessage(settings);
            MSG_SET_READER_CONFIG setReaderConfigMessage = reader.BuildSetReaderConfigMessage(settings);
            setReaderConfigMessage.AddCustomParameter(new PARAM_ImpinjReportBufferConfiguration()
            {
                ReportBufferMode = ENUM_ImpinjReportBufferMode.Low_Latency
            });
            reader.ApplySettings(setReaderConfigMessage, addRoSpecMessage);
        }

        /// <summary>
        /// Appends a line to the CSV log file
        /// </summary>
        /// <param name="line">The line to append to the log file</param>
        protected void LogToCsv(string line)
        {
            File.AppendAllText(logFile, line + Environment.NewLine);
        }

        /// <summary>
        /// Configures the reader including connection, settings, and EPC list loading
        /// </summary>
        /// <returns>The configured reader settings</returns>
        /// <remarks>
        /// This method:
        /// - Loads the predefined EPC list
        /// - Connects to the reader
        /// - Applies default settings
        /// - Enables FastId and Individual reporting mode
        /// - Enables low latency reporting
        /// </remarks>
        protected virtual Settings ConfigureReader()
        {
            // Load the predefined EPC list
            EpcListManager.LoadEpcList("epc_list.txt");

            reader.Connect(hostname);
            reader.ApplyDefaultSettings();

            Settings settings = reader.QueryDefaultSettings();
            settings.Report.IncludeFastId = true;
            settings.Report.IncludePeakRssi = true;
            settings.Report.IncludeAntennaPortNumber = true;
            settings.Report.Mode = ReportMode.Individual;
            settings.RfMode = 0;
            settings.Antennas.DisableAll();
            settings.Antennas.GetAntenna(1).IsEnabled = true;
            settings.Antennas.GetAntenna(1).TxPowerInDbm = 30;
            settings.Antennas.GetAntenna(1).MaxRxSensitivity = true;
            settings.SearchMode = SearchMode.DualTarget;
            settings.Session = 2;
            EnableLowLatencyReporting(settings);
            reader.ApplySettings(settings);

            return settings;
        }

        /// <summary>
        /// Cleans up reader resources
        /// </summary>
        protected virtual void CleanupReader()
        {
            try
            {
                if (reader != null)
                {
                    reader.Stop();
                    reader.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during reader cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if cancellation has been requested
        /// </summary>
        /// <returns>True if cancellation was requested, false otherwise</returns>
        protected bool IsCancellationRequested()
        {
            return cancellationToken.IsCancellationRequested;
        }

        /// <summary>
        /// Abstract method that each strategy will implement to execute its test
        /// </summary>
        public abstract void RunTest(CancellationToken cancellationToken = default);
    }
}
