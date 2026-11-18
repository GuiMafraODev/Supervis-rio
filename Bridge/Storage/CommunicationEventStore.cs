
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using SupervisorioECA.Bridge.Communication;

namespace SupervisorioECA.Bridge.Storage
{
    public class CommunicationEventStore
    {
        private readonly string eventsFolder;
        private readonly object syncRoot;
        private readonly JavaScriptSerializer serializer;

        private long nextId;

        public CommunicationEventStore(string eventsFolder)
        {
            if (String.IsNullOrEmpty(eventsFolder))
                throw new ArgumentException(
                    "Pasta de eventos nao informada.",
                    "eventsFolder");

            this.eventsFolder = eventsFolder;
            syncRoot = new object();

            serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;

            if (!Directory.Exists(this.eventsFolder))
                Directory.CreateDirectory(this.eventsFolder);

            nextId = LoadNextId();
        }

        public void Save(CommunicationEvent communicationEvent)
        {
            if (communicationEvent == null)
                throw new ArgumentNullException(
                    "communicationEvent");

            lock (syncRoot)
            {
                if (communicationEvent.Timestamp == DateTime.MinValue)
                    communicationEvent.Timestamp = DateTime.Now;

                if (communicationEvent.Id <= 0)
                {
                    if (nextId <= 0)
                        throw new InvalidOperationException(
                            "Nao ha mais identificadores disponiveis.");

                    communicationEvent.Id = nextId;

                    if (nextId == Int64.MaxValue)
                        nextId = 0;
                    else
                        nextId++;
                }
                else if (communicationEvent.Id >= nextId)
                {
                    if (communicationEvent.Id == Int64.MaxValue)
                        nextId = 0;
                    else
                        nextId = communicationEvent.Id + 1;
                }

                string fileName = "communication-" +
                    communicationEvent.Timestamp.ToString(
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture) +
                    ".jsonl";

                string filePath = Path.Combine(
                    eventsFolder,
                    fileName);

                string json = serializer.Serialize(
                    communicationEvent);

                using (StreamWriter writer = new StreamWriter(
                    filePath,
                    true,
                    new UTF8Encoding(false)))
                {
                    writer.WriteLine(json);
                }
            }
        }

        public void Save(
            string eventType,
            string description,
            bool isError)
        {
            CommunicationEvent communicationEvent =
                CommunicationEvent.Create(
                    eventType,
                    description,
                    isError);

            Save(communicationEvent);
        }

        public void SaveTagEvent(
            string tagId,
            string quality,
            string description)
        {
            CommunicationEvent communicationEvent =
                CommunicationEvent.CreateTagEvent(
                    tagId,
                    quality,
                    description);

            Save(communicationEvent);
        }

        private long LoadNextId()
        {
            long maxId = 0;

            string[] files = Directory.GetFiles(
                eventsFolder,
                "communication-*.jsonl");

            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    using (StreamReader reader = new StreamReader(
                        files[i],
                        Encoding.UTF8,
                        true))
                    {
                        string line;

                        while ((line = reader.ReadLine()) != null)
                        {
                            if (String.IsNullOrEmpty(line))
                                continue;

                            try
                            {
                                CommunicationEvent item =
                                    serializer.Deserialize<CommunicationEvent>(
                                        line);

                                if (item != null && item.Id > maxId)
                                    maxId = item.Id;
                            }
                            catch
                            {
                                // Ignora somente o registro corrompido.
                            }
                        }
                    }
                }
                catch
                {
                    // Um arquivo inacessivel nao interrompe a inicializacao.
                }
            }

            if (maxId == Int64.MaxValue)
                return 0;

            return maxId + 1;
        }
    }
}
