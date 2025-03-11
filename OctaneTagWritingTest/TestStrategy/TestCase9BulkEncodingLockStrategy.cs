using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;
using Org.LLRP.LTK.LLRPV1;
using Org.LLRP.LTK.LLRPV1.Impinj;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OctaneTagWritingTest.TestStrategy
{
    /// <summary>
    /// Test Strategy - Example 9: Bulk Tag Encoding Test
    /// Tests the ability to encode multiple tags in bulk with either a pre-encoded EPC or default state.
    /// Falls back to individual tag processing if TID list is unavailable.
    /// </summary>
    public class TestCase9BulkEncodingLockStrategy : BaseTestStrategy
    {
        private const ushort EPC_ENCODED_OP_ID = 100;
        private int tagsNumber;
        private int encodeRemaining;
        private List<TagOpSequence> loadedSequences;
        private bool encodeOrDefault;
        
        // New fields for fallback mode
        private bool isFallbackMode;
        private HashSet<string> processedTids;
        private readonly object lockObject = new object();

        public TestCase9BulkEncodingLockStrategy(string hostname, string logFile) : base(hostname, logFile) 
        {
            loadedSequences = new List<TagOpSequence>();
            processedTids = new HashSet<string>();
        }

        protected override Settings ConfigureReader()
        {
            reader.Connect(hostname);

            Settings settings = reader.QueryDefaultSettings();

            // Configure reader settings
            settings.Report.IncludeAntennaPortNumber = true;
            settings.Report.Mode = ReportMode.Individual;
            settings.RfMode = 2;
            settings.SearchMode = SearchMode.SingleTarget;
            settings.Session = 1;
            settings.TagPopulationEstimate = 32;

            // Enable antenna #1 only
            settings.Antennas.DisableAll();
            settings.Antennas.GetAntenna(1).IsEnabled = true;
            settings.Antennas.GetAntenna(1).TxPowerInDbm = 30;
            settings.Antennas.GetAntenna(1).MaxRxSensitivity = true;

            // Enable low latency mode
            EnableLowLatencyReporting(settings);

            return settings;
        }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("Executing Bulk Encoding Test Strategy...");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");
                
                // Get user input for encoding type
                Console.WriteLine("Do you want ENCODE (EPC as Fs) or DEFAULT (EPC as 0s) state?");
                Console.WriteLine("\n(1)ENCODED\n(0)DEFAULT\n");
                var ansys = Console.ReadKey();
                encodeOrDefault = ansys.KeyChar == '1';
                Console.WriteLine("\n");

                // Configure reader and settings
                Settings settings = ConfigureReader();
                
                // Load TID list from file
                Console.WriteLine("Loading TID list from file...");
                string tidListPath = "tid_list.txt";
                List<string> tidList = null;
                bool loadSuccess = false;

                try 
                {
                    tidList = EpcListManager.LoadTidList(tidListPath);
                    loadSuccess = tidList != null && tidList.Count > 0;
                    
                    if (loadSuccess)
                    {
                        tagsNumber = tidList.Count;
                        encodeRemaining = tagsNumber;
                        Console.WriteLine($"Successfully loaded {tagsNumber} TIDs from {tidListPath}");
                        isFallbackMode = false;
                    }
                    else
                    {
                        Console.WriteLine("TID list is empty or null. Switching to fallback mode.");
                        isFallbackMode = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading TID list: {ex.Message}");
                    Console.WriteLine("Switching to fallback mode - processing tags individually.");
                    isFallbackMode = true;
                }

                // Subscribe to events based on mode
                reader.TagOpComplete += OnTagOpComplete;
                
                if (isFallbackMode)
                {
                    Console.WriteLine("Operating in fallback mode - processing tags as they are detected.");
                    reader.TagsReported += OnTagsReported;
                }
                else
                {
                    Console.WriteLine("\nAdding write operations to reader...");
                    PrepareTagOperationSequences(tidList);
                }

                // Start timing and reading
                sw.Start();
                Console.WriteLine("Starting reader...");
                Console.WriteLine("------------------------------------------");
                reader.Start();

                // Create log file if it doesn't exist
                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,Previous_EPC,Expected_EPC,Verified_EPC,WriteTime_ms,VerifyTime_ms,Result,RecoveryAttempts,RSSI,AntennaPort");

                // Keep the test running until cancellation is requested
                while (!IsCancellationRequested())
                {
                    Thread.Sleep(100); // Small delay to prevent CPU spinning
                }

                Console.WriteLine("\nStopping test...");
            }
            catch (OctaneSdkException e)
            {
                Console.WriteLine("Octane SDK exception: {0}", e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception : {0}", e.Message);
            }
            finally
            {
                CleanupReader();
                if (sw.IsRunning)
                {
                    sw.Stop();
                    sw.Reset();
                }
            }
        }

        private void PrepareTagOperationSequences(List<string> tidList)
        {
            if (tidList == null || IsCancellationRequested()) return;

            ushort id = 1;
            foreach (var tid in tidList)
            {
                if (IsCancellationRequested()) return;

                TagOpSequence seq = new TagOpSequence();
                seq.Id = id;
                seq.AntennaId = 1;
                seq.SequenceStopTrigger = SequenceTriggerType.None;
                seq.TargetTag.MemoryBank = MemoryBank.Tid;
                seq.TargetTag.BitPointer = 0;
                seq.TargetTag.Data = tid;
                seq.BlockWriteEnabled = true;
                seq.BlockWriteWordCount = 2;
                seq.BlockWriteRetryCount = 3;

                // Create tag write operation
                TagWriteOp writeEpc = new TagWriteOp();
                writeEpc.Id = (ushort)(EPC_ENCODED_OP_ID + id);
                writeEpc.MemoryBank = MemoryBank.Epc;
                writeEpc.WordPointer = WordPointers.Epc;
                writeEpc.AccessPassword = TagData.FromHexString(newAccessPassword);

                // Get new EPC via helper
                var expectedEpc = TagOpController.GetNextEpcForTag();

                // Set EPC data based on encoding choice
                string epcData = encodeOrDefault ?
                    expectedEpc : 
                    $"B071000000000000000000{id:D2}";
                writeEpc.Data = TagData.FromHexString(epcData);

                seq.Ops.Add(writeEpc);
                loadedSequences.Add(seq);
                reader.AddOpSequence(seq);
                id++;
            }
        }

        private void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            if (!isFallbackMode || report == null || IsCancellationRequested()) return;

            foreach (Tag tag in report.Tags)
            {
                if (IsCancellationRequested()) return;

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;

                if (string.IsNullOrEmpty(tidHex)) continue;

                lock (lockObject)
                {
                    if (processedTids.Contains(tidHex))
                    {
                        Console.WriteLine($"Skipping already processed TID {tidHex}.");
                        continue;
                    }

                    string expectedEpc = TagOpController.GetExpectedEpc(tidHex);
                    string currentEpc = tag.Epc.ToHexString();

                    if (!string.IsNullOrEmpty(expectedEpc) && expectedEpc.Equals(currentEpc, StringComparison.OrdinalIgnoreCase))
                    {
                        TagOpController.RecordResult(tidHex, currentEpc);
                        Console.WriteLine($"Tag {tidHex} already has expected EPC: {currentEpc}");
                        continue;
                    }

                    lock (lockObject)
                    {
                        if (!processedTids.Contains(tidHex))
                        {
                            processedTids.Add(tidHex);
                            targetTid = tidHex;
                            isTargetTidSet = true;
                            Console.WriteLine($"\nNew target TID found: {tidHex}");

                            string newEpcToWrite = TagOpController.GetNextEpcForTag();
                            TagOpController.RecordExpectedEpc(tidHex, newEpcToWrite);

                            ProcessNewTag(tag, newEpcToWrite);
                        }
                    }
                }
            }
        }

        private void ProcessNewTag(Tag tag, string newEpcToWrite)
        {
            if (IsCancellationRequested()) return;

            try
            {
                TagOpSequence seq = new TagOpSequence();
                seq.TargetTag.MemoryBank = MemoryBank.Tid;
                seq.TargetTag.BitPointer = 0;
                seq.TargetTag.Data = tag.Tid.ToHexString();
                seq.BlockWriteEnabled = true;
                seq.BlockWriteWordCount = 2;
                seq.BlockWriteRetryCount = 3;

                // Create tag write operation
                TagWriteOp writeEpc = new TagWriteOp();
                writeEpc.MemoryBank = MemoryBank.Epc;
                writeEpc.WordPointer = WordPointers.Epc;
                writeEpc.AccessPassword = TagData.FromHexString(newAccessPassword);

               

                // Set EPC data based on encoding choice
                string epcData = encodeOrDefault ?
                    newEpcToWrite :
                    $"B071000000000000000000{processedTids.Count:D2}";
                writeEpc.Data = TagData.FromHexString(epcData);

                seq.Ops.Add(writeEpc);
                reader.AddOpSequence(seq);

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                TagOpController.RecordExpectedEpc(tidHex, newEpcToWrite);

                Console.WriteLine($"Scheduled write operation for TID: {tag.Tid.ToHexString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing new tag: {ex.Message}");
            }
        }

        private void OnTagOpComplete(ImpinjReader reader, TagOpReport report)
        {
            foreach (TagOpResult result in report)
            {
                if (result is TagWriteOpResult writeResult)
                {
                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    double resultRssi = writeResult.Tag.IsPcBitsPresent ? writeResult.Tag.PeakRssiInDbm : 0;
                    ushort antennaPort = writeResult.Tag.IsAntennaPortNumberPresent ? writeResult.Tag.AntennaPortNumber : (ushort)0;

                    if (writeResult.Result == WriteResultStatus.Success)
                    {
                        Console.WriteLine($"Successfully wrote EPC for TID: {tidHex}");
                        LogToCsv($"{timestamp},{tidHex},{writeResult.Tag.Epc},Success,{resultRssi},{antennaPort}");
                        TagOpController.RecordResult(tidHex, writeResult.Tag.Epc.ToHexString());
                        LockTag(writeResult.Tag, "11112222"); // Example access password
                    }
                    else
                    {
                        Console.WriteLine($"Failed to write EPC for TID: {tidHex}, Status: {writeResult.Result}");
                        LogToCsv($"{timestamp},{tidHex},{writeResult.Tag.Epc},Failed-{writeResult.Result},{resultRssi},{antennaPort}");
                        TagOpController.RecordResult(tidHex, "Write Error");
                    }
                }

                if (result is TagLockOpResult lockResult)
                {
                    string tidHex = lockResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    double resultRssi = lockResult.Tag.IsPcBitsPresent ? lockResult.Tag.PeakRssiInDbm : 0;
                    ushort antennaPort = lockResult.Tag.IsAntennaPortNumberPresent ? lockResult.Tag.AntennaPortNumber : (ushort)0;

                    if (lockResult.Result == LockResultStatus.Success)
                    {
                        Console.WriteLine($"Successfully locked tag with TID: {tidHex}");
                        LogToCsv($"{timestamp},{tidHex},{lockResult.Tag.Epc},LockSuccess,{resultRssi},{antennaPort}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to lock tag with TID: {tidHex}, Status: {lockResult.Result}");
                        LogToCsv($"{timestamp},{tidHex},{lockResult.Tag.Epc},LockFailed-{lockResult.Result},{resultRssi},{antennaPort}");
                    }
                }
            }
        }


        private void LockTag(Tag tag, string accessPassword)
        {
            try
            {
                TagOpSequence seq = new TagOpSequence();

                // Set target tag using TID
                seq.TargetTag.MemoryBank = MemoryBank.Tid;
                seq.TargetTag.BitPointer = 0;
                seq.TargetTag.Data = tag.Tid.ToHexString();

                // Create a lock operation
                TagLockOp lockOp = new TagLockOp
                {
                    AccessPasswordLockType = TagLockState.Lock,
                    EpcLockType = TagLockState.Lock,
                };

                // Add lock operation to sequence
                seq.Ops.Add(lockOp);

                // Set access password for locking
                seq.Ops.Add(new TagWriteOp
                {
                    MemoryBank = MemoryBank.Reserved,
                    WordPointer = WordPointers.AccessPassword,
                    Data = TagData.FromHexString(accessPassword)
                });

                // Add sequence to the reader
                reader.AddOpSequence(seq);

                Console.WriteLine($"Scheduled lock operation for TID: {tag.Tid.ToHexString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error locking tag: {ex.Message}");
            }
        }
    }
}
