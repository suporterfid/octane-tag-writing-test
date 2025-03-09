using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctaneTagWritingTest.Helpers
{
    public static class EpcListManager
    {
        // Armazena os EPCs disponíveis numa fila
        private static Queue<string> epcQueue = new Queue<string>();
        private static readonly object lockObj = new object();

        // Carrega o arquivo de EPCs (um EPC por linha) e preenche a fila
        public static void LoadEpcList(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Arquivo de EPC não encontrado.", filePath);

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

        // Retorna o próximo EPC disponível e o remove da fila
        public static string GetNextEpc()
        {
            lock (lockObj)
            {
                if (epcQueue.Count > 0)
                    return epcQueue.Dequeue();
                else
                    throw new InvalidOperationException("Não há mais EPCs disponíveis na lista.");
            }
        }
    }
}
