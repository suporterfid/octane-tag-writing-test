using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctaneTagWritingTest.Helpers
{
    public static class EpcListManager
    {
        // Store EPCs in a queue
        private static Queue<string> epcQueue = new Queue<string>();
        // Store TIDs in a list
        private static List<string> tidList = new List<string>();
        private static readonly object lockObj = new object();

        /// <summary>
        /// Loads EPC list from a file (one EPC per line)
        /// </summary>
        /// <param name="filePath">Path to the file containing EPCs</param>
        /// <exception cref="FileNotFoundException">Thrown when the specified file is not found</exception>
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
            }
        }

        /// <summary>
        /// Returns the next available EPC and removes it from the queue
        /// </summary>
        /// <returns>The next EPC value</returns>
        /// <exception cref="InvalidOperationException">Thrown when no more EPCs are available</exception>
        public static string GetNextEpc()
        {
            lock (lockObj)
            {
                if (epcQueue.Count > 0)
                    return epcQueue.Dequeue();
                else
                    throw new InvalidOperationException("No more EPCs available in the list.");
            }
        }

        /// <summary>
        /// Loads TID list from a file (one TID per line) and returns as List
        /// </summary>
        /// <param name="filePath">Path to the file containing TIDs</param>
        /// <returns>List of TIDs loaded from the file</returns>
        /// <exception cref="FileNotFoundException">Thrown when the specified file is not found</exception>
        public static List<string> LoadTidList(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("TID list file not found.", filePath);

            lock (lockObj)
            {
                tidList.Clear();
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
