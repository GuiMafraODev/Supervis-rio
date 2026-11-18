
using System;
using System.IO;
using System.Threading;

namespace SupervisorioECA.Bridge
{
    public class Program
    {
        private static BridgeServiceFactory factory;
        private static readonly ManualResetEvent shutdown =
            new ManualResetEvent(false);

        public static void Main(string[] args)
        {
            Console.Title = "Supervisorio ECA";

            Console.WriteLine("====================================");
            Console.WriteLine("          SUPERVISORIO ECA");
            Console.WriteLine("====================================");
            Console.WriteLine();

            try
            {
                string root = FindProjectRoot();

                Console.WriteLine("Projeto: " + root);
                Console.WriteLine();

                BridgePreflightValidator validator =
                    new BridgePreflightValidator(root);

                Console.WriteLine("Verificando estrutura...");

                var issues = validator.Validate();

                bool hasErrors = false;

                foreach (ValidationIssue issue in issues)
                {
                    Console.WriteLine(
                        "[" + issue.Severity + "] " +
                        issue.Component + ": " +
                        issue.Message
                    );

                    if (issue.Severity == "ERROR")
                        hasErrors = true;
                }

                if (hasErrors)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        "Inicializacao cancelada. Corrija os erros."
                    );

                    WaitForExit();
                    return;
                }

                Console.WriteLine();
                Console.WriteLine("Montando servicos...");

                factory = new BridgeServiceFactory(root);
                factory.Build();

                BridgeDiagnostics diagnostics =
                    new BridgeDiagnostics(root);

                Console.WriteLine();
                Console.WriteLine("Iniciando diagnosticos...");

                var diagnosticItems =
                    diagnostics.Collect(factory);

                foreach (DiagnosticItem item in diagnosticItems)
                {
                    Console.WriteLine(
                        "[" + item.Status + "] " +
                        item.Name + ": " +
                        item.Details
                    );
                }

                Console.WriteLine();
                Console.WriteLine("Iniciando supervisório...");

                factory.Start();

                Console.WriteLine();
                Console.WriteLine("====================================");
                Console.WriteLine("SUPERVISORIO INICIADO");
                Console.WriteLine("====================================");
                Console.WriteLine();
                Console.WriteLine(
                    "Endereco: http://127.0.0.1:8765/"
                );
                Console.WriteLine();
                Console.WriteLine(
                    "Abra o Mypal neste computador e acesse o endereco."
                );
                Console.WriteLine();
                Console.WriteLine(
                    "Pressione CTRL+C para encerrar."
                );

                Console.CancelKeyPress +=
                    delegate(object sender, ConsoleCancelEventArgs e)
                    {
                        e.Cancel = true;
                        shutdown.Set();
                    };

                shutdown.WaitOne();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("ERRO NA INICIALIZACAO:");
                Console.WriteLine(ex.Message);

                Console.WriteLine();
                Console.WriteLine(
                    "Consulte Data\\Events\\bridge.log, se disponivel."
                );

                WaitForExit();
            }
            finally
            {
                if (factory != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("Encerrando servicos...");

                    try
                    {
                        factory.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "Falha no encerramento: " + ex.Message
                        );
                    }
                }

                Console.WriteLine("Aplicacao encerrada.");
            }
        }

        private static string FindProjectRoot()
        {
            string current =
                AppDomain.CurrentDomain.BaseDirectory;

            DirectoryInfo directory =
                new DirectoryInfo(current);

            while (directory != null)
            {
                string config = Path.Combine(
                    directory.FullName,
                    "Data\\Config\\appsettings.json"
                );

                string index = Path.Combine(
                    directory.FullName,
                    "Web\\index.html"
                );

                if (File.Exists(config) &&
                    File.Exists(index))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Nao foi possivel localizar a pasta do projeto."
            );
        }

        private static void WaitForExit()
        {
            Console.WriteLine();
            Console.WriteLine(
                "Pressione ENTER para fechar."
            );

            Console.ReadLine();
        }
    }
}
