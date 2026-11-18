
using System;

namespace SupervisorioECA.Bridge.Opc
{
    public class OpcValue
    {
        public string TagId { get; set; }

        public double Value { get; set; }

        public bool HasValue { get; set; }

        public string Quality { get; set; }

        public DateTime Timestamp { get; set; }

        public bool IsConnected { get; set; }

        public string ErrorMessage { get; set; }

        public OpcValue()
        {
            TagId = String.Empty;
            Value = 0.0;
            HasValue = false;
            Quality = "Unknown";
            Timestamp = DateTime.MinValue;
            IsConnected = false;
            ErrorMessage = String.Empty;
        }

        public bool IsGood()
        {
            return HasValue &&
                   IsConnected &&
                   Quality == "Good";
        }

        public bool IsUsable()
        {
            return IsGood();
        }

        public static OpcValue CreateGood(
            string tagId,
            double value,
            DateTime timestamp)
        {
            OpcValue result = new OpcValue();

            result.TagId = tagId;
            result.Value = value;
            result.HasValue = true;
            result.Quality = "Good";
            result.Timestamp = timestamp;
            result.IsConnected = true;
            result.ErrorMessage = String.Empty;

            return result;
        }

        public static OpcValue CreateBad(
            string tagId,
            string quality,
            string errorMessage)
        {
            OpcValue result = new OpcValue();

            result.TagId = tagId;
            result.Value = 0.0;
            result.HasValue = false;
            result.Quality = String.IsNullOrEmpty(quality)
                ? "Bad"
                : quality;

            result.Timestamp = DateTime.Now;
            result.IsConnected = false;

            result.ErrorMessage = errorMessage ?? String.Empty;

            return result;
        }

        public static OpcValue CreateBad(
            string tagId,
            string quality,
            string errorMessage,
            bool connectionAvailable)
        {
            OpcValue result = new OpcValue();

            result.TagId = tagId;
            result.Value = 0.0;
            result.HasValue = false;

            result.Quality = String.IsNullOrEmpty(quality)
                ? "Bad"
                : quality;

            result.Timestamp = DateTime.Now;
            result.IsConnected = connectionAvailable;

            result.ErrorMessage = errorMessage ?? String.Empty;

            return result;
        }
    }
}
