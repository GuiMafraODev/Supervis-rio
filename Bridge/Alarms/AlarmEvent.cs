
using System;

namespace SupervisorioECA.Bridge.Alarms
{
    public class AlarmEvent
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }

        public string EventType { get; set; }

        public string TagId { get; set; }
        public string TagName { get; set; }

        public string AlarmType { get; set; }
        public string Description { get; set; }

        public double? Value { get; set; }
        public double? AlarmLimit { get; set; }

        public string User { get; set; }

        public double? PreviousLimit { get; set; }
        public double? NewLimit { get; set; }

        public string State { get; set; }

        public AlarmEvent()
        {
            Id = Guid.NewGuid().ToString("N");
            Timestamp = DateTime.Now;

            EventType = "";
            TagId = "";
            TagName = "";
            AlarmType = "";
            Description = "";
            User = "";
            State = "";
        }

        public static AlarmEvent Create(
            string eventType,
            string tagId,
            string tagName,
            string alarmType,
            string description,
            double? value,
            double? alarmLimit,
            string user,
            string state)
        {
            AlarmEvent alarmEvent = new AlarmEvent();

            alarmEvent.EventType = eventType;
            alarmEvent.TagId = tagId;
            alarmEvent.TagName = tagName;
            alarmEvent.AlarmType = alarmType;
            alarmEvent.Description = description;
            alarmEvent.Value = value;
            alarmEvent.AlarmLimit = alarmLimit;
            alarmEvent.User = user;
            alarmEvent.State = state;

            return alarmEvent;
        }

        public static AlarmEvent CreateLimitChange(
            string tagId,
            string tagName,
            string alarmType,
            double? previousLimit,
            double? newLimit,
            string user)
        {
            AlarmEvent alarmEvent = new AlarmEvent();

            alarmEvent.EventType = "LIMIT_CHANGED";
            alarmEvent.TagId = tagId;
            alarmEvent.TagName = tagName;
            alarmEvent.AlarmType = alarmType;
            alarmEvent.Description =
                "Limite de alarme alterado.";

            alarmEvent.PreviousLimit = previousLimit;
            alarmEvent.NewLimit = newLimit;
            alarmEvent.User = user;

            return alarmEvent;
        }
    }
}
