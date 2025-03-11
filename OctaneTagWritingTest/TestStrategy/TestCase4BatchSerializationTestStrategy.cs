using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Impinj.OctaneSdk;
using OctaneTagWritingTest.Helpers;

namespace OctaneTagWritingTest.TestStrategy
{
    /// <summary>
    /// Test strategy for batch writing with serialization
    /// </summary>
    public class TestCase4BatchSerializationTestStrategy : BaseTestStrategy
    {
        private readonly List<Tag> loteTags = new List<Tag>();
        private int serialCounter = 0;
        private readonly object batchLock = new object();

        public TestCase4BatchSerializationTestStrategy(string hostname, string logFile) 
            : base(hostname, logFile)
        {
        }

        public override void RunTest(CancellationToken cancellationToken = default)
        {
            try
            {
                this.cancellationToken = cancellationToken;
                Console.WriteLine("Starting batch writing test with serialization...");
                Console.WriteLine("Press 'q' to stop the test and return to menu.");

                // Configure the reader (connection, settings, and EPC list loading)
                ConfigureReader();

                // Subscribe to read and operation events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                // Ensure log file exists
                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,OldEPC,NewEPC,SerialCounter,WriteTime,Result,RSSI,AntennaPort");

                // Keep running until cancellation is requested
                while (!IsCancellationRequested())
                {
                    // Process accumulated tags in batches
                    ProcessAccumulatedTags();
                    Thread.Sleep(1000); // Wait before processing next batch
                }

                Console.WriteLine("\nStopping test...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in batch test: " + ex.Message);
                LogToCsv($"ERROR,{DateTime.Now:yyyy-MM-dd HH:mm:ss},{ex.Message}");
            }
            finally
            {
                CleanupReader();
            }
        }

        private void ProcessAccumulatedTags()
        {
            if (IsCancellationRequested()) return;

            List<Tag> tagsToProcess;
            lock (batchLock)
            {
                tagsToProcess = new List<Tag>(loteTags);
                loteTags.Clear();
            }

            if (tagsToProcess.Count > 0)
            {
                Console.WriteLine($"\nProcessing batch of {tagsToProcess.Count} tags...");

                foreach (Tag tag in tagsToProcess)
                {
                    if (IsCancellationRequested()) return;

                    if (tag.Epc == null || tag.Tid == null)
                    {
                        Console.WriteLine("Skipping tag with null EPC or TID");
                        continue;
                    }

                    string oldEpc = tag.Epc.ToHexString();
                    string novoEpc = TagOpController.GetNextEpcForTag();
                    Console.WriteLine($"Writing to tag TID={tag.Tid.ToHexString()}: {oldEpc} -> {novoEpc}");
                    sw.Restart();
                    TriggerWrite(tag, novoEpc);
                }
            }
        }

        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null || IsCancellationRequested()) return;
            
            foreach (Tag tag in report)
            {
                if (IsCancellationRequested()) return;

                string tidHex = tag.Tid?.ToHexString();
                
                // Reset target TID for each new tag to enable continuous encoding
                if (!string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"\nNew target TID found: {tidHex}");
                }

                // If TID matches target, add to batch
                if (!string.IsNullOrEmpty(tidHex) && tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase) && tag.Tid != null)
                {
                    lock (batchLock)
                    {
                        if (!loteTags.Exists(t => t.Tid.ToHexString().Equals(tag.Tid.ToHexString(), StringComparison.OrdinalIgnoreCase)))
                        {
                            if (tag.Epc == null)
                            {
                                Console.WriteLine($"Skipping tag with TID {tag.Tid.ToHexString()} due to null EPC");
                                continue;
                            }
                            Console.WriteLine($"Tag added to batch: EPC={tag.Epc.ToHexString()}, TID={tag.Tid.ToHexString()}");
                            loteTags.Add(tag);
                        }
                    }
                }
            }
        }

        private void TriggerWrite(Tag tag, string novoEpc)
        {
            if (IsCancellationRequested()) return;

            if (tag?.Epc == null)
            {
                Console.WriteLine("Cannot trigger write for tag with null EPC");
                return;
            }

            TagOpSequence seq = new TagOpSequence();
            seq.BlockWriteEnabled = true;
            seq.BlockWriteWordCount = 2;
            seq.TargetTag.MemoryBank = MemoryBank.Epc;
            seq.TargetTag.BitPointer = BitPointers.Epc;
            seq.TargetTag.Data = tag.Epc.ToHexString();

            try
            {
                // Update Access password
                TagWriteOp updateAccessPwd = new TagWriteOp();
                updateAccessPwd.AccessPassword = null;
                updateAccessPwd.MemoryBank = MemoryBank.Reserved;
                updateAccessPwd.WordPointer = WordPointers.AccessPassword;
                updateAccessPwd.Data = TagData.FromHexString(newAccessPassword);
                seq.Ops.Add(updateAccessPwd);

                // Write new EPC
                TagWriteOp writeOp = new TagWriteOp();
                writeOp.AccessPassword = TagData.FromHexString(newAccessPassword);
                writeOp.MemoryBank = MemoryBank.Epc;
                writeOp.WordPointer = WordPointers.Epc;
                writeOp.Data = TagData.FromHexString(novoEpc);
                seq.Ops.Add(writeOp);

                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                TagOpController.RecordExpectedEpc(tidHex, novoEpc);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error preparing write operation: {ex.Message}");
                return;
            }

            reader.AddOpSequence(seq);
        }

        private void OnTagOpComplete(ImpinjReader reader, TagOpReport? report)
        {
            if (report == null || IsCancellationRequested()) return;
            
            foreach (TagOpResult result in report)
            {
                if (IsCancellationRequested()) return;

                if (result is TagWriteOpResult writeResult)
                {
                    sw.Stop();
                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string oldEpc = writeResult.Tag?.Epc?.ToHexString() ?? "Unknown";
                    string newEpc = TagOpController.GetExpectedEpc(tidHex);
                    long writeTime = sw.ElapsedMilliseconds;
                    string res = writeResult.Result.ToString();
                    double resultRssi = 0;
                    if (writeResult.Tag.IsPcBitsPresent)
                        resultRssi = writeResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (writeResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = writeResult.Tag.AntennaPortNumber;

                    Console.WriteLine($"Write completed for TID {tidHex}: {res} in {writeTime} ms");
                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{serialCounter++},{writeTime},{res},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, res);

                    // Reset isTargetTidSet to allow processing of new tags
                    isTargetTidSet = false;
                }
            }
        }
    }
}
