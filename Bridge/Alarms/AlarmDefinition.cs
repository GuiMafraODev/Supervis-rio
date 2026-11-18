
using System;

namespace SupervisorioECA.Bridge.Alarms
{
    public class AlarmDefinition
    {
        public string TagId { get; set; }

        public bool HighAlarmEnabled { get; set; }

        public double HighLimit { get; set; }

        public bool LowAlarmEnabled { get; set; }

        public double LowLimit { get; set; }

        public double Hysteresis { get; set; }

        public bool Enabled { get; set; }

        public DateTime LastModified { get; set; }

        public string ModifiedBy { get; set; }

        public AlarmDefinition()
        {
            TagId = String.Empty;

            HighAlarmEnabled = false;
            HighLimit = 0.0;

            LowAlarmEnabled = false;
            LowLimit = 0.0;

            Hysteresis = 0.05;

            Enabled = true;

            LastModified = DateTime.MinValue;
            ModifiedBy = String.Empty;
        }

        public bool IsValid()
        {
            if (String.IsNullOrEmpty(TagId))
                return false;

            if (Hysteresis < 0.0)
                return false;

            if (HighAlarmEnabled &&
                LowAlarmEnabled &&
                LowLimit >= HighLimit)
                return false;

            return true;
        }
    }
}
