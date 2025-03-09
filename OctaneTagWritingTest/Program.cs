namespace OctaneTagWritingTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Erro: Informe o hostname do leitor como argumento.");
                return;
            }
            string hostname = args[0];
            TestManager manager = new TestManager(hostname);
            while (true)
            {
                manager.DisplayMenu();
                Console.Write("Escolha uma opção (ou 'q' para sair): ");
                string option = Console.ReadLine();
                if (option.ToLower() == "q")
                    break;
                manager.ExecuteTest(option);
            }
        }
    }
}
