
using System;

namespace SupervisorioECA.Bridge.Communication
{
    public class CommunicationEvent
    {
        public long Id { get; set; }

        public DateTime Timestamp { get; set; }

        public string EventType { get; set; }

        public string Source { get; set; }

        public string Description { get; set; }

        public string TagId { get; set; }

        public string Quality { get; set; }

        public bool IsError { get; set; }

        public CommunicationEvent()
        {
            Id = 0;
            Timestamp = DateTime.Now;
            EventType = String.Empty;
            Source = "OPC";
            Description = String.Empty;
            TagId = String.Empty;
            Quality = String.Empty;
            IsError = false;
        }

        public static CommunicationEvent Create(
            string eventType,
            string description,
            bool isError)
        {
            CommunicationEvent communicationEvent =
                new CommunicationEvent();

            communicationEvent.Timestamp = DateTime.Now;
            communicationEvent.EventType = eventType;
            communicationEvent.Description = description;
            communicationEvent.IsError = isError;

            return communicationEvent;
        }

        public static CommunicationEvent CreateTagEvent(
            string tagId,
            string quality,
            string description)
        {
            CommunicationEvent communicationEvent =
                new CommunicationEvent();

            communicationEvent.Timestamp = DateTime.Now;
            communicationEvent.EventType = "TAG_QUALITY";
            communicationEvent.TagId = tagId;
            communicationEvent.Quality = quality;
            communicationEvent.Description = description;
            communicationEvent.IsError = true;

            return communicationEvent;
        }
    }
}
