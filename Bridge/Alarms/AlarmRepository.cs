
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SupervisorioECA.Bridge.Alarms
{
    public class AlarmRepository
    {
        private readonly string filePath;
        private readonly object syncRoot = new object();

        private readonly JavaScriptSerializer serializer =
            new JavaScriptSerializer();

        public AlarmRepository(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
                throw new ArgumentException(
                    "Caminho do arquivo nao informado."
                );

            this.filePath = filePath;

            string folder = Path.GetDirectoryName(filePath);

            if (!String.IsNullOrEmpty(folder) &&
                !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        public IList<AlarmDefinition> Load()
        {
            IList<AlarmDefinition> result =
                new List<AlarmDefinition>();

            lock (syncRoot)
            {
                if (!File.Exists(filePath))
                    return result;

                string json = File.ReadAllText(
                    filePath, Encoding.UTF8
                );

                object parsed = serializer.DeserializeObject(json);

                object[] records = parsed as object[];

                if (records == null)
                    throw new InvalidDataException(
                        "Arquivo de alarmes possui formato invalido."
                    );

                foreach (object record in records)
                {
                    Dictionary<string, object> data =
                        record as Dictionary<string, object>;

                    if (data == null)
                        continue;

                    AlarmDefinition definition =
                        new AlarmDefinition();

                    definition.TagId = GetString(
                        data, "tagId", ""
                    );

                    definition.HighAlarmEnabled = GetBool(
                        data, "highAlarmEnabled", false
                    );

                    definition.HighLimit = GetDouble(
                        data, "highLimit", 0.0
                    );

                    definition.LowAlarmEnabled = GetBool(
                        data, "lowAlarmEnabled", false
                    );

                    definition.LowLimit = GetDouble(
                        data, "lowLimit", 0.0
                    );

                    definition.Hysteresis = GetDouble(
                        data, "hysteresis", 0.05
                    );

                    definition.Enabled = GetBool(
                        data, "enabled", true
                    );

                    definition.ModifiedBy = GetString(
                        data, "modifiedBy", ""
                    );

                    string modified = GetString(
                        data, "lastModified", ""
                    );

                    DateTime parsedDate;

                    if (DateTime.TryParse(
                        modified, out parsedDate))
                    {
                        definition.LastModified = parsedDate;
                    }

                    if (definition.IsValid())
                        result.Add(definition);
                }
            }

            return result;
        }

        public void Save(IList<AlarmDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException("definitions");

            List<Dictionary<string, object>> records =
                new List<Dictionary<string, object>>();

            foreach (AlarmDefinition definition in definitions)
            {
                if (definition == null || !definition.IsValid())
                    throw new InvalidDataException(
                        "Existe uma configuracao de alarme invalida."
                    );

                Dictionary<string, object> record =
                    new Dictionary<string, object>();

                record["tagId"] = definition.TagId;
                record["highAlarmEnabled"] =
                    definition.HighAlarmEnabled;
                record["highLimit"] = definition.HighLimit;
                record["lowAlarmEnabled"] =
                    definition.LowAlarmEnabled;
                record["lowLimit"] = definition.LowLimit;
                record["hysteresis"] = definition.Hysteresis;
                record["enabled"] = definition.Enabled;
                record["modifiedBy"] = definition.ModifiedBy;
                record["lastModified"] =
                    definition.LastModified.ToString("o");

                records.Add(record);
            }

            string json = serializer.Serialize(records);

            lock (syncRoot)
            {
                string temporaryPath = filePath + ".tmp";

                File.WriteAllText(
                    temporaryPath,
                    json,
                    new UTF8Encoding(false)
                );

                if (File.Exists(filePath))
                {
                    string backupPath = filePath + ".bak";

                    if (File.Exists(backupPath))
                        File.Delete(backupPath);

                    File.Replace(
                        temporaryPath,
                        filePath,
                        backupPath
                    );
                }
                else
                {
                    File.Move(temporaryPath, filePath);
                }
            }
        }

        private static string GetString(
            Dictionary<string, object> data,
            string key,
            string defaultValue)
        {
            object value;

            if (!data.TryGetValue(key, out value) || value == null)
                return defaultValue;

            return Convert.ToString(value);
        }

        private static double GetDouble(
            Dictionary<string, object> data,
            string key,
            double defaultValue)
        {
            object value;

            if (!data.TryGetValue(key, out value) || value == null)
                return defaultValue;

            return Convert.ToDouble(value);
        }

        private static bool GetBool(
            Dictionary<string, object> data,
            string key,
            bool defaultValue)
        {
            object value;

            if (!data.TryGetValue(key, out value) || value == null)
                return defaultValue;

            return Convert.ToBoolean(value);
        }
    }
}
