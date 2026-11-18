
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SupervisorioECA.Bridge.Storage
{
    public class TrendExportService
    {
        private readonly TrendStore trendStore;

        public TrendExportService(TrendStore trendStore)
        {
            if (trendStore == null)
                throw new ArgumentNullException("trendStore");

            this.trendStore = trendStore;
        }

        public string ExportCsv(
            string tagId,
            DateTime start,
            DateTime end,
            string outputFolder)
        {
            if (String.IsNullOrEmpty(tagId))
                throw new ArgumentException(
                    "Variavel nao informada."
                );

            if (end < start)
                throw new ArgumentException(
                    "Periodo de exportacao invalido."
                );

            if (String.IsNullOrEmpty(outputFolder))
                throw new ArgumentException(
                    "Pasta de exportacao nao informada."
                );

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            IList<TrendSample> samples =
                trendStore.Query(tagId, start, end);

            string safeTag = MakeSafeFileName(tagId);

            string fileName =
                safeTag + "_" +
                start.ToString("yyyyMMdd_HHmmss") +
                "_" +
                end.ToString("yyyyMMdd_HHmmss") +
                ".csv";

            string outputPath = Path.Combine(
                outputFolder,
                fileName
            );

            StringBuilder csv = new StringBuilder();

            csv.AppendLine(
                "Data;Hora;Variavel;Valor;Qualidade"
            );

            foreach (TrendSample sample in samples)
            {
                if (sample == null || !sample.IsValid)
                    continue;

                csv.Append(
                    sample.Timestamp.ToString(
                        "dd/MM/yyyy",
                        CultureInfo.GetCultureInfo("pt-BR")
                    )
                );

                csv.Append(";");

                csv.Append(
                    sample.Timestamp.ToString(
                        "HH:mm:ss",
                        CultureInfo.InvariantCulture
                    )
                );

                csv.Append(";");

                csv.Append(EscapeCsv(tagId));

                csv.Append(";");

                csv.Append(
                    sample.Value.ToString(
                        "0.########",
                        CultureInfo.InvariantCulture
                    )
                );

                csv.Append(";");

                csv.Append(
                    EscapeCsv(sample.Quality)
                );

                csv.AppendLine();
            }

            File.WriteAllText(
                outputPath,
                csv.ToString(),
                new UTF8Encoding(true)
            );

            return outputPath;
        }

        private string MakeSafeFileName(string value)
        {
            char[] invalid =
                Path.GetInvalidFileNameChars();

            StringBuilder result = new StringBuilder();

            foreach (char c in value)
            {
                bool isInvalid = false;

                foreach (char invalidChar in invalid)
                {
                    if (c == invalidChar)
                    {
                        isInvalid = true;
                        break;
                    }
                }

                result.Append(
                    isInvalid ? '_' : c
                );
            }

            return result.ToString();
        }

        private string EscapeCsv(string value)
        {
            if (value == null)
                return "";

            if (value.Contains(";") ||
                value.Contains("\"") ||
                value.Contains("\r") ||
                value.Contains("\n"))
            {
                return "\"" +
                    value.Replace("\"", "\"\"") +
                    "\"";
            }

            return value;
        }
    }
}
