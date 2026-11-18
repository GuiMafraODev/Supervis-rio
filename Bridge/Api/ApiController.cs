
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Web.Script.Serialization;

namespace SupervisorioECA.Bridge.Api
{
    public sealed class ApiController
    {
        private readonly ApiDataService dataService;
        private readonly EventHistoryApiService eventHistoryService;
        private readonly JavaScriptSerializer serializer;

        public ApiController(ApiDataService dataService)
            : this(dataService, null)
        {
        }

        public ApiController(
            ApiDataService dataService,
            EventHistoryApiService eventHistoryService)
        {
            if (dataService == null)
                throw new ArgumentNullException("dataService");

            this.dataService = dataService;
            this.eventHistoryService = eventHistoryService;

            serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = Int32.MaxValue;
        }

        public string Handle(
            string method,
            string path,
            NameValueCollection query,
            string body)
        {
            try
            {
                if (String.IsNullOrEmpty(method))
                    return SerializeError("Método HTTP não informado.");

                object result;

                if (String.Equals(
                    method,
                    "GET",
                    StringComparison.OrdinalIgnoreCase))
                {
                    result = HandleGet(path, query);
                }
                else if (String.Equals(
                    method,
                    "POST",
                    StringComparison.OrdinalIgnoreCase))
                {
                    result = HandlePost(path, body);
                }
                else
                {
                    return SerializeError("Método não permitido.");
                }

                return serializer.Serialize(
                    new ApiResponse
                    {
                        Success = true,
                        Message = "OK",
                        Data = result
                    });
            }
            catch (Exception ex)
            {
                return SerializeError(ex.Message);
            }
        }

        private object HandleGet(
            string path,
            NameValueCollection query)
        {
            if (path == "/api/status")
                return dataService.GetStatus();

            if (path == "/api/tags")
                return dataService.GetTags();

            if (path == "/api/alarms")
                return dataService.GetActiveAlarms();

            if (path == "/api/events")
            {
                if (eventHistoryService != null)
                {
                    string start = query != null
                        ? query["start"]
                        : null;

                    string end = query != null
                        ? query["end"]
                        : null;

                    string tagId = query != null
                        ? query["tagId"]
                        : null;

                    int maxResults = 500;

                    if (query != null &&
                        !String.IsNullOrEmpty(query["limit"]))
                    {
                        if (!Int32.TryParse(
                            query["limit"],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out maxResults))
                        {
                            maxResults = 500;
                        }
                    }

                    if (maxResults <= 0)
                        maxResults = 500;

                    return eventHistoryService.GetEvents(
                        start,
                        end,
                        tagId,
                        maxResults);
                }

                return dataService.GetEvents();
            }

            if (path == "/api/trends")
            {
                string tagId = query != null
                    ? query["tagId"]
                    : null;

                string start = query != null
                    ? query["start"]
                    : null;

                string end = query != null
                    ? query["end"]
                    : null;

                return dataService.GetTrend(
                    tagId,
                    start,
                    end);
            }

            throw new InvalidOperationException(
                "Rota GET não encontrada.");
        }

        private object HandlePost(
            string path,
            string body)
        {
            if (path == "/api/alarms/acknowledge")
            {
                Dictionary<string, object> request =
                    serializer.Deserialize<Dictionary<string, object>>(
                        String.IsNullOrEmpty(body) ? "{}" : body
                    );

                if (request == null)
                    throw new ArgumentException(
                        "Corpo da requisicao invalido.");

                object idObject;
                object userObject;

                string idText = null;
                string user = "OPERADOR";

                if (request.TryGetValue("id", out idObject) &&
                    idObject != null)
                {
                    idText = Convert.ToString(
                        idObject,
                        CultureInfo.InvariantCulture
                    );
                }

                if (request.TryGetValue("user", out userObject) &&
                    userObject != null)
                {
                    user = Convert.ToString(
                        userObject,
                        CultureInfo.InvariantCulture
                    );
                }

                long occurrenceId;

                if (String.IsNullOrEmpty(idText) ||
                    !Int64.TryParse(
                        idText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out occurrenceId) ||
                    occurrenceId <= 0)
                {
                    throw new ArgumentException(
                        "Identificador do alarme invalido.");
                }

                if (String.IsNullOrEmpty(user))
                    user = "OPERADOR";

                string normalizedId = occurrenceId.ToString(
                    CultureInfo.InvariantCulture
                );

                bool acknowledged =
                    dataService.AcknowledgeAlarm(
                        normalizedId,
                        user
                    );

                if (!acknowledged)
                    throw new InvalidOperationException(
                        "Não foi possível reconhecer o alarme."
                    );

                return new
                {
                    acknowledged = true,
                    id = normalizedId,
                    user = user
                };
            }

            throw new InvalidOperationException(
                "Rota POST não encontrada."
            );
        }

        private string SerializeError(string message)
        {
            return serializer.Serialize(
                new ApiResponse
                {
                    Success = false,
                    Message = message,
                    Data = null
                });
        }
    }
}
