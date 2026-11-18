
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SupervisorioECA.Bridge.Storage
{
    public class TrendStore
    {
        private readonly string trendsFolder;
        private readonly object syncRoot = new object();

        private readonly JavaScriptSerializer serializer =
            new JavaScriptSerializer();

        public TrendStore(string trendsFolder)
        {
            if (String.IsNullOrEmpty(trendsFolder))
                throw new ArgumentException(
                    "Pasta de tendencias nao informada."
                );

            this.trendsFolder = trendsFolder;

            if (!Directory.Exists(this.trendsFolder))
                Directory.CreateDirectory(this.trendsFolder);
        }

        public void Save(TrendSample sample)
        {
            if (sample == null)
                throw new ArgumentNullException("sample");

            if (!sample.IsValid ||
                String.IsNullOrEmpty(sample.TagId))
                return;

            string filePath = GetFilePath(sample.Timestamp);

            Dictionary<string, object> record =
                new Dictionary<string, object>();

            record["tagId"] = sample.TagId;
            record["value"] = sample.Value;
            record["timestamp"] = sample.Timestamp.ToString(
                "o",
                CultureInfo.InvariantCulture
            );
            record["quality"] = sample.Quality;
            record["isValid"] = true;

            string json = serializer.Serialize(record);

            lock (syncRoot)
            {
                File.AppendAllText(
                    filePath,
                    json + Environment.NewLine,
                    new UTF8Encoding(false)
                );
            }
        }

        public void SaveBatch(IList<TrendSample> samples)
        {
            if (samples == null)
                return;

            foreach (TrendSample sample in samples)
            {
                if (sample != null && sample.IsValid)
                    Save(sample);
            }
        }

        public IList<TrendSample> Query(
            string tagId,
            DateTime start,
            DateTime end)
        {
            if (String.IsNullOrEmpty(tagId))
                throw new ArgumentException("Tag nao informada.");

            if (end < start)
                throw new ArgumentException(
                    "O horario final deve ser posterior ao inicial."
                );

            IList<TrendSample> result =
                new List<TrendSample>();

            DateTime currentDay = start.Date;
            DateTime lastDay = end.Date;

            while (currentDay <= lastDay)
            {
                string filePath = GetFilePath(currentDay);

                if (File.Exists(filePath))
                {
                    ReadFile(
                        filePath,
                        tagId,
                        start,
                        end,
                        result
                    );
                }

                currentDay = currentDay.AddDays(1);
            }

            return result;
        }

        private void ReadFile(
            string filePath,
            string tagId,
            DateTime start,
            DateTime end,
            IList<TrendSample> result)
        {
            lock (syncRoot)
            {
                using (StreamReader reader = new StreamReader(
                    filePath, Encoding.UTF8))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (String.IsNullOrEmpty(line))
                            continue;

                        try
                        {
                            Dictionary<string, object> record =
                                serializer.Deserialize<
                                    Dictionary<string, object>
                                >(line);

                            string recordTag = Convert.ToString(
                                record["tagId"],
                                CultureInfo.InvariantCulture
                            );

                            if (recordTag != tagId)
                                continue;

                            DateTime timestamp = DateTime.Parse(
                                Convert.ToString(record["timestamp"]),
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind
                            );

                            if (timestamp < start || timestamp > end)
                                continue;

                            double value = Convert.ToDouble(
                                record["value"],
                                CultureInfo.InvariantCulture
                            );

                            TrendSample sample = new TrendSample();

                            sample.TagId = recordTag;
                            sample.Value = value;
                            sample.Timestamp = timestamp;
                            sample.Quality = "Good";
                            sample.IsValid = true;

                            result.Add(sample);
                        }
                        catch
                        {
                            // Uma linha danificada nao interrompe
                            // a leitura das demais amostras.
                        }
                    }
                }
            }

            SortByTimestamp(result);
        }

        private static void SortByTimestamp(
            IList<TrendSample> samples)
        {
            List<TrendSample> sorted =
                new List<TrendSample>(samples);

            sorted.Sort(delegate(
                TrendSample a,
                TrendSample b)
            {
                return a.Timestamp.CompareTo(b.Timestamp);
            });

            for (int i = 0; i < sorted.Count; i++)
                samples[i] = sorted[i];
        }

        private string GetFilePath(DateTime timestamp)
        {
            string fileName = "trends-" +
                timestamp.ToString(
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture
                ) + ".jsonl";

            return Path.Combine(trendsFolder, fileName);
        }
    }
}
