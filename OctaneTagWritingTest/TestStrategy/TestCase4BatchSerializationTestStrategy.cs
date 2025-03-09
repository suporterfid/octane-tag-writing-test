using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        // Remove duplicate Stopwatch since it's already defined in BaseTestStrategy
        private int serialCounter = 0;

        public TestCase4BatchSerializationTestStrategy(string hostname)
            : base(hostname, "TestCase4_Log.csv")
        {
        }

        public override void RunTest()
        {
            try
            {
                Console.WriteLine("Starting batch writing test with serialization...");
                // Configure the reader (connection, settings, and EPC list loading)
                ConfigureReader();

                // Subscribe to read and operation events
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                // Create log file header
                LogToCsv("Timestamp,TID,Previous_EPC,New_EPC,Serial,Result");

                Console.WriteLine("Waiting for tags to accumulate for batch. Press Enter to start batch writing...");
                Console.ReadLine();

                // Unsubscribe from tag collection to avoid duplication
                reader.TagsReported -= OnTagsReported;

                // For each accumulated tag, trigger the write operation
                foreach (Tag tag in loteTags)
                {
                    string oldEpc = tag.Epc.ToHexString();
                    string novoEpc = TagOpController.GetNextEpcForTag();
                    Console.WriteLine("Writing to tag TID={0}: {1} -> {2}", tag.Tid.ToHexString(), oldEpc, novoEpc);
                    sw.Restart(); // Start time measurement
                    TriggerWrite(tag, novoEpc);
                }

                Console.WriteLine("Batch operation triggered. Press Enter to stop.");
                Console.ReadLine();

                reader.Stop();
                reader.Disconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in batch test: " + ex.Message);
            }
        }

        /// <summary>
        /// Event handler to accumulate read tags that match the target TID filter
        /// </summary>
        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null) return;
            
            foreach (Tag tag in report)
            {
                string tidHex = tag.Tid?.ToHexString();
                
                // Set target TID if not set yet
                if (!isTargetTidSet && !string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"Target TID set to: {tidHex}");
                }

                // If TID matches target, add to batch
                if (!string.IsNullOrEmpty(tidHex) && tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    if (tag.Tid != null)
                    {
                        if (!loteTags.Exists(t => t.Tid.ToHexString().Equals(tag.Tid.ToHexString(), StringComparison.OrdinalIgnoreCase)))
                        {
                            Console.WriteLine("Tag added to batch: EPC={0}, TID={1}",
                                tag.Epc.ToHexString(), tag.Tid.ToHexString());
                            loteTags.Add(tag);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Method to trigger write operation for a specific tag
        /// </summary>
        private void TriggerWrite(Tag tag, string novoEpc)
        {
            TagOpSequence seq = new TagOpSequence();
            // Enable BlockWrite (32-bit block write with 2 words per operation)
            seq.BlockWriteEnabled = true;
            seq.BlockWriteWordCount = 2;
            seq.TargetTag.MemoryBank = MemoryBank.Epc;
            seq.TargetTag.BitPointer = BitPointers.Epc;
            seq.TargetTag.Data = tag.Epc.ToHexString();

            // Update Access password (write new password to Reserved bank)
            TagWriteOp updateAccessPwd = new TagWriteOp();
            updateAccessPwd.AccessPassword = null;
            updateAccessPwd.MemoryBank = MemoryBank.Reserved;
            updateAccessPwd.WordPointer = WordPointers.AccessPassword;
            updateAccessPwd.Data = TagData.FromHexString(newAccessPassword);
            seq.Ops.Add(updateAccessPwd);

            // Write new EPC using the new Access password
            TagWriteOp writeOp = new TagWriteOp();
            writeOp.AccessPassword = TagData.FromHexString(newAccessPassword);
            writeOp.MemoryBank = MemoryBank.Epc;
            writeOp.WordPointer = WordPointers.Epc;
            writeOp.Data = TagData.FromHexString(novoEpc);
            seq.Ops.Add(writeOp);

            // Trigger operation on reader
            reader.AddOpSequence(seq);
        }

        /// <summary>
        /// Event handler called when write operations are completed
        /// </summary>
        private void OnTagOpComplete(ImpinjReader reader, TagOpReport? report)
        {
            if (report == null) return;
            
            foreach (TagOpResult result in report)
            {
                if (result is TagWriteOpResult writeResult)
                {
                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string oldEpc = writeResult.Tag.Epc.ToHexString();
                    string newEpc = TagOpController.GetExpectedEpc(tidHex);
                    // Here, "Serial" can be included if a unique serial number is generated for each EPC
                    string res = writeResult.Result.ToString();
                    sw.Stop();
                    long writeTime = sw.ElapsedMilliseconds;

                    Console.WriteLine("Write completed for TID {0}: {1} in {2} ms", tidHex, res, writeTime);
                    LogToCsv($"{timestamp},{tidHex},{oldEpc},{newEpc},{serialCounter++},{res}");
                    TagOpController.RecordResult(tidHex, res);
                }
            }
        }
    }
}
