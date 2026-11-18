
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace SupervisorioECA.Bridge.Api
{
    public class ApiController
    {
        private readonly ApiDataService dataService;

        private readonly JavaScriptSerializer serializer =
            new JavaScriptSerializer();

        public ApiController(ApiDataService dataService)
        {
            if (dataService == null)
                throw new ArgumentNullException("dataService");

            this.dataService = dataService;
        }

        public string Handle(
            string method,
            string path,
            string query,
            string body)
        {
            try
            {
                object data = Route(
                    method,
                    path,
                    query,
                    body
                );

                return Serialize(ApiResponse.Ok(data));
            }
            catch (ArgumentException ex)
            {
                return Serialize(ApiResponse.Error(ex.Message));
            }
            catch (Exception ex)
            {
                return Serialize(
                    ApiResponse.Error(
                        "Erro interno ao processar requisicao."
                    )
                );
            }
        }

        private object Route(
            string method,
            string path,
            string query,
            string body)
        {
            method = (method ?? "").ToUpperInvariant();
            path = (path ?? "").TrimEnd('/');

            Dictionary<string, string> parameters =
                ParseQuery(query);

            if (method == "GET" && path == "/api/status")
                return dataService.GetStatus();

            if (method == "GET" && path == "/api/tags")
                return dataService.GetTags();

            if (method == "GET" && path == "/api/alarms")
                return dataService.GetActiveAlarms();

            if (method == "GET" && path == "/api/events")
            {
                DateTime start = GetDate(
                    parameters,
                    "start",
                    DateTime.Now.Date
                );

                DateTime end = GetDate(
                    parameters,
                    "end",
                    DateTime.Now
                );

                string tagId = GetParameter(
                    parameters, "tagId", ""
                );

                int limit = GetInteger(
                    parameters, "limit", 500
                );

                if (limit < 1 || limit > 5000)
                    throw new ArgumentException(
                        "Limite de consulta invalido."
                    );

                return dataService.GetEvents(
                    start,
                    end,
                    tagId,
                    limit
                );
            }

            if (method == "GET" && path == "/api/trends")
            {
                string tagId = GetParameter(
                    parameters, "tagId", ""
                );

                if (String.IsNullOrEmpty(tagId))
                    throw new ArgumentException(
                        "Informe a variavel da tendencia."
                    );

                DateTime start = GetDate(
                    parameters,
                    "start",
                    DateTime.Now.AddHours(-1)
                );

                DateTime end = GetDate(
                    parameters,
                    "end",
                    DateTime.Now
                );

                if (end < start)
                    throw new ArgumentException(
                        "Periodo de consulta invalido."
                    );

                return dataService.GetTrend(
                    tagId,
                    start,
                    end
                );
            }

            if (method == "POST" &&
                path == "/api/alarms/acknowledge")
            {
                Dictionary<string, object> request =
                    serializer.Deserialize<
                        Dictionary<string, object>
                    >(body ?? "{}");

                string alarmId = GetBodyString(
                    request, "alarmId"
                );

                string user = GetBodyString(
                    request, "user"
                );

                if (String.IsNullOrEmpty(alarmId))
                    throw new ArgumentException(
                        "Identificador do alarme nao informado."
                    );

                bool acknowledged =
                    dataService.AcknowledgeAlarm(
                        alarmId,
                        user
                    );

                if (!acknowledged)
                    throw new ArgumentException(
                        "Alarme nao encontrado ou ja reconhecido."
                    );

                return new Dictionary<string, object>
                {
                    { "acknowledged", true }
                };
            }

            if (method == "POST")
                throw new ArgumentException(
                    "Operacao de escrita nao autorizada."
                );

            throw new ArgumentException(
                "Rota ou metodo nao encontrado."
            );
        }

        private Dictionary<string, string> ParseQuery(
            string query)
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase
                );

            if (String.IsNullOrEmpty(query))
                return result;

            string[] parts = query.TrimStart('?').Split('&');

            foreach (string part in parts)
            {
                if (String.IsNullOrEmpty(part))
                    continue;

                string[] pair = part.Split(
                    new char[] { '=' }, 2
                );

                string key = Uri.UnescapeDataString(
                    pair[0].Replace("+", " ")
                );

                string value = pair.Length > 1
                    ? Uri.UnescapeDataString(
                        pair[1].Replace("+", " ")
                    )
                    : "";

                result[key] = value;
            }

            return result;
        }

        private string GetParameter(
            Dictionary<string, string> parameters,
            string key,
            string defaultValue)
        {
            string value;

            return parameters.TryGetValue(key, out value)
                ? value
                : defaultValue;
        }

        private DateTime GetDate(
            Dictionary<string, string> parameters,
            string key,
            DateTime defaultValue)
        {
            string value;

            if (!parameters.TryGetValue(key, out value) ||
                String.IsNullOrEmpty(value))
                return defaultValue;

            DateTime parsed;

            if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out parsed))
            {
                throw new ArgumentException(
                    "Data invalida no parametro: " + key
                );
            }

            return parsed;
        }

        private int GetInteger(
            Dictionary<string, string> parameters,
            string key,
            int defaultValue)
        {
            string value;

            if (!parameters.TryGetValue(key, out value))
                return defaultValue;

            int parsed;

            if (!Int32.TryParse(value, out parsed))
                throw new ArgumentException(
                    "Numero invalido no parametro: " + key
                );

            return parsed;
        }

        private string GetBodyString(
            Dictionary<string, object> request,
            string key)
        {
            object value;

            if (request == null ||
                !request.TryGetValue(key, out value) ||
                value == null)
                return "";

            return Convert.ToString(value);
        }

        private string Serialize(ApiResponse response)
        {
            return serializer.Serialize(response);
        }
    }
}
