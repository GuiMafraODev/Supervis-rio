
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace SupervisorioECA.Bridge.Communication
{
    public class BridgeLogger
    {
        private readonly string filePath;
        private readonly long maxFileSizeBytes;
        private readonly int maxFiles;

        private readonly object syncRoot = new object();

        public BridgeLogger(
            string folder,
            string fileName,
            int maxFileSizeMB,
            int maxFiles)
        {
            if (String.IsNullOrEmpty(folder))
                throw new ArgumentException(
                    "Pasta de logs nao informada."
                );

            if (String.IsNullOrEmpty(fileName))
                fileName = "bridge.log";

            if (maxFileSizeMB < 1)
                maxFileSizeMB = 5;

            if (maxFiles < 1)
                maxFiles = 5;

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            filePath = Path.Combine(folder, fileName);

            maxFileSizeBytes =
                (long)maxFileSizeMB * 1024L * 1024L;

            this.maxFiles = maxFiles;
        }

        public void Info(string message)
        {
            Write("INFO", message, null);
        }

        public void Warning(string message)
        {
            Write("WARNING", message, null);
        }

        public void Error(string message)
        {
            Write("ERROR", message, null);
        }

        public void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        public void OpcConnected(string server)
        {
            Write(
                "OPC",
                "Conexao estabelecida: " + server,
                null
            );
        }

        public void OpcDisconnected(string reason)
        {
            Write(
                "OPC",
                "Conexao perdida: " + reason,
                null
            );
        }

        public void OpcReconnect(int attempt)
        {
            Write(
                "OPC",
                "Tentativa de reconexao: " +
                attempt.ToString(
                    CultureInfo.InvariantCulture
                ),
                null
            );
        }

        private void Write(
            string level,
            string message,
            Exception exception)
        {
            lock (syncRoot)
            {
                try
                {
                    RotateIfNecessary();

                    StringBuilder entry = new StringBuilder();

                    entry.Append(
                        DateTime.Now.ToString(
                            "yyyy-MM-dd HH:mm:ss.fff",
                            CultureInfo.InvariantCulture
                        )
                    );

                    entry.Append(" [");
                    entry.Append(level);
                    entry.Append("] ");

                    entry.Append(
                        message ?? ""
                    );

                    if (exception != null)
                    {
                        entry.Append(" | Exception: ");
                        entry.Append(exception.GetType().FullName);
                        entry.Append(" | ");
                        entry.Append(exception.Message);
                    }

                    entry.Append(Environment.NewLine);

                    File.AppendAllText(
                        filePath,
                        entry.ToString(),
                        new UTF8Encoding(false)
                    );
                }
                catch
                {
                    // Uma falha no logger nao deve derrubar a bridge.
                }
            }
        }

        private void RotateIfNecessary()
        {
            if (!File.Exists(filePath))
                return;

            FileInfo info = new FileInfo(filePath);

            if (info.Length < maxFileSizeBytes)
                return;

            string oldest = filePath + "." + maxFiles;

            if (File.Exists(oldest))
                File.Delete(oldest);

            for (int i = maxFiles - 1; i >= 1; i--)
            {
                string source = filePath + "." + i;
                string destination = filePath + "." + (i + 1);

                if (File.Exists(source))
                    File.Move(source, destination);
            }

            File.Move(filePath, filePath + ".1");
        }
    }
}
