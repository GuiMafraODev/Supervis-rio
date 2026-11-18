
using System;

namespace SupervisorioECA.Bridge.Storage
{
    public class TrendSample
    {
        public long Id { get; set; }

        public string TagId { get; set; }

        public double Value { get; set; }

        public DateTime Timestamp { get; set; }

        public string Quality { get; set; }

        public bool IsValid { get; set; }

        public TrendSample()
        {
            Id = 0;
            TagId = String.Empty;
            Value = 0.0;
            Timestamp = DateTime.Now;
            Quality = "Unknown";
            IsValid = false;
        }

        public static TrendSample FromOpcValue(
            Opc.OpcValue opcValue)
        {
            if (opcValue == null)
                throw new ArgumentNullException("opcValue");

            TrendSample sample = new TrendSample();

            sample.TagId = opcValue.TagId;
            sample.Timestamp = opcValue.Timestamp;
            sample.Quality = opcValue.Quality;
            sample.IsValid = opcValue.IsGood();

            if (sample.IsValid)
                sample.Value = opcValue.Value;

            return sample;
        }
    }
}
