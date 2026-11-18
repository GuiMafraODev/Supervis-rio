
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace SupervisorioECA.Bridge.Storage
{
    public class UnifiedEvent
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Category { get; set; }
        public string EventType { get; set; }
        public string Source { get; set; }
        public string TagId { get; set; }
        public string Description { get; set; }
        public string Quality { get; set; }
        public bool IsError { get; set; }
    }

    public class EventHistoryService
    {
        private readonly object syncRoot = new object();
        private readonly string folder;
        private readonly JavaScriptSerializer serializer;

        public EventHistoryService(string folder)
        {
            if (String.IsNullOrEmpty(folder))
                throw new ArgumentException(
                    "Pasta de eventos nao informada."
                );

            this.folder = folder;

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;
        }

        public void AddAlarmEvent(Alarms.AlarmEvent source)
        {
            if (source == null)
                return;

            UnifiedEvent item = new UnifiedEvent();

            item.Id = source.Id;
            item.Timestamp = source.Timestamp;
            item.Category = "ALARM";
            item.EventType = source.EventType;
            item.Source = "Sistema de alarmes";
            item.TagId = source.TagId;
            item.Description = source.Description;
            item.IsError = true;

            Add(item);
        }

        public void AddCommunicationEvent(
            Communication.CommunicationEvent source)
        {
            if (source == null)
                return;

            UnifiedEvent item = new UnifiedEvent();

            item.Id = source.Id;
            item.Timestamp = source.Timestamp;
            item.Category = "COMMUNICATION";
            item.EventType = source.EventType;
            item.Source = source.Source;
            item.TagId = source.TagId;
            item.Description = source.Description;
            item.Quality = source.Quality;
            item.IsError = source.IsError;

            Add(item);
        }

        public void Add(UnifiedEvent item)
        {
            if (item == null)
                return;

            lock (syncRoot)
            {
                string file = GetDailyFile(item.Timestamp);

                string json = serializer.Serialize(item);

                File.AppendAllText(
                    file,
                    json + Environment.NewLine
                );
            }
        }

        public IList<UnifiedEvent> Query(
            DateTime start,
            DateTime end,
            string tagId,
            int maxResults)
        {
            if (end < start)
                throw new ArgumentException(
                    "Periodo de consulta invalido."
                );

            if (maxResults <= 0)
                maxResults = 1000;

            List<UnifiedEvent> result =
                new List<UnifiedEvent>();

            DateTime day = start.Date;
            DateTime lastDay = end.Date;

            while (day <= lastDay)
            {
                string file = GetDailyFile(day);

                if (File.Exists(file))
                    ReadFile(file, start, end, tagId, result);

                day = day.AddDays(1);
            }

            result.Sort(
                delegate(UnifiedEvent a, UnifiedEvent b)
                {
                    return b.Timestamp.CompareTo(a.Timestamp);
                }
            );

            if (result.Count > maxResults)
                result.RemoveRange(
                    maxResults,
                    result.Count - maxResults
                );

            return result;
        }

        private void ReadFile(
            string file,
            DateTime start,
            DateTime end,
            string tagId,
            List<UnifiedEvent> result)
        {
            lock (syncRoot)
            {
                using (StreamReader reader =
                    new StreamReader(file))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (String.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            UnifiedEvent item =
                                serializer.Deserialize<UnifiedEvent>(
                                    line
                                );

                            if (item == null)
                                continue;

                            if (item.Timestamp < start ||
                                item.Timestamp > end)
                                continue;

                            if (!String.IsNullOrEmpty(tagId) &&
                                !String.Equals(
                                    item.TagId,
                                    tagId,
                                    StringComparison.OrdinalIgnoreCase))
                                continue;

                            result.Add(item);
                        }
                        catch
                        {
                            // Ignora somente o registro danificado.
                        }
                    }
                }
            }
        }

        private string GetDailyFile(DateTime date)
        {
            return Path.Combine(
                folder,
                "events_" +
                date.ToString(
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture
                ) +
                ".jsonl"
            );
        }
    }
}
