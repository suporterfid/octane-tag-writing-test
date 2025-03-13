using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace OctaneTagWritingTest.TestStrategy
{
    /// <summary>
    /// Test Strategy - Example 1: Optimal Write Speed Test
    /// This strategy measures the speed of writing new EPCs to tags
    /// </summary>
    public class TestCase1SpeedStrategy : BaseTestStrategy
    {
        public TestCase1SpeedStrategy(string hostname, string logFile, ReaderSettings readerSettings) : base(hostname, logFile, readerSettings) { }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                Console.WriteLine("Executing Speed Test Strategy...");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                ConfigureReader();

                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                if (!File.Exists(logFile))
                {
                    TagOpController.Instance.LogToCsv(logFile, "Timestamp,TID,OldEPC,NewEPC,WriteTime,Result,RSSI,AntennaPort");
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }

                Console.WriteLine("\nStopping test...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Test error: " + ex.Message);
            }
            finally
            {
                CleanupReader();
            }
        }

        private void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            if (report == null || cancellationToken.IsCancellationRequested)
                return;

            foreach (Tag tag in report.Tags)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (TagOpController.Instance.IsTidProcessed(tidHex) || TagOpController.Instance.HasResult(tidHex))
                    continue;

                string currentEpc = tag.Epc.ToHexString();
                string expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);

                if (!string.IsNullOrEmpty(expectedEpc) && expectedEpc.Equals(currentEpc, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Verified Tag {tidHex} already has EPC: {currentEpc}");
                    TagOpController.Instance.RecordResult(tidHex, currentEpc, true);
                    continue;
                }

                if (string.IsNullOrEmpty(expectedEpc))
                {
                    expectedEpc = TagOpController.Instance.GetNextEpcForTag();
                    TagOpController.Instance.RecordExpectedEpc(tidHex, expectedEpc);
                    Console.WriteLine($"Assigning new EPC to TID {tidHex}: {currentEpc} -> {expectedEpc}");
                    TagOpController.Instance.TriggerWriteAndVerify(tag, expectedEpc, reader, cancellationToken, new Stopwatch(), newAccessPassword, true);
                }
            }
        }

        private void OnTagOpComplete(ImpinjReader reader, TagOpReport report)
        {
            if (report == null || cancellationToken.IsCancellationRequested)
                return;

            foreach (TagOpResult result in report)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                if (result is TagWriteOpResult writeResult)
                {
                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string oldEpc = writeResult.Tag.Epc.ToHexString();
                    string newEpc = TagOpController.Instance.GetExpectedEpc(tidHex);
                    string resultStatus = writeResult.Result.ToString();
                    double resultRssi = writeResult.Tag.IsPcBitsPresent ? writeResult.Tag.PeakRssiInDbm : 0;
                    ushort antennaPort = writeResult.Tag.IsAntennaPortNumberPresent ? writeResult.Tag.AntennaPortNumber : (ushort)0;

                    TagOpController.Instance.LogToCsv(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{tidHex},{oldEpc},{newEpc},{sw.ElapsedMilliseconds},{resultStatus},{resultRssi},{antennaPort}");
                    TagOpController.Instance.RecordResult(tidHex, resultStatus, resultStatus == "Success");
                }
            }
        }
    }
}
