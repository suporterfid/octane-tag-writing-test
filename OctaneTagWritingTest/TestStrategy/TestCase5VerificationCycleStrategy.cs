namespace OctaneTagWritingTest.TestStrategy
{
    using global::OctaneTagWritingTest.Helpers;
    using Impinj.OctaneSdk;
    using System;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    /// Strategy for verification cycle (write-verify)
    /// </summary>
    public class TestCase5VerificationCycleStrategy : BaseTestStrategy
    {
        private readonly Stopwatch swWrite = new Stopwatch();
        private readonly Stopwatch swVerify = new Stopwatch();
        private Tag? currentTargetTag;
        private string? expectedEpc;

        public TestCase5VerificationCycleStrategy(string hostname, string logFile)
            : base(hostname, logFile)
        {
        }

        public override void RunTest()
        {
            try
            {
                Console.WriteLine("Starting verification cycle test (write-verify)...");
                // Configure reader and load EPC list, enabling low latency
                ConfigureReader();

                // Subscribe to events for read processing and write/read operations
                reader.TagsReported += OnTagsReported;
                reader.TagOpComplete += OnTagOpComplete;
                reader.Start();

                // Create log file if it doesn't exist
                if (!File.Exists(logFile))
                    LogToCsv("Timestamp,TID,Previous_EPC,Expected_EPC,Verified_EPC,WriteTime_ms,VerifyTime_ms,Result,RSSI,AntennaPort");
         
                Console.WriteLine("Waiting for target tag read. Press Enter to stop.");
                Console.ReadLine();

                reader.Stop();
                reader.Disconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in verification test: " + ex.Message);
            }
        }

        /// <summary>
        /// Event handler called when reader reports tags
        /// </summary>
        private void OnTagsReported(ImpinjReader sender, TagReport? report)
        {
            if (report == null) return;
            
            foreach (Tag tag in report)
            {
                string tidHex = tag.Tid?.ToHexString() ?? string.Empty;
                if (string.IsNullOrEmpty(tidHex))
                    continue;

                // If result already exists for this TID, skip the event
                if (TagOpController.HasResult(tidHex))
                    continue;

                // Set target TID if not set yet
                if (!isTargetTidSet && !string.IsNullOrEmpty(tidHex))
                {
                    targetTid = tidHex;
                    isTargetTidSet = true;
                    Console.WriteLine($"Target TID set to: {tidHex}");
                }

                // Filter by target TID
                if (tidHex.Equals(targetTid, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Target tag identified: EPC={0}, TID={1}", tag.Epc.ToHexString(), tidHex);
                    // Unsubscribe to avoid multiple executions for the same tag
                    reader.TagsReported -= OnTagsReported;
                    currentTargetTag = tag;
                    TriggerWriteAndVerify(tag);
                    break;
                }
            }
        }

        /// <summary>
        /// Triggers write operation (with password update and new EPC writing) and starts verification cycle
        /// </summary>
        private void TriggerWriteAndVerify(Tag tag)
        {
            string oldEpc = tag.Epc.ToHexString();
            // Get new EPC to be written (e.g., via TagOpController)
            expectedEpc = TagOpController.GetNextEpcForTag();
            Console.WriteLine("Starting write operation for TID {0}: {1} -> {2}", tag.Tid.ToHexString(), oldEpc, expectedEpc);

            TagOpSequence seq = new TagOpSequence();
            seq.BlockWriteEnabled = true;
            seq.BlockWriteWordCount = 2;
            seq.TargetTag.MemoryBank = MemoryBank.Epc;
            seq.TargetTag.BitPointer = BitPointers.Epc;
            seq.TargetTag.Data = oldEpc;

            // Update Access password (write to Reserved bank)
            TagWriteOp updateAccessPwd = new TagWriteOp();
            updateAccessPwd.AccessPassword = null;
            updateAccessPwd.MemoryBank = MemoryBank.Reserved;
            updateAccessPwd.WordPointer = WordPointers.AccessPassword;
            updateAccessPwd.Data = TagData.FromHexString(newAccessPassword);
            seq.Ops.Add(updateAccessPwd);

            // Write operation for new EPC
            TagWriteOp writeOp = new TagWriteOp();
            writeOp.AccessPassword = TagData.FromHexString(newAccessPassword);
            writeOp.MemoryBank = MemoryBank.Epc;
            writeOp.WordPointer = WordPointers.Epc;
            writeOp.Data = TagData.FromHexString(expectedEpc);
            seq.Ops.Add(writeOp);

            swWrite.Restart();
            reader.AddOpSequence(seq);
        }

        /// <summary>
        /// After write completion, triggers read operation for verification
        /// </summary>
        private void TriggerVerificationRead(Tag tag)
        {
            TagOpSequence seq = new TagOpSequence();
            TagReadOp readOp = new TagReadOp();
            readOp.AccessPassword = TagData.FromHexString(newAccessPassword);
            readOp.MemoryBank = MemoryBank.Epc;
            readOp.WordPointer = WordPointers.Epc;
            // Calculate EPC size in words (each word = 4 hex characters)
            ushort wordCount = (ushort)(expectedEpc.Length / 4);
            readOp.WordCount = wordCount;
            seq.Ops.Add(readOp);

            swVerify.Restart();
            reader.AddOpSequence(seq);
        }

        /// <summary>
        /// Event handler called when write or read operations are completed
        /// </summary>
        private void OnTagOpComplete(ImpinjReader reader, TagOpReport? report)
        {
            if (report == null) return;
            
            foreach (TagOpResult result in report)
            {
                if (result is TagWriteOpResult writeResult)
                {
                    swWrite.Stop();
                    string tidHex = writeResult.Tag.Tid?.ToHexString() ?? "N/A";
                    Console.WriteLine("Write completed for TID {0} in {1} ms", tidHex, swWrite.ElapsedMilliseconds);
                    // After write, start read operation for verification
                    TriggerVerificationRead(currentTargetTag);
                }
                else if (result is TagReadOpResult readResult)
                {
                    swVerify.Stop();
                    string tidHex = readResult.Tag.Tid?.ToHexString() ?? "N/A";
                    string verifiedEpc = readResult.Data != null ? readResult.Data.ToHexWordString() : "N/A";
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string resultStatus = verifiedEpc.Equals(expectedEpc, StringComparison.OrdinalIgnoreCase) ? "Success" : "Failure";
                    double resultRssi = 0;
                    if (readResult.Tag.IsPcBitsPresent)
                        resultRssi = readResult.Tag.PeakRssiInDbm;
                    ushort antennaPort = 0;
                    if (readResult.Tag.IsAntennaPortNumberPresent)
                        antennaPort = readResult.Tag.AntennaPortNumber;

                    Console.WriteLine("Verification for TID {0}: EPC read = {1} ({2}) in {3} ms", tidHex, verifiedEpc, resultStatus, swVerify.ElapsedMilliseconds);
                    LogToCsv($"{timestamp},{tidHex},{currentTargetTag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{swWrite.ElapsedMilliseconds},{swVerify.ElapsedMilliseconds},{resultStatus},{resultRssi},{antennaPort}");
                    TagOpController.RecordResult(tidHex, resultStatus);
                }
            }
        }
    }
}
