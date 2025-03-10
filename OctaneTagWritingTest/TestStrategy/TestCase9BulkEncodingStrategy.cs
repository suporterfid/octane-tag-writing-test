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
    /// Tests the ability to encode multiple tags in bulk with either all F's or all 0's
    /// </summary>
    public class TestCase9BulkEncodingStrategy : BaseTestStrategy
    {
        private const ushort EPC_ENCODED_OP_ID = 100;
        private int tagsNumber;
        private int encodeRemaining;
        private List<TagOpSequence> loadedSequences;
        private bool encodeOrDefault;

        public TestCase9BulkEncodingStrategy(string hostname, string logFile) : base(hostname, logFile) 
        {
            loadedSequences = new List<TagOpSequence>();
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
            settings.Antennas.GetAntenna(1).RxSensitivityInDbm = -90;

            // Enable low latency mode
            EnableLowLatencyReporting(settings);

            return settings;
        }

        public override void RunTest()
        {
            try
            {
                Console.WriteLine("Executing Bulk Encoding Test Strategy...");
                
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
                List<string> tidList;
                try 
                {
                    tidList = EpcListManager.LoadTidList(tidListPath);
                    tagsNumber = tidList.Count;
                    encodeRemaining = tagsNumber;
                    Console.WriteLine($"Successfully loaded {tagsNumber} TIDs from {tidListPath}");
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"Error: TID list file '{tidListPath}' not found.");
                    Console.WriteLine("Please create a file named 'tid_list.txt' with one TID per line.");
                    Console.WriteLine("Example TID format: E28011B020005A422D220337");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading TID list: {ex.Message}");
                    return;
                }

                Console.WriteLine("\nAdding write operations to reader...");
                PrepareTagOperationSequences(tidList);

                // Subscribe to tag operation events
                reader.TagOpComplete += OnTagOpComplete;

                // Start timing and reading
                sw.Start();
                Console.WriteLine("Starting reader...");
                Console.WriteLine("------------------------------------------");
                reader.Start();

                Console.WriteLine("Bulk encoding test running. Press Enter to stop.");
                Console.ReadLine();

                reader.Stop();
                reader.Disconnect();
            }
            catch (OctaneSdkException e)
            {
                Console.WriteLine("Octane SDK exception: {0}", e.Message);
                if (reader.IsConnected)
                {
                    try
                    {
                        reader.Stop();
                        reader.Disconnect();
                    }
                    catch (Exception disconnectEx)
                    {
                        Console.WriteLine("Error during reader cleanup: " + disconnectEx.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception : {0}", e.Message);
                if (reader.IsConnected)
                {
                    try
                    {
                        reader.Stop();
                        reader.Disconnect();
                    }
                    catch (Exception disconnectEx)
                    {
                        Console.WriteLine("Error during reader cleanup: " + disconnectEx.Message);
                    }
                }
            }
            finally
            {
                if (sw.IsRunning)
                {
                    sw.Stop();
                    sw.Reset();
                }
            }
        }

        private void PrepareTagOperationSequences(List<string> tidList)
        {
            ushort id = 1;
            foreach (var tid in tidList)
            {
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

                // Set EPC data based on encoding choice
                string epcData = encodeOrDefault ? 
                    $"FFFFFFFFFFFFFFFFFFFFFF{id:D2}" : 
                    $"0000000000000000000000{id:D2}";
                writeEpc.Data = TagData.FromHexString(epcData);

                seq.Ops.Add(writeEpc);
                loadedSequences.Add(seq);
                reader.AddOpSequence(seq);
                id++;
            }
        }

        private void OnTagOpComplete(ImpinjReader reader, TagOpReport report)
        {
            foreach (TagOpResult result in report)
            {
                if (result is TagWriteOpResult writeResult)
                {
                    if (writeResult.Result == WriteResultStatus.Success)
                    {
                        reader.DeleteOpSequence(writeResult.SequenceId);
                        Console.WriteLine("Tag operation {0} completed for EPC {1}, removing from queue...", 
                            writeResult.SequenceId, writeResult.Tag.Epc);
                        
                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        LogToCsv($"{timestamp},{writeResult.Tag.Tid?.ToHexString()},{writeResult.Tag.Epc},{writeResult.Result}");
                        
                        encodeRemaining--;
                    }

                    if (encodeRemaining == 0)
                    {
                        sw.Stop();
                        Console.WriteLine("------------------------------------------");
                        Console.WriteLine("Total test time to encode {0} tags: {1}", tagsNumber, sw.Elapsed);
                        Console.WriteLine("------------------------------------------\n");
                        Console.WriteLine("Press enter to continue");

                        sw.Reset();
                        reader.Stop();
                    }
                }
            }
        }
    }
}
