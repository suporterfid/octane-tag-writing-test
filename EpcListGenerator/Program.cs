using Impinj.TagUtils;

namespace EpcListGenerator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Solicita ao usuário o GTIN
            Console.Write("Digite o GTIN: ");
            string gtin = Console.ReadLine();
            if (string.IsNullOrEmpty(gtin))
            {
                // Caso GTIN não seja informado, utiliza um valor padrão
                gtin = "80614141123458";
                Console.WriteLine($"GTIN não informado. Usando GTIN padrão: {gtin}");
            }

            // Solicita a quantidade de EPCs a serem gerados
            Console.Write("Digite a quantidade de EPCs a serem gerados: ");
            string quantidadeInput = Console.ReadLine();
            if (!long.TryParse(quantidadeInput, out long quantidade))
            {
                Console.WriteLine("Quantidade inválida.");
                return;
            }
            // Verifica se o serial gerado terá até 10 dígitos
            if (quantidade >= 10000000000)
            {
                Console.WriteLine("Quantidade muito alta: o serial number deverá ter até 10 dígitos. Informe uma quantidade menor que 10^10.");
                return;
            }

            string outputFile = "epc_list.txt";
            try
            {
                using (StreamWriter writer = new StreamWriter(outputFile))
                {
                    // Gera EPCs sequencialmente a partir de 1 até a quantidade informada
                    for (long i = 1; i <= quantidade; i++)
                    {
                        // Cria o objeto Sgtin96 com o GTIN informado e partition 6
                        Sgtin96 sgtin96 = Sgtin96.FromGTIN(gtin, 6);
                        // Atribui o serial number (convertendo para ulong)
                        sgtin96.SerialNumber = (ulong)i;
                        // Converte para o EPC (string hexadecimal no padrão GS1)
                        string targetEpc = sgtin96.ToEpc();
                        writer.WriteLine(targetEpc);
                    }
                }
                Console.WriteLine($"Arquivo '{outputFile}' criado com {quantidade} EPCs.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao criar o arquivo: " + ex.Message);
            }
        }
    }
}
