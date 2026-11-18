
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

        public UnifiedEvent()
        {
            Id = String.Empty;
            Category = String.Empty;
            EventType = String.Empty;
            Source = String.Empty;
            TagId = String.Empty;
            Description = String.Empty;
            Quality = String.Empty;
            Timestamp = DateTime.Now;
            IsError = false;
        }
    }

    public class EventHistoryService
    {
        private readonly object syncRoot;
        private readonly string folder;
        private readonly JavaScriptSerializer serializer;

        public EventHistoryService(string folder)
        {
            if (String.IsNullOrEmpty(folder))
                throw new ArgumentException(
                    "A pasta do historico nao foi informada.",
                    "folder");

            this.folder = folder;
            syncRoot = new object();

            if (!Directory.Exists(this.folder))
                Directory.CreateDirectory(this.folder);

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

            // O tipo do evento nao determina sozinho se ele e uma falha.
            // A classificacao fica neutra ate que a regra seja definida
            // com base nos tipos reais de eventos do sistema.
            item.IsError = false;

            Add(item);
        }

        public void AddCommunicationEvent(
            Communication.CommunicationEvent source)
        {
            if (source == null)
                return;

            UnifiedEvent item = new UnifiedEvent();

            item.Id = source.Id > 0
                ? source.Id.ToString(CultureInfo.InvariantCulture)
                : String.Empty;
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

            if (item.Timestamp == DateTime.MinValue)
                item.Timestamp = DateTime.Now;

            if (String.IsNullOrEmpty(item.Id))
                item.Id = Guid.NewGuid().ToString("N");

            if (String.IsNullOrEmpty(item.Category))
                item.Category = "SYSTEM";

            if (String.IsNullOrEmpty(item.Source))
                item.Source = "Sistema";

            if (item.TagId == null)
                item.TagId = String.Empty;

            if (item.Description == null)
                item.Description = String.Empty;

            string filePath = GetDailyFile(item.Timestamp);

            lock (syncRoot)
            {
                string json = serializer.Serialize(item);

                using (StreamWriter writer = new StreamWriter(
                    filePath,
                    true,
                    System.Text.Encoding.UTF8))
                {
                    writer.WriteLine(json);
                }
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
                    "A data final nao pode ser anterior a data inicial.");

            if (maxResults <= 0)
                maxResults = 1000;

            if (maxResults > 2000)
                maxResults = 2000;

            List<UnifiedEvent> results = new List<UnifiedEvent>();

            DateTime firstDay = start.Date;
            DateTime lastDay = end.Date;

            // Le os arquivos do dia mais recente para o mais antigo.
            // Assim, quando atingir o limite, evita carregar historico antigo.
            DateTime day = lastDay;

            while (true)
            {
                string filePath = GetDailyFile(day);

                if (File.Exists(filePath))
                {
                    ReadFile(
                        filePath,
                        start,
                        end,
                        tagId,
                        maxResults,
                        results);
                }

                if (results.Count >= maxResults || day <= firstDay)
                    break;

                day = day.AddDays(-1);
            }

            results.Sort(delegate(UnifiedEvent a, UnifiedEvent b)
            {
                return b.Timestamp.CompareTo(a.Timestamp);
            });

            if (results.Count > maxResults)
                results.RemoveRange(
                    maxResults,
                    results.Count - maxResults);

            return results;
        }

        private void ReadFile(
            string filePath,
            DateTime start,
            DateTime end,
            string tagId,
            int maxResults,
            List<UnifiedEvent> results)
        {
            lock (syncRoot)
            {
                using (StreamReader reader = new StreamReader(
                    filePath,
                    System.Text.Encoding.UTF8,
                    true))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (results.Count >= maxResults)
                            break;

                        if (String.IsNullOrEmpty(line))
                            continue;

                        UnifiedEvent item;

                        try
                        {
                            item = serializer.Deserialize<UnifiedEvent>(line);
                        }
                        catch
                        {
                            // Ignora somente o registro corrompido.
                            continue;
                        }

                        if (item == null)
                            continue;

                        if (item.Timestamp < start ||
                            item.Timestamp > end)
                            continue;

                        if (!String.IsNullOrEmpty(tagId))
                        {
                            if (!String.Equals(
                                item.TagId,
                                tagId,
                                StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        results.Add(item);
                    }
                }
            }
        }

        private string GetDailyFile(DateTime date)
        {
            string fileName = "events_" +
                date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) +
                ".jsonl";

            return Path.Combine(folder, fileName);
        }
    }
}
