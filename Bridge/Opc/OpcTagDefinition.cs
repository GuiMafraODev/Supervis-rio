
using System;

namespace SupervisorioECA.Bridge.Opc
{
    public class OpcTagDefinition
    {
        public string TagId { get; set; }

        public string Name { get; set; }

        public string Topic { get; set; }

        public string AccessPath { get; set; }

        public string ItemId { get; set; }

        public string Source { get; set; }

        public string DataType { get; set; }

        public double Scale { get; set; }

        public string Unit { get; set; }

        public double Minimum { get; set; }

        public double Maximum { get; set; }

        public bool Enabled { get; set; }

        public OpcTagDefinition()
        {
            TagId = String.Empty;
            Name = String.Empty;
            Topic = String.Empty;
            AccessPath = String.Empty;
            ItemId = String.Empty;
            Source = String.Empty;
            DataType = "Float";
            Scale = 1.0;
            Unit = String.Empty;
            Minimum = 0.0;
            Maximum = 0.0;
            Enabled = true;
        }

        public bool IsValid()
        {
            return
                !String.IsNullOrEmpty(TagId) &&
                !String.IsNullOrEmpty(Name) &&
                !String.IsNullOrEmpty(Topic) &&
                !String.IsNullOrEmpty(ItemId) &&
                Scale > 0.0 &&
                Maximum >= Minimum;
        }
    }
}
