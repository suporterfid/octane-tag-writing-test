using EpcListGenerator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;

namespace OctaneTagWritingTest.Helpers
{
    public static class EpcListManager
    {
        private static Queue<string> epcQueue = new Queue<string>();
        private static readonly object lockObj = new object();
        private static string lastEpc = "000000000000000000000000";
        private static long currentSerialNumber = 1;
        private static string epcHeader = "B200";
        private static string epcPlainItemCode = "99999999999999";
        private static long quantity = 1;


        public static void LoadEpcList(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("EPC file not found.", filePath);

            lock (lockObj)
            {
                epcQueue.Clear();
                string[] lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    string epc = line.Trim();
                    if (!string.IsNullOrEmpty(epc))
                        epcQueue.Enqueue(epc);
                }

                if (epcQueue.Any())
                    lastEpc = epcQueue.Last();
            }
        }

        public static void InitEpcData(string header, string code, long epcQuantity = 1)
        {
            epcHeader = header;
            epcPlainItemCode = code;
            quantity = epcQuantity;
        }

        public static string GetNextEpc()
        {
            
                
                List<string> createdEpcs = EpcListGeneratorHelper.GenerateCustomEpcList(epcHeader, epcPlainItemCode, quantity, currentSerialNumber);
                currentSerialNumber = currentSerialNumber + 1;
                string currentEpc = createdEpcs.FirstOrDefault();
                if(TagOpController.Instance.GetExistingEpc(currentEpc))
                {
                    createdEpcs = EpcListGeneratorHelper.GenerateCustomEpcList(epcHeader, epcPlainItemCode, quantity, currentSerialNumber);
                    currentSerialNumber = currentSerialNumber + 1;
                    currentEpc = createdEpcs.FirstOrDefault();
                }
                Console.WriteLine($"Returning next EPC created: {currentEpc}: SN = {currentSerialNumber}");
                return currentEpc;
                //if (epcQueue.Count > 0)
                //    return epcQueue.Dequeue();
                //else
                //{
                //    lastEpc = GenerateNewSerialNumber();
                //    return lastEpc;
                //}

            
        }

        private static string GenerateNewSerialNumber()
        {
            string prefix = lastEpc.Substring(0, lastEpc.Length - 6);
            string lastDigits = lastEpc.Substring(lastEpc.Length - 6);

            int number = int.Parse(lastDigits, System.Globalization.NumberStyles.HexNumber);
            number++;

            return prefix + number.ToString("X4");
        }

        public static List<string> LoadTidList(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("TID list file not found.", filePath);

            var tidList = new List<string>();

            lock (lockObj)
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    string tid = line.Trim();
                    if (!string.IsNullOrEmpty(tid))
                        tidList.Add(tid);
                }
                return tidList;
            }
        }
    }
}