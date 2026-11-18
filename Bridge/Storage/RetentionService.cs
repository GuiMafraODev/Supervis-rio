
using System;
using System.Collections.Generic;
using System.IO;

namespace SupervisorioECA.Bridge.Storage
{
    public class RetentionService
    {
        private readonly string trendsFolder;
        private readonly string eventsFolder;
        private readonly int retentionDays;

        private readonly object syncRoot = new object();

        public RetentionService(
            string trendsFolder,
            string eventsFolder,
            int retentionDays)
        {
            if (String.IsNullOrEmpty(trendsFolder))
                throw new ArgumentException(
                    "Pasta de tendencias nao informada."
                );

            if (String.IsNullOrEmpty(eventsFolder))
                throw new ArgumentException(
                    "Pasta de eventos nao informada."
                );

            if (retentionDays < 1)
                throw new ArgumentException(
                    "Retencao deve ser de pelo menos um dia."
                );

            this.trendsFolder = trendsFolder;
            this.eventsFolder = eventsFolder;
            this.retentionDays = retentionDays;
        }

        public RetentionResult Execute()
        {
            RetentionResult result = new RetentionResult();

            lock (syncRoot)
            {
                DateTime cutoff = DateTime.Now.Date.AddDays(
                    -retentionDays
                );

                result.CutoffDate = cutoff;

                CleanFolder(
                    trendsFolder,
                    "trends_*.jsonl",
                    cutoff,
                    result
                );

                CleanFolder(
                    eventsFolder,
                    "alarms_*.jsonl",
                    cutoff,
                    result
                );
            }

            return result;
        }

        private void CleanFolder(
            string folder,
            string pattern,
            DateTime cutoff,
            RetentionResult result)
        {
            if (!Directory.Exists(folder))
                return;

            string[] files = Directory.GetFiles(
                folder,
                pattern,
                SearchOption.TopDirectoryOnly
            );

            foreach (string file in files)
            {
                try
                {
                    string fileName = Path.GetFileName(file);

                    if (fileName.EndsWith(
                        ".tmp",
                        StringComparison.OrdinalIgnoreCase))
                        continue;

                    DateTime fileDate;

                    if (!TryGetDateFromFileName(
                        fileName,
                        out fileDate))
                    {
                        result.SkippedFiles++;
                        continue;
                    }

                    if (fileDate.Date >= cutoff.Date)
                        continue;

                    FileInfo info = new FileInfo(file);

                    if (!info.Exists)
                        continue;

                    File.Delete(file);

                    result.DeletedFiles++;
                    result.DeletedBytes += info.Length;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(
                        Path.GetFileName(file) +
                        ": " + ex.Message
                    );
                }
            }
        }

        private bool TryGetDateFromFileName(
            string fileName,
            out DateTime fileDate)
        {
            fileDate = DateTime.MinValue;

            if (String.IsNullOrEmpty(fileName))
                return false;

            string datePart;

            if (fileName.StartsWith(
                "trends_",
                StringComparison.OrdinalIgnoreCase))
            {
                datePart = fileName.Substring(7, 8);
            }
            else if (fileName.StartsWith(
                "alarms_",
                StringComparison.OrdinalIgnoreCase))
            {
                datePart = fileName.Substring(7, 8);
            }
            else
            {
                return false;
            }

            if (datePart.Length != 8)
                return false;

            return DateTime.TryParseExact(
                datePart,
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out fileDate
            );
        }
    }

    public class RetentionResult
    {
        public DateTime CutoffDate { get; set; }

        public int DeletedFiles { get; set; }
        public int SkippedFiles { get; set; }

        public long DeletedBytes { get; set; }

        public IList<string> Errors { get; set; }

        public RetentionResult()
        {
            DeletedFiles = 0;
            SkippedFiles = 0;
            DeletedBytes = 0;

            Errors = new List<string>();
        }
    }
}
