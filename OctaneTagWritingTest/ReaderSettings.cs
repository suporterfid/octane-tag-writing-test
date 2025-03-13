﻿using System;
using System.IO;
using System.Text.Json;

namespace OctaneTagWritingTest
{
    public class ReaderSettings
    {
        public string Hostname { get; set; }
        public string LogFile { get; set; }
        public bool IncludeFastId { get; set; }
        public bool IncludePeakRssi { get; set; }
        public bool IncludeAntennaPortNumber { get; set; }
        public string ReportMode { get; set; }
        public int RfMode { get; set; }

        public int AntennaPort { get; set; }

        public int TxPowerInDbm { get; set; }
        public bool MaxRxSensitivity { get; set; }
        public int RxSensitivityInDbm { get; set; }
        public string SearchMode { get; set; }
        public int Session { get; set; }
        public string MemoryBank { get; set; }
        public int BitPointer { get; set; }
        public string TagMask { get; set; }
        public int BitCount { get; set; }
        public string FilterOp { get; set; }
        public string FilterMode { get; set; }

        public void Save(string filePath)
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public static ReaderSettings Load(string filePath)
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<ReaderSettings>(json);
            }
            return null;
        }
    }
}
