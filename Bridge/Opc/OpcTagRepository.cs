
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace SupervisorioECA.Bridge.Opc
{
    public class OpcTagRepository
    {
        private readonly string filePath;

        public OpcTagRepository(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
                throw new ArgumentException("Caminho do arquivo invalido.");

            this.filePath = filePath;
        }

        public IList<OpcTagDefinition> Load()
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Arquivo tags.json nao encontrado.",
                    filePath
                );
            }

            string json = File.ReadAllText(filePath);

            JavaScriptSerializer serializer =
                new JavaScriptSerializer();

            object root = serializer.DeserializeObject(json);

            Dictionary<string, object> document =
                root as Dictionary<string, object>;

            if (document == null)
            {
                throw new InvalidDataException(
                    "Estrutura principal do tags.json invalida."
                );
            }

            object tagsObject;

            if (!document.TryGetValue("tags", out tagsObject))
            {
                throw new InvalidDataException(
                    "A lista de tags nao foi encontrada."
                );
            }

            object[] tagArray = tagsObject as object[];

            if (tagArray == null)
            {
                throw new InvalidDataException(
                    "A propriedade tags precisa ser uma lista."
                );
            }

            IList<OpcTagDefinition> result =
                new List<OpcTagDefinition>();

            foreach (object item in tagArray)
            {
                Dictionary<string, object> data =
                    item as Dictionary<string, object>;

                if (data == null)
                {
                    throw new InvalidDataException(
                        "Uma definicao de tag possui formato invalido."
                    );
                }

                OpcTagDefinition tag = new OpcTagDefinition();

                tag.TagId = GetString(data, "id", "");
                tag.Name = GetString(data, "name", "");
                tag.Topic = GetString(data, "topic", "");
                tag.AccessPath = GetString(data, "accessPath", "");
                tag.ItemId = GetString(data, "itemId", "");
                tag.Source = GetString(data, "source", "");

                tag.DataType = GetString(
                    data, "dataType", "Float"
                );

                tag.Scale = GetDouble(
                    data, "scale", 1.0
                );

                tag.Unit = GetString(
                    data, "unit", ""
                );

                tag.Minimum = GetDouble(
                    data, "min", 0.0
                );

                tag.Maximum = GetDouble(
                    data, "max", 0.0
                );

                tag.Enabled = GetBool(
                    data, "enabled", true
                );

                if (!tag.IsValid())
                {
                    throw new InvalidDataException(
                        "Definicao invalida para a tag: " +
                        tag.TagId
                    );
                }

                result.Add(tag);
            }

            if (result.Count == 0)
            {
                throw new InvalidDataException(
                    "Nenhuma tag foi carregada."
                );
            }

            return result;
        }

        private static string GetString(
            Dictionary<string, object> data,
            string key,
            string defaultValue)
        {
            object value;

            if (!data.TryGetValue(key, out value) || value == null)
                return defaultValue;

            return Convert.ToString(
                value, CultureInfo.InvariantCulture
            );
        }

        private static double GetDouble(
            Dictionary<string, object> data,
            string key,
            double defaultValue)
        {
            object value;

            if (!data.TryGetValue(key, out value) || value == null)
                return defaultValue;

            return Convert.ToDouble(
                value, CultureInfo.InvariantCulture
            );
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
