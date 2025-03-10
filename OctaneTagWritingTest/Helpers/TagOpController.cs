using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctaneTagWritingTest.Helpers
{
    public static class TagOpController
    {
        // Dictionary: key = TID (hexadecimal), value = expected EPC after writing
        private static Dictionary<string, string> expectedEpcByTid = new Dictionary<string, string>();

        // Dictionary: key = TID (hexadecimal), value = operation result
        private static Dictionary<string, string> operationResultByTid = new Dictionary<string, string>();

        /// <summary>
        /// Checks if there is a recorded result for the given TID
        /// </summary>
        /// <param name="tid">The TID to check</param>
        /// <returns>True if a result exists for the TID, false otherwise</returns>
        public static bool HasResult(string tid)
        {
            return operationResultByTid.ContainsKey(tid);
        }

        /// <summary>
        /// Records the expected EPC for a given TID
        /// </summary>
        /// <param name="tid">The TID of the tag</param>
        /// <param name="expectedEpc">The expected EPC value after writing</param>
        public static void RecordExpectedEpc(string tid, string expectedEpc)
        {
            if (!expectedEpcByTid.ContainsKey(tid))
                expectedEpcByTid.Add(tid, expectedEpc);
            else
                expectedEpcByTid[tid] = expectedEpc;
        }

        /// <summary>
        /// Gets the expected EPC for a given TID
        /// </summary>
        /// <param name="tid">The TID to look up</param>
        /// <returns>The expected EPC value, or null if not found</returns>
        public static string GetExpectedEpc(string tid)
        {
            if (expectedEpcByTid.TryGetValue(tid, out string expected))
                return expected;
            return null;
        }

        /// <summary>
        /// Records the operation result for a given TID
        /// </summary>
        /// <param name="tid">The TID of the tag</param>
        /// <param name="result">The result of the operation</param>
        public static void RecordResult(string tid, string result)
        {
            if (!operationResultByTid.ContainsKey(tid))
                operationResultByTid.Add(tid, result);
            else
                operationResultByTid[tid] = result;
        }

        /// <summary>
        /// Returns the next EPC from the predefined list using EpcListManager
        /// </summary>
        /// <returns>The next EPC value</returns>
        public static string GetNextEpcForTag()
        {
            return EpcListManager.GetNextEpc();
        }
    }
}
