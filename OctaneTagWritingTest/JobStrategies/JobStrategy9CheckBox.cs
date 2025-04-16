using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using Org.LLRP.LTK.LLRPV1.Impinj;

namespace OctaneTagWritingTest.JobStrategies
{
    /// <summary>
    /// Single-reader "CheckBox" strategy with enhanced verification and reporting
    /// </summary>
    public class JobStrategy9CheckBox : BaseTestStrategy
    {
        #region Private Fields
        private ImpinjReader writerReader;
        // Duration for tag collection (in seconds)
        private const int ReadDurationSeconds = 10;
        // Overall timeout for write operations (in seconds)
        private const int WriteTimeoutSeconds = 20;
        // Duration (in ms) for verification read phase
        private const int VerificationDurationMs = 5000;
        // The SKU must contain exactly 12 digits
        private readonly string sku;
        // Thread-safe collection to track tag data (TID -> TagProcessingInfo)
        private readonly ConcurrentDictionary<string, TagProcessingInfo> tagInfoMap = new();
        // Separate dictionary for verification phase
        private readonly ConcurrentDictionary<string, Tag> verificationTags = new();
        // Flag to indicate if the GPI processing is already running
        private int gpiProcessingFlag = 0;
        // Tag collection active flag
        private bool isCollectingTags = true;
        // Verification phase active flag
        private bool isVerificationPhase = false;
        // Cancellation token source for operation timeouts
        private CancellationTokenSource operationCts;
        // Timer for periodic status updates
        private Timer statusUpdateTimer;
        // Summary reports
        private StringBuilder processSummary = new();
        #endregion

        #region Constructor
        public JobStrategy9CheckBox(string hostname, string logFile, Dictionary<string, ReaderSettings> readerSettings, string sku)
            : base(hostname, logFile, readerSettings)
        {
            if (string.IsNullOrEmpty(sku) || sku.Length != 12)
            {
                throw new ArgumentException("SKU must contain exactly 12 digits.", nameof(sku));
            }
            this.sku = sku;
            TagOpController.Instance.CleanUp();
        }
        #endregion

