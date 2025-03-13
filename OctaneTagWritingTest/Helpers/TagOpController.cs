using Impinj.OctaneSdk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace OctaneTagWritingTest.Helpers
{
    public sealed class TagOpController
    { // Dictionary: key = TID, value = expected EPC.
        private Dictionary<string, string> expectedEpcByTid = new Dictionary<string, string>();
        // Dictionaries for recording operation results.
        private Dictionary<string, string> operationResultByTid = new Dictionary<string, string>();
        private Dictionary<string, string> operationResultWithSuccessByTid = new Dictionary<string, string>();

        private readonly object lockObj = new object();
        private HashSet<string> processedTids = new HashSet<string>();

        // Private constructor for singleton.
        private TagOpController() { }

        // Lazy singleton instance.
        private static readonly Lazy<TagOpController> _instance = new Lazy<TagOpController>(() => new TagOpController());
        public static TagOpController Instance => _instance.Value;

        // New properties to hold the current target TID and flag.
        public string LocalTargetTid { get; private set; }
        public bool IsLocalTargetTidSet { get; private set; }

        public bool HasResult(string tid)
        {
            lock (lockObj)
            {
                return operationResultByTid.ContainsKey(tid);
            }
        }

        public void RecordExpectedEpc(string tid, string expectedEpc)
        {
            lock (lockObj)
            {
                if (!expectedEpcByTid.ContainsKey(tid))
                    expectedEpcByTid.Add(tid, expectedEpc);
                else
                    expectedEpcByTid[tid] = expectedEpc;
            }
        }

        public bool IsTidProcessed(string tidHex)
        {
            lock (lockObj)
            {
                return processedTids.Contains(tidHex);
            }
        }

        public string GetExpectedEpc(string tid)
        {
            lock (lockObj)
            {
                if (expectedEpcByTid.TryGetValue(tid, out string expected))
                    return expected;
                return null;
            }
        }

        public void RecordResult(string tid, string result, bool wasSuccess)
        {
            lock (lockObj)
            {
                if (!processedTids.Contains(tid))
                {
                    processedTids.Add(tid);
                }

                if (wasSuccess)
                {
                    operationResultWithSuccessByTid[tid] = result;
                }

                if (!operationResultByTid.ContainsKey(tid))
                    operationResultByTid.Add(tid, result);
                else
                    operationResultByTid[tid] = result;
            }
        }

        public string GetNextEpcForTag()
        {
            lock (lockObj)
            {
                string nextEpc = EpcListManager.GetNextEpc();
                if (GetExistingEpc(nextEpc))
                    nextEpc = EpcListManager.GetNextEpc();
                return nextEpc;
            }
        }

        public int GetSuccessCount()
        {
            lock (lockObj)
            {
                return operationResultWithSuccessByTid.Count;
            }
        }

        public bool GetExistingEpc(string epc)
        {
            lock (lockObj)
            {
                return expectedEpcByTid.Values.Contains(epc);
            }
        }

        public void PermaLockTag(Tag tag, string accessPassword, ImpinjReader reader)
        {
            try
            {
                TagOpSequence seq = new TagOpSequence();
                // Set target tag using TID.
                seq.TargetTag.MemoryBank = MemoryBank.Tid;
                seq.TargetTag.BitPointer = 0;
                seq.TargetTag.Data = tag.Tid.ToHexString();

                // Add write operation to set the access password.
                seq.Ops.Add(new TagWriteOp
                {
                    MemoryBank = MemoryBank.Reserved,
                    WordPointer = WordPointers.AccessPassword,
                    Data = TagData.FromHexString(accessPassword)
                });

                // Create a lock operation.
                TagLockOp permalockOp = new TagLockOp
                {
                    AccessPasswordLockType = TagLockState.Permalock,
                    EpcLockType = TagLockState.Permalock,
                };

                seq.Ops.Add(permalockOp);
                reader.AddOpSequence(seq);
                Console.WriteLine($"Scheduled lock operation for TID: {tag.Tid.ToHexString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error locking tag: {ex.Message}");
            }
        }

        public void LockTag(Tag tag, string accessPassword, ImpinjReader reader)
        {
            try
            {
                TagOpSequence seq = new TagOpSequence();
                seq.TargetTag.MemoryBank = MemoryBank.Tid;
                seq.TargetTag.BitPointer = 0;
                seq.TargetTag.Data = tag.Tid.ToHexString();

                TagLockOp lockOp = new TagLockOp
                {
                    AccessPasswordLockType = TagLockState.Lock,
                    EpcLockType = TagLockState.Lock,
                };

                seq.Ops.Add(lockOp);
                seq.Ops.Add(new TagWriteOp
                {
                    MemoryBank = MemoryBank.Reserved,
                    WordPointer = WordPointers.AccessPassword,
                    Data = TagData.FromHexString(accessPassword)
                });

                reader.AddOpSequence(seq);
                Console.WriteLine($"Scheduled lock operation for TID: {tag.Tid.ToHexString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error locking tag: {ex.Message}");
            }
        }

        public void TriggerWriteAndVerify(Tag tag, string newEpcToWrite, ImpinjReader reader, CancellationToken cancellationToken, Stopwatch swWrite, string newAccessPassword, bool encodeOrDefault, ushort targetAntennaPort = 1, bool useBlockWrite = true, ushort sequenceMaxRetries = 5)
        {
            if (cancellationToken.IsCancellationRequested) return;

            string oldEpc = tag.Epc.ToHexString();
            // Set EPC data based on encoding choice.
            string epcData = encodeOrDefault ? newEpcToWrite : $"B071000000000000000000{processedTids.Count:D2}";
            string currentTid = tag.Tid.ToHexString();
            Console.WriteLine($"Attempting robust operation for TID {currentTid}: {oldEpc} -> {newEpcToWrite}");

            TagOpSequence seq = new TagOpSequence();
            seq.AntennaId = targetAntennaPort;
            seq.SequenceStopTrigger = SequenceTriggerType.None;
            seq.TargetTag.MemoryBank = MemoryBank.Tid;
            seq.TargetTag.BitPointer = 0;
            seq.TargetTag.Data = currentTid;
            if(useBlockWrite) // If block write is enabled, set the block write parameters.
            {
                seq.BlockWriteEnabled = true;
                seq.BlockWriteWordCount = 2;
                seq.BlockWriteRetryCount = 3;
            }

            if(sequenceMaxRetries > 0)
            {
                seq.SequenceStopTrigger = SequenceTriggerType.ExecutionCount;
                seq.ExecutionCount = sequenceMaxRetries;
            }
            else
            {
                seq.SequenceStopTrigger = SequenceTriggerType.None;
            }


            TagWriteOp writeOp = new TagWriteOp();
            writeOp.AccessPassword = TagData.FromHexString(newAccessPassword);
            writeOp.MemoryBank = MemoryBank.Epc;
            writeOp.WordPointer = WordPointers.Epc;
            writeOp.Data = TagData.FromHexString(epcData);
            seq.Ops.Add(writeOp);

            // If the new EPC is a different length, update the PC bits.
            if (oldEpc.Length != epcData.Length)
            {
                ushort newEpcLenWords = (ushort)(newEpcToWrite.Length / 4);
                ushort newPcBits = PcBits.AdjustPcBits(tag.PcBits, newEpcLenWords);
                Console.WriteLine("Adding a write operation to change the PC bits from :");
                Console.WriteLine("{0} to {1}\n", tag.PcBits.ToString("X4"), newPcBits.ToString("X4"));

                TagWriteOp writePc = new TagWriteOp();
                writePc.MemoryBank = MemoryBank.Epc;
                writePc.Data = TagData.FromWord(newPcBits);
                writePc.WordPointer = WordPointers.PcBits;
                seq.Ops.Add(writePc);
            }

            swWrite.Restart();
            reader.AddOpSequence(seq);
            RecordExpectedEpc(currentTid, epcData);
        }

        public void TriggerVerificationRead(Tag tag, ImpinjReader reader, CancellationToken cancellationToken, Stopwatch swVerify, string newAccessPassword)
        {
            if (cancellationToken.IsCancellationRequested) return;

            string currentTid = tag.Tid.ToHexString();
            string expectedEpc = GetExpectedEpc(currentTid);

            TagOpSequence seq = new TagOpSequence();
            seq.AntennaId = 1;
            seq.SequenceStopTrigger = SequenceTriggerType.None;
            seq.TargetTag.MemoryBank = MemoryBank.Tid;
            seq.TargetTag.BitPointer = 0;
            seq.TargetTag.Data = currentTid;
            seq.BlockWriteEnabled = true;
            seq.BlockWriteWordCount = 2;
            seq.BlockWriteRetryCount = 3;

            TagReadOp readOp = new TagReadOp();
            readOp.AccessPassword = TagData.FromHexString(newAccessPassword);
            readOp.MemoryBank = MemoryBank.Epc;
            readOp.WordPointer = WordPointers.Epc;
            ushort wordCount = (ushort)(expectedEpc.Length / 4);
            readOp.WordCount = wordCount;
            seq.Ops.Add(readOp);

            swVerify.Restart();
            reader.AddOpSequence(seq);
        }

        // New helper method to process a verified tag.
        public void HandleVerifiedTag(Tag tag, string tidHex, string expectedEpc, Stopwatch swWrite, Stopwatch swVerify, ConcurrentDictionary<string, int> retryCount, Tag currentTargetTag, string chipModel, string logFile)
        {
            // Record the successful result.
            RecordResult(tidHex, tag.Epc.ToHexString(), true);

            // Update the singleton’s target tag properties.
            LocalTargetTid = tidHex;
            IsLocalTargetTidSet = true;

            Console.WriteLine($"Tag {tidHex} already has expected EPC: {tag.Epc.ToHexString()} - Success count {GetSuccessCount()}");
            swVerify.Stop();
            string verifiedEpc = tag.Epc.ToHexString() ?? "N/A";
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string resultStatus = verifiedEpc.Equals(expectedEpc, StringComparison.OrdinalIgnoreCase) ? "Success" : "Failure";
            long writeTime = swWrite.ElapsedMilliseconds;
            long verifyTime = swVerify.ElapsedMilliseconds;
            int retries = retryCount.ContainsKey(tidHex) ? retryCount[tidHex] : 0;
            double resultRssi = 0;
            if (tag.IsPcBitsPresent)
                resultRssi = tag.PeakRssiInDbm;
            ushort antennaPort = 0;
            if (tag.IsAntennaPortNumberPresent)
                antennaPort = tag.AntennaPortNumber;

            // Log the CSV entry.
            string logLine = $"{timestamp},{tidHex},{currentTargetTag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{writeTime},{verifyTime},{resultStatus},{retries},{resultRssi},{antennaPort},{chipModel}";
            LogToCsv(logFile, logLine);
        }

        // Utility method to extract chip model information.
        public string GetChipModel(Tag tag)
        {
            string chipModel = "";
            if (tag.IsFastIdPresent)
                chipModel = tag.ModelDetails.ModelName.ToString();
            return chipModel;
        }

        /// <summary>
        /// Appends a line to the CSV log file
        /// </summary>
        /// <param name="line">The line to append to the log file</param>
        public void LogToCsv(string logFile, string line)
        {
            File.AppendAllText(logFile, line + Environment.NewLine);
        }

        public void ProcessVerificationResult(TagReadOpResult readResult, string tidHex, ConcurrentDictionary<string, int> recoveryCount, Stopwatch swWrite, Stopwatch swVerify, string logFile, ImpinjReader reader, CancellationToken cancellationToken, string newAccessPassword, int maxRecoveryAttempts)
        {
            swVerify.Stop();

            string expectedEpc = GetExpectedEpc(tidHex);
            string verifiedEpc = readResult.Data?.ToHexString() ?? "N/A";
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string resultStatus = verifiedEpc.Equals(expectedEpc, StringComparison.OrdinalIgnoreCase) ? "Success" : "Failure";

            int attempts = recoveryCount.GetOrAdd(tidHex, 0);

            if (resultStatus == "Failure" && attempts < maxRecoveryAttempts)
            {
                recoveryCount[tidHex] = attempts + 1;
                Console.WriteLine($"Verification failed, retry {recoveryCount[tidHex]} for TID {tidHex}");
                TriggerWriteAndVerify(readResult.Tag, expectedEpc, reader, cancellationToken, swWrite, newAccessPassword, true);
            }
            else
            {
                double rssi = readResult.Tag.IsPcBitsPresent ? readResult.Tag.PeakRssiInDbm : 0;
                ushort antennaPort = readResult.Tag.IsAntennaPortNumberPresent ? readResult.Tag.AntennaPortNumber : (ushort)0;

                Console.WriteLine($"Verification for TID {tidHex}: EPC read = {verifiedEpc} ({resultStatus})");

                string logLine = $"{timestamp},{tidHex},{readResult.Tag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{swWrite.ElapsedMilliseconds},{swVerify.ElapsedMilliseconds},{resultStatus},{attempts},{rssi},{antennaPort}";
                LogToCsv(logFile, logLine);

                RecordResult(tidHex, resultStatus, resultStatus == "Success");
            }
        }
    }
}
