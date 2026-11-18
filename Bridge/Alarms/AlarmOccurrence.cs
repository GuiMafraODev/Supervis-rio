
using System;

namespace SupervisorioECA.Bridge.Alarms
{
    public class AlarmOccurrence
    {
        public long Id { get; set; }

        public string TagId { get; set; }

        public string TagName { get; set; }

        public string AlarmType { get; set; }

        public string State { get; set; }

        public double TriggerValue { get; set; }

        public double AlarmLimit { get; set; }

        public DateTime TriggeredAt { get; set; }

        public DateTime? AcknowledgedAt { get; set; }

        public DateTime? NormalizedAt { get; set; }

        public string AcknowledgedBy { get; set; }

        public string Description { get; set; }

        public AlarmOccurrence()
        {
            Id = 0;

            TagId = String.Empty;
            TagName = String.Empty;

            AlarmType = String.Empty;
            State = "ACTIVE";

            TriggerValue = 0.0;
            AlarmLimit = 0.0;

            TriggeredAt = DateTime.Now;

            AcknowledgedAt = null;
            NormalizedAt = null;

            AcknowledgedBy = String.Empty;
            Description = String.Empty;
        }

        public bool IsActive()
        {
            return State == "ACTIVE" ||
                   State == "ACKNOWLEDGED";
        }

        public bool IsAcknowledged()
        {
            return State == "ACKNOWLEDGED";
        }

        public bool IsNormalized()
        {
            return State == "NORMALIZED";
        }

        public void Acknowledge(string user)
        {
            if (!IsActive())
                throw new InvalidOperationException(
                    "Somente alarmes ativos podem ser reconhecidos."
                );

            if (IsAcknowledged())
                return;

            State = "ACKNOWLEDGED";
            AcknowledgedAt = DateTime.Now;
            AcknowledgedBy = String.IsNullOrEmpty(user)
                ? "OPERADOR"
                : user;
        }

        public void Normalize()
        {
            if (!IsActive())
                return;

            State = "NORMALIZED";
            NormalizedAt = DateTime.Now;
        }
    }
}