        #region Main Execution Methods
        public override void RunJob(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                operationCts = new CancellationTokenSource();

                Console.WriteLine("=== Single Reader CheckBox Test ===");
                Console.WriteLine("GPI events on Port 1 will trigger tag collection, write, and verification.");
                Console.WriteLine("Press 'q' to cancel.");

                // Configure the reader
                ConfigureWriterReader();

                // Initialize log file if needed
                if (!File.Exists(logFile))
                {
                    LogToCsv("Timestamp,TID,Original_EPC,Expected_EPC,Verified_EPC,WriteTime_ms,VerifyTime_ms,Result,RSSI,AntennaPort");
                }

                // Start periodic status updates
                statusUpdateTimer = new Timer(UpdateStatusDisplay, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

                // Keep the application running until the user cancels
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).KeyChar == 'q')
                    {
                        break;
                    }
                    Thread.Sleep(200); // Reduce CPU usage
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in JobStrategy9CheckBox: {ex.Message}");
            }
            finally
            {
                statusUpdateTimer?.Dispose();
                operationCts?.Dispose();
                CleanupWriterReader();
            }
        }

        private void UpdateStatusDisplay(object state)
        {
            int totalTags = tagInfoMap.Count;
            int verifiedTags = tagInfoMap.Values.Count(info => info.IsVerified);
            int successTags = tagInfoMap.Values.Count(info => info.VerificationSuccess);
            int failedTags = tagInfoMap.Values.Count(info => info.IsVerified && !info.VerificationSuccess);

            Console.WriteLine($"Status: {DateTime.Now.ToString("HH:mm:ss")} - Tags: {totalTags} | Verified: {verifiedTags} | Success: {successTags} | Failed: {failedTags}");

            // Update console title
            try
            {
                Console.Title = $"CheckBox: {totalTags}|{verifiedTags}|{successTags}|{failedTags}";
            }
            catch { /* Ignore title update errors */ }
        }
        #endregion

        #region Reader Configuration Methods
        private void ConfigureWriterReader()
        {
            var writerSettings = GetSettingsForRole("writer");

            if (writerReader == null)
                writerReader = new ImpinjReader();

            if (!writerReader.IsConnected)
            {
                writerReader.Connect(writerSettings.Hostname);
            }

            writerReader.ApplyDefaultSettings();

            var settingsToApply = writerReader.QueryDefaultSettings();
            settingsToApply.Report.IncludeFastId = writerSettings.IncludeFastId;
            settingsToApply.Report.IncludePeakRssi = writerSettings.IncludePeakRssi;
            settingsToApply.Report.IncludePcBits = true;
            settingsToApply.Report.IncludeAntennaPortNumber = writerSettings.IncludeAntennaPortNumber;
            settingsToApply.Report.Mode = (ReportMode)Enum.Parse(typeof(ReportMode), writerSettings.ReportMode);
            settingsToApply.RfMode = (uint)writerSettings.RfMode;
            settingsToApply.SearchMode = (SearchMode)Enum.Parse(typeof(SearchMode), writerSettings.SearchMode);
            settingsToApply.Session = (ushort)writerSettings.Session;

            // Configure all four antennas
            settingsToApply.Antennas.DisableAll();
            for (ushort port = 1; port <= 4; port++)
            {
                var antenna = settingsToApply.Antennas.GetAntenna(port);
                antenna.IsEnabled = true;
                antenna.TxPowerInDbm = writerSettings.TxPowerInDbm;
                antenna.MaxRxSensitivity = writerSettings.MaxRxSensitivity;
                antenna.RxSensitivityInDbm = writerSettings.RxSensitivityInDbm;
            }

            // Configure GPI for port 1
            var gpi = settingsToApply.Gpis.GetGpi(1);
            gpi.IsEnabled = true;
            gpi.DebounceInMs = 50;

            // Set GPI triggers for starting and stopping
            settingsToApply.AutoStart.Mode = AutoStartMode.GpiTrigger;
            settingsToApply.AutoStart.GpiPortNumber = 1;
            settingsToApply.AutoStart.GpiLevel = true;
            settingsToApply.AutoStop.Mode = AutoStopMode.GpiTrigger;
            settingsToApply.AutoStop.GpiPortNumber = 1;
            settingsToApply.AutoStop.GpiLevel = false;

            // Attach event handlers
            writerReader.GpiChanged += OnGpiEvent;
            writerReader.TagsReported += OnTagsReported;
            writerReader.TagOpComplete += OnTagOpComplete;

            // Enable low latency reporting
            EnableLowLatencyReporting(settingsToApply, writerReader);
        }

        private void EnableLowLatencyReporting(Settings settings, ImpinjReader reader)
        {
            var addRoSpecMessage = reader.BuildAddROSpecMessage(settings);
            var setReaderConfigMessage = reader.BuildSetReaderConfigMessage(settings);
            setReaderConfigMessage.AddCustomParameter(new PARAM_ImpinjReportBufferConfiguration()
            {
                ReportBufferMode = ENUM_ImpinjReportBufferMode.Low_Latency
            });
            reader.ApplySettings(setReaderConfigMessage, addRoSpecMessage);
        }
        #endregion

        #region GPI Event Handling
        private async void OnGpiEvent(ImpinjReader sender, GpiEvent e)
        {
            if (e.PortNumber != 1)
                return;

            if (e.State)
            {
                // Use Interlocked.CompareExchange to ensure only one processing instance runs
                if (Interlocked.CompareExchange(ref gpiProcessingFlag, 1, 0) == 0)
                {
                    Console.WriteLine("GPI Port 1 is TRUE - initiating tag processing workflow");
                    processSummary.Clear();

                    try
                    {
                        // Create a new CTS for this operation cycle
                        if (operationCts != null)
                        {
                            operationCts.Dispose();
                        }
                        operationCts = new CancellationTokenSource();

                        // Begin tag collection
                        bool collectionConfirmed = await WaitForReadTagsAsync();
                        if (collectionConfirmed)
                        {
                            // Write operations
                            await EncodeReadTagsAsync();

                            // Verification phase
                            await VerifyWrittenTagsAsync();

                            // Display summary
                            DisplayProcessSummary();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Operation was canceled.");
                        DisplayProcessSummary();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during tag processing: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("GPI Port 1 event received while processing already in progress. Ignoring duplicate trigger.");
                }
            }
            else
            {
                // When GPI state becomes false, reset the processing flag
                Console.WriteLine("GPI Port 1 is FALSE - resetting processing flag");
                Interlocked.Exchange(ref gpiProcessingFlag, 0);
            }
        }
        #endregion

        #region Tag Collection Phase
        private async Task<bool> WaitForReadTagsAsync()
        {
            // Reset for new collection
            tagInfoMap.Clear();
            verificationTags.Clear();
            isCollectingTags = true;

            Console.WriteLine($"Collecting tags for {ReadDurationSeconds} seconds...");
            try
            {
                // Set up linked token source to handle both cancellation methods
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, operationCts.Token);

                await Task.Delay(ReadDurationSeconds * 1000, linkedCts.Token);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Tag collection was canceled");
                return false;
            }

            // End tag collection
            isCollectingTags = false;

            int tagCount = tagInfoMap.Count;
            if (tagCount == 0)
            {
                Console.WriteLine("No tags were collected. Operation canceled.");
                return false;
            }

            Console.WriteLine($"Tag collection ended. Total tags collected: {tagCount}. Confirm? (y/n)");
            string confirmation = Console.ReadLine();
            if (!confirmation.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Operation canceled by user");
                return false;
            }

            // Add collection summary to process report
            processSummary.AppendLine("=== TAG COLLECTION SUMMARY ===");
            processSummary.AppendLine($"Tags collected: {tagCount}");
            foreach (var kvp in tagInfoMap)
            {
                processSummary.AppendLine($"TID: {kvp.Key}, Original EPC: {kvp.Value.OriginalEpc}");
            }

            return true;
        }
        #endregion

        #region Tag Writing Phase
        private async Task EncodeReadTagsAsync()
        {
            Console.WriteLine("Starting write phase...");
            processSummary.AppendLine("\n=== TAG WRITING PHASE ===");

            Stopwatch globalWriteTimer = Stopwatch.StartNew();
            int processedCount = 0;
            int tagCount = tagInfoMap.Count;

            // Process each collected tag
            foreach (var kvp in tagInfoMap)
            {
                if (operationCts.Token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Write phase was canceled");
                }

                // Check for global timeout
                if (globalWriteTimer.Elapsed.TotalSeconds > WriteTimeoutSeconds)
                {
                    Console.WriteLine("Global write timeout reached");
                    processSummary.AppendLine($"Write phase timed out after {WriteTimeoutSeconds} seconds");
                    break;
                }

                string tid = kvp.Key;
                TagProcessingInfo info = kvp.Value;

                if (!info.Tag.IsFastIdPresent)
                {
                    // Skip tags without FastID capability (they might be invalid for write operations)
                    processSummary.AppendLine($"Skipped TID {tid} - FastID not present");
                    continue;
                }

                // Generate new EPC based on SKU and TID
                string newEpc = GenerateNewEpc(sku, tid);
                info.ExpectedEpc = newEpc;
                info.WriteTimer.Restart();

                // Record in TagOpController for tracking
                TagOpController.Instance.RecordExpectedEpc(tid, newEpc);

                // Trigger write operation
                TagOpController.Instance.TriggerWriteAndVerify(
                    info.Tag,
                    newEpc,
                    writerReader,
                    operationCts.Token,
                    info.WriteTimer,
                    newAccessPassword,
                    true,
                    1,  // Use antenna 1 for writing
                    true,
                    3   // 3 retry attempts
                );

                processedCount++;
                Console.WriteLine($"Writing EPC {newEpc} to tag {tid} ({processedCount}/{tagCount})");

                // Brief delay between tags to avoid overwhelming the reader
                await Task.Delay(100, operationCts.Token);
            }

            globalWriteTimer.Stop();
            processSummary.AppendLine($"Write phase completed in {globalWriteTimer.ElapsedMilliseconds}ms");
            processSummary.AppendLine($"Tags processed: {processedCount}/{tagCount}");

            // If all writes complete instantly, allow time for TagOpComplete events to be processed
            await Task.Delay(Math.Min(1000, VerificationDurationMs / 5), operationCts.Token);

            // Check if all tags have been written successfully
            int writtenCount = tagInfoMap.Values.Count(info => info.IsWritten);
            if (writtenCount == tagCount)
            {
                Console.WriteLine($"All {tagCount} tags were written successfully");
            }
            else
            {
                Console.WriteLine($"{writtenCount}/{tagCount} tags were written");
            }
        }
        #endregion

        #region Tag Verification Phase
        private async Task VerifyWrittenTagsAsync()
        {
            Console.WriteLine("Starting verification phase...");
            processSummary.AppendLine("\n=== TAG VERIFICATION PHASE ===");

            // Prepare for verification
            verificationTags.Clear();
            isVerificationPhase = true;
            int tagCount = tagInfoMap.Count;
            int expectedToVerify = tagInfoMap.Values.Count(info => info.IsWritten);

            try
            {
                // Set up cancelable verification phase
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, operationCts.Token);

                // Create a dynamic verification timeout
                Stopwatch verifyPhaseTimer = Stopwatch.StartNew();
                bool earlyCompletion = false;

                // Wait for verification with periodic checks for early completion
                for (int elapsed = 0; elapsed < VerificationDurationMs; elapsed += 250)
                {
                    // Check if we should terminate early (all tags verified)
                    int verifiedCount = tagInfoMap.Values.Count(info => info.IsVerified);

                    if (verifiedCount >= expectedToVerify)
                    {
                        earlyCompletion = true;
                        Console.WriteLine("Early verification completion - all tags verified");
                        break;
                    }

                    // Wait a bit longer
                    await Task.Delay(250, linkedCts.Token);
                }

                verifyPhaseTimer.Stop();

                if (earlyCompletion)
                {
                    processSummary.AppendLine($"Verification completed early in {verifyPhaseTimer.ElapsedMilliseconds}ms");
                }
                else
                {
                    processSummary.AppendLine($"Verification completed in {verifyPhaseTimer.ElapsedMilliseconds}ms (full duration)");
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Verification phase was canceled");
                processSummary.AppendLine("Verification phase was canceled");
            }
            finally
            {
                isVerificationPhase = false;
            }

            // Process verification results
            int successCount = 0;
            int failCount = 0;

            foreach (var kvp in tagInfoMap)
            {
                string tid = kvp.Key;
                TagProcessingInfo info = kvp.Value;

                if (info.IsVerified)
                {
                    if (info.VerificationSuccess)
                    {
                        successCount++;
                        processSummary.AppendLine($"SUCCESS: TID {tid} - EPC: {info.VerifiedEpc}");
                    }
                    else
                    {
                        failCount++;
                        processSummary.AppendLine($"FAILURE: TID {tid} - Expected: {info.ExpectedEpc}, Read: {info.VerifiedEpc}");
                    }

                    // Log to CSV
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string status = info.VerificationSuccess ? "Success" : "Failure";
                    double rssi = info.Tag.IsPeakRssiInDbmPresent ? info.Tag.PeakRssiInDbm : 0;
                    ushort antenna = info.Tag.IsAntennaPortNumberPresent ? info.Tag.AntennaPortNumber : (ushort)0;

                    LogToCsv($"{timestamp},{tid},{info.OriginalEpc},{info.ExpectedEpc},{info.VerifiedEpc}," +
                             $"{info.WriteTimer.ElapsedMilliseconds},{info.VerifyTimer.ElapsedMilliseconds}," +
                             $"{status},{rssi},{antenna}");

                    TagOpController.Instance.RecordResult(tid, status, info.VerificationSuccess);
                }
                else if (info.IsWritten)
                {
                    // Tag was written but not verified
                    processSummary.AppendLine($"NOT VERIFIED: TID {tid} - EPC write attempted but verification failed");
                }
                else
                {
                    // Tag was not written
                    processSummary.AppendLine($"NOT PROCESSED: TID {tid} - No write operation completed");
                }
            }

            processSummary.AppendLine($"\nVerification Summary: {successCount} successful, {failCount} failed, " +
                                    $"{tagCount - (successCount + failCount)} not verified");

            Console.WriteLine($"Verification complete: {successCount}/{tagCount} tags verified successfully");
        }
        #endregion

        #region Tag Event Handlers
        private void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            if (report == null || cancellationToken.IsCancellationRequested)
                return;

            foreach (var tag in report.Tags)
            {
                string tid = tag.Tid?.ToHexString() ?? "";
                string epc = tag.Epc?.ToHexString() ?? "";

                if (string.IsNullOrEmpty(tid))
                    continue;

                if (isVerificationPhase)
                {
                    // In verification phase, check reported EPCs against expected values
                    if (tagInfoMap.TryGetValue(tid, out TagProcessingInfo info) && info.IsWritten)
                    {
                        // Tag is part of our collection and write was attempted
                        info.VerifyTimer.Stop();
                        info.VerifiedEpc = epc;
                        info.IsVerified = true;
                        info.VerificationSuccess = epc.Equals(info.ExpectedEpc, StringComparison.OrdinalIgnoreCase);

                        Console.WriteLine($"Verification read: TID: {tid}, EPC: {epc}, Result: {(info.VerificationSuccess ? "SUCCESS" : "FAILURE")}");
                    }

                    // Also update the verification tags collection
                    verificationTags.TryAdd(tid, tag);
                }
                else if (isCollectingTags)
                {
                    // In collection phase, store unique TIDs and their current EPCs
                    if (!tagInfoMap.ContainsKey(tid))
                    {
                        var info = new TagProcessingInfo
                        {
                            Tag = tag,
                            OriginalEpc = epc
                        };

                        tagInfoMap.TryAdd(tid, info);
                        Console.WriteLine($"Collected tag: TID: {tid}, EPC: {epc}, Antenna: {tag.AntennaPortNumber}");
                    }
                }
            }
        }

        private void OnTagOpComplete(ImpinjReader sender, TagOpReport report)
        {
            if (report == null || IsCancellationRequested())
                return;

            foreach (TagOpResult result in report)
            {
                string tidHex = result.Tag.Tid?.ToHexString() ?? "N/A";

                if (result is TagWriteOpResult writeResult)
                {
                    swWriteTimers[tidHex].Stop();

                    if (writeResult.Result == WriteResultStatus.Success)
                    {
                        Console.WriteLine($"Write operation succeeded for TID: {tidHex}, initiating verification.");

                        // Clean up operation sequence after successful processing
                        try
                        {
                            sender.DeleteOpSequence(result.SequenceId);
                        }
                        catch (Exception)
                        {
                            // Ignore any errors during sequence deletion
                        }

                        TagOpController.Instance.TriggerVerificationRead(writeResult.Tag, reader, cancellationToken,
                            swVerifyTimers.GetOrAdd(tidHex, _ => new Stopwatch()), newAccessPassword);
                    }
                    else
                    {
                        LogResult(tidHex, writeResult.Tag.Epc.ToHexString(), TagOpController.Instance.GetExpectedEpc(tidHex), "N/A",
                                  "Write Failure", retryCount.GetOrAdd(tidHex, 0), writeResult.Tag);
                        TagOpController.Instance.RecordResult(tidHex, "Write Error", false);

                        // Clean up operation sequence even after failure
                        try
                        {
                            sender.DeleteOpSequence(result.SequenceId);
                        }
                        catch (Exception)
                        {
                            // Ignore any errors during sequence deletion
                        }
                    }
                }
                else if (result is TagReadOpResult readResult)
                {
                    swVerifyTimers[tidHex].Stop();

                    string verifiedEpc = readResult.Data?.ToHexString() ?? "N/A";
                    string expectedEpc = TagOpController.Instance.GetExpectedEpc(tidHex);
                    string resultStatus = verifiedEpc.Equals(expectedEpc, StringComparison.OrdinalIgnoreCase) ? "Success" : "Verification Failure";

                    int retries = retryCount.GetOrAdd(tidHex, 0);

                    if (resultStatus == "Verification Failure" && retries < maxRetries)
                    {
                        retryCount[tidHex] = retries + 1;
                        Console.WriteLine($"Verification failed, retry {retryCount[tidHex]} for TID {tidHex}");

                        // Clean up current operation sequence before initiating a new one
                        try
                        {
                            sender.DeleteOpSequence(result.SequenceId);
                        }
                        catch (Exception)
                        {
                            // Ignore any errors during sequence deletion
                        }

                        TagOpController.Instance.TriggerWriteAndVerify(readResult.Tag, expectedEpc, reader, cancellationToken,
                            swWriteTimers.GetOrAdd(tidHex, _ => new Stopwatch()), newAccessPassword, true);
                    }
                    else
                    {
                        LogResult(tidHex, readResult.Tag.Epc.ToHexString(), expectedEpc, verifiedEpc,
                                  resultStatus, retries, readResult.Tag);
                        TagOpController.Instance.RecordResult(tidHex, resultStatus, resultStatus == "Success");

                        // Clean up operation sequence after final processing
                        try
                        {
                            sender.DeleteOpSequence(result.SequenceId);
                        }
                        catch (Exception)
                        {
                            // Ignore any errors during sequence deletion
                        }
                    }
                }
            }
        }
        #endregion

        #region Helper Methods
        private string GenerateNewEpc(string sku, string tid)
        {
            // E7 + 12-digit SKU + last 10 characters of TID
            string tidSuffix = tid.Length >= 10 ? tid.Substring(tid.Length - 10) : tid.PadLeft(10, '0');
            return "E7" + sku + tidSuffix;
        }

        private void DisplayProcessSummary()
        {
            if (processSummary.Length > 0)
            {
                Console.WriteLine("\n\n========== PROCESS SUMMARY ==========");
                Console.WriteLine(processSummary.ToString());
                Console.WriteLine("======================================\n");
            }
        }

        private void LogToCsv(string line)
        {
            TagOpController.Instance.LogToCsv(logFile, line);
        }

        private void CleanupWriterReader()
        {
            try
            {
                Console.WriteLine("Cleanup resources...");
                if (writerReader != null && writerReader.IsConnected)
                {
                    writerReader.TagsReported -= OnTagsReported;
                    writerReader.TagOpComplete -= OnTagOpComplete;
                    writerReader.GpiChanged -= OnGpiEvent;
                }

                tagInfoMap.Clear();
                verificationTags.Clear();
                TagOpController.Instance.CleanUp();
                Console.WriteLine("Cleanup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }
        #endregion
    }

    
}