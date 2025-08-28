﻿﻿using Impinj.OctaneSdk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Serilog;
using OctaneTagWritingTest.Infrastructure;

namespace OctaneTagWritingTest.Helpers
{
    public sealed class TagOpController
    {
        private static readonly ILogger Logger = LoggingConfiguration.CreateLogger<TagOpController>();
        // Dictionary: key = TID, value = expected EPC.
        private readonly Dictionary<string, string> expectedEpcByTid = new Dictionary<string, string>();
        // Dictionaries for recording operation results
        private readonly ConcurrentDictionary<string, string> operationResultByTid = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> operationResultWithSuccessByTid = new ConcurrentDictionary<string, string>();

        private readonly object lockObj = new object();
        private readonly HashSet<string> processedTids = new HashSet<string>();
        private readonly ConcurrentDictionary<string, string> addedWriteSequences = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> addedReadSequences = new ConcurrentDictionary<string, string>();
        
        // Fields for serial number generation
        private readonly ConcurrentDictionary<string, string> serialByTid;
        private readonly SerialGenerator serialGenerator;
        
        // Private constructor for singleton
        private TagOpController() 
        {
            serialByTid = new ConcurrentDictionary<string, string>();
            serialGenerator = new SerialGenerator();
        }

        /// <summary>
        /// Gets an existing serial for a TID or generates a new one if it doesn't exist
        /// </summary>
        /// <param name="tid">The TID to get or generate a serial for</param>
        /// <returns>A unique serial number</returns>
        public string GetOrGenerateSerial(string tid)
        {
            return serialByTid.GetOrAdd(tid, _ => 
            {
                try
                {
                    return serialGenerator.GenerateUniqueSerial();
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Warning(ex, "Failed to generate unique serial for TID {TID}, using fallback", tid);
                    // Fallback to using a timestamp-based serial if random generation fails
                    string fallbackSerial = DateTime.Now.Ticks.ToString().Substring(0, 10);
                    while (serialGenerator.IsSerialUsed(fallbackSerial))
                    {
                        fallbackSerial = DateTime.Now.Ticks.ToString().Substring(0, 10);
                    }
                    return fallbackSerial;
                }
            });
        }

        // Lazy singleton instance.
        private static readonly Lazy<TagOpController> _instance = new Lazy<TagOpController>(() => new TagOpController());
        public static TagOpController Instance => _instance.Value;

        // New properties to hold the current target TID and flag.
        public string LocalTargetTid { get; private set; }
        public bool IsLocalTargetTidSet { get; private set; }

        private readonly object fileLock = new object();

        public void CleanUp()
        {
            lock (lockObj)
            {
                try
                {
                    addedWriteSequences.Clear();
                    addedReadSequences.Clear();
                    processedTids.Clear();
                    expectedEpcByTid.Clear();
                    operationResultByTid.Clear();
                    operationResultWithSuccessByTid.Clear();
                    serialByTid.Clear();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during TagOpController cleanup");
                }
            }
        }

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
                    if(!operationResultWithSuccessByTid.ContainsKey(tid))
                    {
                        operationResultWithSuccessByTid.TryAdd(tid, result);
                        Logger.Information("Operation success recorded for TID {TID} with result {Result}. Total success count: {SuccessCount}", 
                            tid, result, operationResultWithSuccessByTid.Count());
                    }
                    
                    
                }

