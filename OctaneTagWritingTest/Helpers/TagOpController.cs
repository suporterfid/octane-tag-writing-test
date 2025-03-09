using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctaneTagWritingTest.Helpers
{
    public static class TagOpController
    {
        // Dicionário: chave = TID (hexadecimal), valor = EPC esperado após a gravação
        private static Dictionary<string, string> expectedEpcByTid = new Dictionary<string, string>();

        // Dicionário: chave = TID (hexadecimal), valor = resultado da operação
        private static Dictionary<string, string> operationResultByTid = new Dictionary<string, string>();

        public static bool HasResult(string tid)
        {
            return operationResultByTid.ContainsKey(tid);
        }

        public static void RecordExpectedEpc(string tid, string expectedEpc)
        {
            if (!expectedEpcByTid.ContainsKey(tid))
                expectedEpcByTid.Add(tid, expectedEpc);
            else
                expectedEpcByTid[tid] = expectedEpc;
        }

        public static string GetExpectedEpc(string tid)
        {
            if (expectedEpcByTid.TryGetValue(tid, out string expected))
                return expected;
            return null;
        }

        public static void RecordResult(string tid, string result)
        {
            if (!operationResultByTid.ContainsKey(tid))
                operationResultByTid.Add(tid, result);
            else
                operationResultByTid[tid] = result;
        }

        // Retorna o próximo EPC da lista pré-definida utilizando o EpcListManager
        public static string GetNextEpcForTag()
        {
            return EpcListManager.GetNextEpc();
        }
    }
}
