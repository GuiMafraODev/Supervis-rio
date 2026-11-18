
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace SupervisorioECA.Bridge
{
    public class DiagnosticItem
    {
        public string Name { get; set; }
        public string Status { get; set; }
        public string Details { get; set; }
    }

    public class BridgeDiagnostics
    {
        private readonly string root;

        public BridgeDiagnostics(string projectRoot)
        {
            if (String.IsNullOrEmpty(projectRoot))
                throw new ArgumentException(
                    "Diretorio do projeto nao informado."
                );

            root = Path.GetFullPath(projectRoot);
        }

        public IList<DiagnosticItem> Collect(
            BridgeServiceFactory factory)
        {
            List<DiagnosticItem> result =
                new List<DiagnosticItem>();

            AddRuntimeInformation(result);
            AddDiskInformation(result);
            AddFileInformation(result);
            AddOpcInformation(result, factory);
            AddMemoryInformation(result);

            return result;
        }

        private void AddRuntimeInformation(
            List<DiagnosticItem> result)
        {
            Add(
                result,
                "Sistema operacional",
                "INFO",
                Environment.OSVersion.ToString()
            );

            Add(
                result,
                "Arquitetura do processo",
                Environment.Is64BitProcess ? "WARNING" : "OK",
                Environment.Is64BitProcess
                    ? "Processo 64 bits."
                    : "Processo 32 bits."
            );

            Add(
                result,
                ".NET Runtime",
                "INFO",
                Environment.Version.ToString()
            );

            Add(
                result,
                "Diretorio da aplicacao",
                "INFO",
                AppDomain.CurrentDomain.BaseDirectory
            );
        }

        private void AddDiskInformation(
            List<DiagnosticItem> result)
        {
            try
            {
                string driveRoot = Path.GetPathRoot(root);

                DriveInfo drive = new DriveInfo(driveRoot);

                if (!drive.IsReady)
                {
                    Add(
                        result,
                        "Armazenamento",
                        "ERROR",
                        "Unidade nao esta disponivel."
                    );

                    return;
                }

                long freeMB = drive.AvailableFreeSpace /
                    (1024 * 1024);

                long totalMB = drive.TotalSize /
                    (1024 * 1024);

                Add(
                    result,
                    "Armazenamento",
                    freeMB < 500 ? "WARNING" : "OK",
                    "Livre: " + freeMB +
                    " MB / Total: " + totalMB + " MB"
                );
            }
            catch (Exception ex)
            {
                Add(
                    result,
                    "Armazenamento",
                    "ERROR",
                    ex.Message
                );
            }
        }

        private void AddFileInformation(
            List<DiagnosticItem> result)
        {
            CheckFile(result, "Data\\Config\\appsettings.json");
            CheckFile(result, "Data\\Config\\tags.json");
            CheckFile(result, "Web\\index.html");
            CheckFile(result, "Web\\css\\style.css");
            CheckFile(result, "Web\\js\\app.js");

            CheckDirectory(result, "Data\\Trends");
            CheckDirectory(result, "Data\\Events");
            CheckDirectory(result, "Data\\Backup");
        }

        private void AddOpcInformation(
            List<DiagnosticItem> result,
            BridgeServiceFactory factory)
        {
            if (factory == null || factory.OpcClient == null)
            {
                Add(
                    result,
                    "Cliente OPC",
                    "WARNING",
                    "Cliente OPC ainda nao inicializado."
                );

                return;
            }

            Add(
                result,
                "Cliente OPC",
                factory.OpcClient.IsConnected ? "OK" : "WARNING",
                factory.OpcClient.IsConnected
                    ? "Conectado ao servidor OPC."
                    : "Aguardando conexao com RSLinx."
            );

            Add(
                result,
                "Servidor OPC",
                "INFO",
                factory.OpcClient.ServerName
            );

            Add(
                result,
                "Variaveis configuradas",
                "INFO",
                factory.Tags == null
                    ? "Nao carregadas."
                    : factory.Tags.Count.ToString()
            );
        }

        private void AddMemoryInformation(
            List<DiagnosticItem> result)
        {
            try
            {
                Process process = Process.GetCurrentProcess();

                long memoryMB =
                    process.WorkingSet64 / (1024 * 1024);

                Add(
                    result,
                    "Memoria do processo",
                    memoryMB > 700 ? "WARNING" : "OK",
                    memoryMB + " MB"
                );
            }
            catch
            {
                Add(
                    result,
                    "Memoria do processo",
                    "WARNING",
                    "Nao foi possivel consultar."
                );
            }
        }

        private void CheckFile(
            List<DiagnosticItem> result,
            string relativePath)
        {
            bool exists = File.Exists(
                Path.Combine(root, relativePath)
            );

            Add(
                result,
                relativePath,
                exists ? "OK" : "ERROR",
                exists ? "Arquivo encontrado." : "Arquivo ausente."
            );
        }

        private void CheckDirectory(
            List<DiagnosticItem> result,
            string relativePath)
        {
            bool exists = Directory.Exists(
                Path.Combine(root, relativePath)
            );

            Add(
                result,
                relativePath,
                exists ? "OK" : "ERROR",
                exists ? "Pasta encontrada." : "Pasta ausente."
            );
        }

        private void Add(
            List<DiagnosticItem> result,
            string name,
            string status,
            string details)
        {
            DiagnosticItem item = new DiagnosticItem();

            item.Name = name;
            item.Status = status;
            item.Details = details;

            result.Add(item);
        }
    }
}