                if (!operationResultByTid.ContainsKey(tid))
                    operationResultByTid.TryAdd(tid, result);

            }
        }

        public string GetNextEpcForTag(string epc, string tid)
        {
            const int maxRetries = 5;
            int retryCount = 0;
            string nextEpc;

            lock (lockObj)
            {
                do
                {
                    // Get a new EPC from the manager.
                    nextEpc = EpcListManager.Instance.CreateEpcWithCurrentDigits(epc, tid);

                    // If the EPC does not already exist, break out of the loop.
                    if (!GetExistingEpc(nextEpc))
                    {
                        break;
                    }

                    retryCount++;
                }
                while (retryCount < maxRetries);

                // If after the maximum retries the EPC still exists, fall back to using SerialGenerator
                if (GetExistingEpc(nextEpc))
                {
                    Logger.Warning("EpcListManager failed to generate unique EPC for TID {TID}, falling back to SerialGenerator", tid);
                    string serial = GetOrGenerateSerial(tid);
                    // Format the serial as an EPC (maintaining the same format as the original EPC)
                    nextEpc = FormatSerialAsEpc(serial, epc);
                    
                    // Verify the generated EPC is unique
                    if (GetExistingEpc(nextEpc))
                    {
                        Logger.Error("Failed to generate unique EPC for TID {TID} even with SerialGenerator fallback", tid);
                        throw new InvalidOperationException("Failed to generate unique EPC even with SerialGenerator fallback");
                    }
                }

                return nextEpc;
            }
        }

        /// <summary>
        /// Formats a serial number as an EPC while preserving or defaulting to standard prefix
        /// </summary>
        /// <param name="serial">The serial number to format</param>
        /// <param name="originalEpc">The original EPC to extract prefix from</param>
        /// <returns>A properly formatted EPC string</returns>
        private string FormatSerialAsEpc(string serial, string originalEpc)
        {
            // Default prefix if original EPC is invalid or too short
            const string DEFAULT_PREFIX = "E280";  // Standard EPC prefix for SGTIN-96
            
            // Validate and extract prefix from original EPC
            string prefix;
            if (string.IsNullOrEmpty(originalEpc) || originalEpc.Length < 4)
            {
                prefix = DEFAULT_PREFIX;
                Logger.Warning("Invalid original EPC format for serial {Serial}, using default prefix {DefaultPrefix}", serial, DEFAULT_PREFIX);
            }
            else
            {
                // Verify the prefix is valid hexadecimal
                prefix = originalEpc.Substring(0, 4);
                if (!prefix.All(c => "0123456789ABCDEFabcdef".Contains(c)))
                {
                    prefix = DEFAULT_PREFIX;
                    Logger.Warning("Invalid EPC prefix format (non-hexadecimal) for serial {Serial}, using default prefix {DefaultPrefix}", serial, DEFAULT_PREFIX);
                }
            }

            return $"{prefix}{serial}";
        }


        public int GetTotalReadCount()
        {
            return expectedEpcByTid.Count();
        }

        public int GetSuccessCount()
        {
            return operationResultWithSuccessByTid.Count();
        }

        public bool GetExistingEpc(string epc)
        {
            lock (lockObj)
            {
                return expectedEpcByTid.Values.Contains(epc);
            }
        }

        public void PermaLockTag(Tag tag, string accessPassword, IReaderClient reader)
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

                try
                {
                    addedWriteSequences.TryAdd(seq.Id.ToString(), tag.Tid.ToHexString());
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to track permalock sequence {SequenceId} for TID {TID}", seq.Id, tag.Tid.ToHexString());
                }

                CheckAndCleanAccessSequencesOnReader(addedWriteSequences, reader);

                reader.AddOpSequence(seq);
                Logger.Information("Scheduled permalock operation for TID {TID}", tag.Tid.ToHexString());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error performing permalock operation on tag TID {TID}", tag.Tid.ToHexString());
            }
        }

        public void LockTag(Tag tag, string accessPassword, IReaderClient reader)
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

                try
                {
                    addedWriteSequences.TryAdd(seq.Id.ToString(), tag.Tid.ToHexString());
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to track lock sequence {SequenceId} for TID {TID}", seq.Id, tag.Tid.ToHexString());
                }
                CheckAndCleanAccessSequencesOnReader(addedWriteSequences, reader);

                reader.AddOpSequence(seq);
                Logger.Information("Scheduled lock operation for TID {TID}", tag.Tid.ToHexString());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error performing lock operation on tag TID {TID}", tag.Tid.ToHexString());
            }
        }

        public void CheckAndCleanAccessSequencesOnReader(ConcurrentDictionary<string, string> addedSequences, IReaderClient reader)
        {
            try
            {
                if(addedSequences.Count > 50)
                {
                    Logger.Information("Cleaning up {SequenceCount} access sequences on reader", addedSequences.Count);
                    reader.DeleteAllOpSequences();
                    addedSequences.Clear();
                    Logger.Information("Reader sequences cleaned up successfully");
                }

            }
            catch (Exception e)
            {
                Logger.Warning(e, "Warning while trying to clean up {SequenceCount} sequences", addedSequences.Count);
                
            }

        }
        /// <summary>
        /// Triggers a partial write operation that updates only the specified number of characters in the EPC while preserving the rest.
        /// </summary>
        /// <param name="tag">The tag to write to</param>
        /// <param name="newEpcToWrite">The new EPC value to partially write</param>
        /// <param name="reader">The RFID reader instance</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <param name="swWrite">Stopwatch for timing the write operation</param>
        /// <param name="newAccessPassword">Access password for the tag</param>
        /// <param name="encodeOrDefault">If true, uses the provided EPC; if false, uses a default pattern</param>
        /// <param name="charactersToWrite">Number of characters to write (minimum 8, default 14)</param>
        /// <param name="targetAntennaPort">Target antenna port (default 1)</param>
        /// <param name="useBlockWrite">Whether to use block write operations (default true)</param>
        /// <param name="sequenceMaxRetries">Maximum number of retries for the sequence (default 5)</param>
        public void TriggerPartialWriteAndVerify(Tag tag, string newEpcToWrite, IReaderClient reader, CancellationToken cancellationToken, Stopwatch swWrite, string newAccessPassword, bool encodeOrDefault, int charactersToWrite = 14, ushort targetAntennaPort = 1, bool useBlockWrite = true, ushort sequenceMaxRetries = 5)
        {
            if (cancellationToken.IsCancellationRequested) return;

            // Validate minimum characters requirement (2 words = 8 characters)
            if (charactersToWrite < 8)
            {
                throw new ArgumentException("Minimum characters to write must be 8 (2 words)", nameof(charactersToWrite));
            }

            string oldEpc = tag.Epc.ToHexString();
            
            // Take only the specified number of characters from the new EPC
            string partialNewEpc = newEpcToWrite.Substring(0, Math.Min(charactersToWrite, newEpcToWrite.Length));
            
            // Keep the remaining characters from the old EPC
            string remainingOldEpc = oldEpc.Length > charactersToWrite ? oldEpc.Substring(charactersToWrite) : "";
            
            // Set EPC data based on encoding choice
            string epcData = encodeOrDefault 
                ? partialNewEpc + remainingOldEpc 
                : $"B071000000000000000000{processedTids.Count:D2}";

            string currentTid = tag.Tid.ToHexString();
            Logger.Information("Attempting partial write operation for TID {TID}: {OldEPC} -> {NewEPC} (Writing first {CharactersToWrite} characters) - Read RSSI {RSSI}", 
                currentTid, oldEpc, epcData, charactersToWrite, tag.PeakRssiInDbm);
            
            TagOpSequence seq = new TagOpSequence();
            seq.SequenceStopTrigger = SequenceTriggerType.None;
            seq.TargetTag.MemoryBank = MemoryBank.Tid;
            seq.TargetTag.BitPointer = 0;
            seq.TargetTag.Data = currentTid;
            
            if (useBlockWrite)
            {
                seq.BlockWriteEnabled = true;
                seq.BlockWriteWordCount = 2; // Calculate words based on characters
                seq.BlockWriteRetryCount = 3;
            }

            if (sequenceMaxRetries > 0)
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

            swWrite.Restart();

            try
            {
                addedWriteSequences.TryAdd(seq.Id.ToString(), tag.Tid.ToHexString());
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to track write sequence {SequenceId} for TID {TID}", seq.Id, tag.Tid.ToHexString());
            }

            CheckAndCleanAccessSequencesOnReader(addedWriteSequences, reader);
            try
            {
                reader.AddOpSequence(seq);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error adding partial write sequence {SequenceId} for TID {TID}", seq.Id, currentTid);
                try
                {
                    Logger.Information("Cleaning up {SequenceCount} access sequences after partial write error for TID {TID}", addedWriteSequences.Count, currentTid);
                    reader.DeleteAllOpSequences();
                    addedWriteSequences.Clear();
                    reader.AddOpSequence(seq);
                    Logger.Information("Reader sequences cleaned up successfully after partial write error");
                }
                catch (Exception cleanupEx)
                {
                    Logger.Error(cleanupEx, "Error during cleanup of {SequenceCount} sequences after partial write failure", addedWriteSequences.Count);
                }
            }
            
            Logger.Information("Added partial write OpSequence {SequenceId} for TID {TID} - Current EPC: {OldEPC} -> Expected EPC {NewEPC}", 
                seq.Id, currentTid, oldEpc, epcData);

            RecordExpectedEpc(currentTid, epcData);
        }

        public void TriggerWriteAndVerify(Tag tag, string newEpcToWrite, IReaderClient reader, CancellationToken cancellationToken, Stopwatch swWrite, string newAccessPassword, bool encodeOrDefault, ushort targetAntennaPort = 1, bool useBlockWrite = true, ushort sequenceMaxRetries = 5)
        {
            if (cancellationToken.IsCancellationRequested) return;

            string oldEpc = tag.Epc.ToHexString();
            string epcData = encodeOrDefault ? newEpcToWrite : $"B071000000000000000000{processedTids.Count:D2}";
            string currentTid = tag.Tid.ToHexString();
            
            TagOpSequence seq = new TagOpSequence();
            seq.SequenceStopTrigger = SequenceTriggerType.None;
            seq.TargetTag.MemoryBank = MemoryBank.Tid;
            seq.TargetTag.BitPointer = 0;
            seq.TargetTag.Data = currentTid;
            if (useBlockWrite) // If block write is enabled, set the block write parameters.
            {
                seq.BlockWriteEnabled = true;
                seq.BlockWriteWordCount = 2;
                seq.BlockWriteRetryCount = 3;
            }

            if (sequenceMaxRetries > 0)
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

            Logger.Information("Adding write operation sequence to write new EPC {EPC} to tag TID {TID}", epcData, currentTid);
            seq.Ops.Add(writeOp);

            // If the new EPC is a different length, update the PC bits.
            if (oldEpc.Length != epcData.Length)
            {
                ushort newEpcLenWords = (ushort)(newEpcToWrite.Length / 4);
                ushort newPcBits = PcBits.AdjustPcBits(tag.PcBits, newEpcLenWords);
                Logger.Information("Adding PC bits write operation for TID {TID}: {OldPcBits} -> {NewPcBits}", 
                    currentTid, tag.PcBits.ToString("X4"), newPcBits.ToString("X4"));

                TagWriteOp writePc = new TagWriteOp();
                writePc.MemoryBank = MemoryBank.Epc;
                writePc.Data = TagData.FromWord(newPcBits);
                writePc.WordPointer = WordPointers.PcBits;
                seq.Ops.Add(writePc);
            }

            swWrite.Restart();

            try
            {
                addedWriteSequences.TryAdd(seq.Id.ToString(), tag.Tid.ToHexString());
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to track write sequence {SequenceId} for TID {TID}", seq.Id, tag.Tid.ToHexString());
            }

            CheckAndCleanAccessSequencesOnReader(addedWriteSequences, reader);
            try
            {
                reader.AddOpSequence(seq);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error adding write sequence {SequenceId} for TID {TID}", seq.Id, currentTid);
                try
                {
                    Logger.Information("Cleaning up {SequenceCount} access sequences after write error for TID {TID}", addedWriteSequences.Count, currentTid);
                    reader.DeleteAllOpSequences();
                    addedWriteSequences.Clear();
                    reader.AddOpSequence(seq);
                    Logger.Information("Reader sequences cleaned up successfully after write error");
                }
                catch (Exception cleanupEx)
                {
                    Logger.Error(cleanupEx, "Error during cleanup of {SequenceCount} sequences after write failure", addedWriteSequences.Count);
                }
            }


            //Console.WriteLine($"Added Write OpSequence {seq.Id} to TID {currentTid} - Current EPC: {oldEpc} -> Expected EPC {epcData}");
            
            

            RecordExpectedEpc(currentTid, epcData);
        }

        public void TriggerVerificationRead(Tag tag, IReaderClient reader, CancellationToken cancellationToken, Stopwatch swVerify, string newAccessPassword)
        {
            if (cancellationToken.IsCancellationRequested) return;

            string currentTid = tag.Tid.ToHexString();
            string expectedEpc = GetExpectedEpc(currentTid);

            TagOpSequence seq = new TagOpSequence();
            //seq.AntennaId = ;
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

            try
            {
                addedReadSequences.TryAdd(seq.Id.ToString(), tag.Tid.ToHexString());
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to track read sequence {SequenceId} for TID {TID}", seq.Id, tag.Tid.ToHexString());
            }

            CheckAndCleanAccessSequencesOnReader(addedReadSequences, reader);

            swVerify.Restart();
            try
            {
                reader.AddOpSequence(seq);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error adding verification read sequence {SequenceId} for TID {TID}", seq.Id, currentTid);
                try
                {
                    Logger.Information("Cleaning up {SequenceCount} read sequences after verification error for TID {TID}", addedReadSequences.Count, currentTid);
                    reader.DeleteAllOpSequences();
                    addedReadSequences.Clear();
                    reader.AddOpSequence(seq);
                    Logger.Information("Reader sequences cleaned up successfully after verification error");
                }
                catch (Exception cleanupEx)
                {
                    Logger.Error(cleanupEx, "Error during cleanup of {SequenceCount} read sequences after verification failure", addedReadSequences.Count);
                }
            }
        }

        // New helper method to process a verified tag.
        public void HandleVerifiedTag(Tag tag, string tidHex, string expectedEpc, Stopwatch swWrite, Stopwatch swVerify, ConcurrentDictionary<string, int> retryCount, Tag currentTargetTag, string chipModel, string logFile)
        {
            // Record the successful result.
            RecordResult(tidHex, tag.Epc.ToHexString(), true);

            // Update the singleton’s target tag properties.
            LocalTargetTid = tidHex;
            IsLocalTargetTidSet = true;

            Logger.Information("Tag {TID} already has expected EPC: {EPC} - Success count {SuccessCount}", 
                tidHex, tag.Epc.ToHexString(), GetSuccessCount());
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
            try
            {
                lock (fileLock)
                {
                    File.AppendAllText(logFile, line + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Unable to write data to CSV log file {LogFile}", logFile);
            }
        }

        public void ProcessVerificationResult(TagReadOpResult readResult, string tidHex, ConcurrentDictionary<string, int> recoveryCount, Stopwatch swWrite, Stopwatch swVerify, string logFile, IReaderClient reader, CancellationToken cancellationToken, string newAccessPassword, int maxRecoveryAttempts)
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
                Logger.Warning("Verification failed for TID {TID}, retry attempt {RetryCount}", tidHex, recoveryCount[tidHex]);
                // Use the same method (partial or full write) that was originally used
                if (expectedEpc.Length == readResult.Tag.Epc.ToHexString().Length)
                {
                    TriggerPartialWriteAndVerify(readResult.Tag, expectedEpc, reader, cancellationToken, swWrite, newAccessPassword, true);
                }
                else
                {
                    TriggerWriteAndVerify(readResult.Tag, expectedEpc, reader, cancellationToken, swWrite, newAccessPassword, true);
                }
            }
            else
            {
                double rssi = readResult.Tag.IsPcBitsPresent ? readResult.Tag.PeakRssiInDbm : 0;
                ushort antennaPort = readResult.Tag.IsAntennaPortNumberPresent ? readResult.Tag.AntennaPortNumber : (ushort)0;

                Logger.Information("Verification result for TID {TID}: EPC read = {VerifiedEPC} ({ResultStatus})", 
                    tidHex, verifiedEpc, resultStatus);

                string logLine = $"{timestamp},{tidHex},{readResult.Tag.Epc.ToHexString()},{expectedEpc},{verifiedEpc},{swWrite.ElapsedMilliseconds},{swVerify.ElapsedMilliseconds},{resultStatus},{attempts},{rssi},{antennaPort}";
                LogToCsv(logFile, logLine);

                RecordResult(tidHex, resultStatus, resultStatus == "Success");
            }
        }
    }
}

