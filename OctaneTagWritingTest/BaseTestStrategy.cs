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
    public abstract class BaseTestStrategy : ITestStrategy
    {
        protected ImpinjReader reader;
        protected string hostname;
        protected string newAccessPassword = "AABBCCDD";
        protected string targetTid;  // Will be set with first TID read
        protected bool isTargetTidSet = false;
        protected string logFile;
        protected Stopwatch sw = new Stopwatch();

        public BaseTestStrategy(string hostname, string logFile)
        {
            this.hostname = hostname;
            this.logFile = logFile;
            reader = new ImpinjReader();
        }

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

        protected void LogToCsv(string line)
        {
            File.AppendAllText(logFile, line + Environment.NewLine);
        }

        /// <summary>
        /// Configures the reader including connection, settings, and EPC list loading
        /// </summary>
        /// <returns>The configured reader settings</returns>
        protected virtual Settings ConfigureReader()
        {
            // Load the predefined EPC list
            EpcListManager.LoadEpcList("epc_list.txt");

            reader.Connect(hostname);
            reader.ApplyDefaultSettings();

            Settings settings = reader.QueryDefaultSettings();
            settings.Report.IncludeFastId = true;
            settings.Report.Mode = ReportMode.Individual;
            EnableLowLatencyReporting(settings);
            reader.ApplySettings(settings);

            return settings;
        }

        /// <summary>
        /// Abstract method that each strategy will implement to execute its test
        /// </summary>
        public abstract void RunTest();
    }
}
