
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
        private readonly object syncRoot = new object();

        private readonly JavaScriptSerializer serializer =
            new JavaScriptSerializer();

        public CommunicationEventStore(string eventsFolder)
        {
            if (String.IsNullOrEmpty(eventsFolder))
                throw new ArgumentException(
                    "Pasta de eventos nao informada."
                );

            this.eventsFolder = eventsFolder;

            if (!Directory.Exists(this.eventsFolder))
                Directory.CreateDirectory(this.eventsFolder);
        }

        public void Save(CommunicationEvent communicationEvent)
        {
            if (communicationEvent == null)
                throw new ArgumentNullException(
                    "communicationEvent"
                );

            string fileName = "communication-" +
                communicationEvent.Timestamp.ToString(
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture
                ) + ".jsonl";

            string filePath = Path.Combine(
                eventsFolder,
                fileName
            );

            string json = serializer.Serialize(
                communicationEvent
            );

            lock (syncRoot)
            {
                File.AppendAllText(
                    filePath,
                    json + Environment.NewLine,
                    new UTF8Encoding(false)
                );
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
                    isError
                );

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
                    description
                );

            Save(communicationEvent);
        }
    }
}
