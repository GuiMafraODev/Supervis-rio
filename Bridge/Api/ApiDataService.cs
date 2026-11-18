
using System;
using System.Collections.Generic;
using System.Globalization;
using SupervisorioECA.Bridge.Opc;
using SupervisorioECA.Bridge.Communication;
using SupervisorioECA.Bridge.Alarms;
using SupervisorioECA.Bridge.Storage;

namespace SupervisorioECA.Bridge.Api
{
    public class ApiDataService
    {
        private readonly IList<OpcTagDefinition> tags;
        private readonly OpcAcquisitionService acquisition;
        private readonly AlarmService alarms;
        private readonly TrendStore trends;

        private readonly Func<bool> opcConnected;
        private readonly Func<string> opcServerName;

        public ApiDataService(
            IList<OpcTagDefinition> tags,
            OpcAcquisitionService acquisition,
            AlarmService alarms,
            TrendStore trends,
            Func<bool> opcConnected,
            Func<string> opcServerName)
        {
            if (acquisition == null)
                throw new ArgumentNullException("acquisition");

            if (alarms == null)
                throw new ArgumentNullException("alarms");

            if (trends == null)
                throw new ArgumentNullException("trends");

            this.tags = tags ?? new List<OpcTagDefinition>();
            this.acquisition = acquisition;
            this.alarms = alarms;
            this.trends = trends;
            this.opcConnected = opcConnected;
            this.opcServerName = opcServerName;
        }

        public ApiCommunicationStatus GetStatus()
        {
            ApiCommunicationStatus status =
                new ApiCommunicationStatus();

            status.ApiOnline = true;

            status.OpcConnected =
                opcConnected != null && opcConnected();

            status.OpcServer =
                opcServerName != null
                    ? opcServerName()
                    : "";

            status.Timestamp = DateTime.Now;

            status.Message = status.OpcConnected
                ? "Comunicacao OPC estabelecida."
                : "Aguardando comunicacao OPC.";

            return status;
        }

        public IList<ApiTagValue> GetTags()
        {
            IList<ApiTagValue> result =
                new List<ApiTagValue>();

            IDictionary<string, OpcValue> latest =
                acquisition.GetLatestValues();

            foreach (OpcTagDefinition definition in tags)
            {
                if (definition == null || !definition.Enabled)
                    continue;

                ApiTagValue item = new ApiTagValue();

                item.Id = definition.TagId;
                item.Name = definition.Name;
                item.Unit = definition.Unit;

                OpcValue value = null;

                bool hasValue =
                    latest != null &&
                    latest.TryGetValue(
                        definition.TagId,
                        out value
                    );

                if (hasValue &&
                    value != null &&
                    value.IsGood())
                {
                    item.Value = value.Value;
                    item.Quality = value.Quality;
                    item.Timestamp = value.Timestamp;
                    item.Available = true;
                    item.Error = "";
                }
                else
                {
                    item.Value = null;

                    item.Quality = value != null
                        ? value.Quality
                        : "Bad";

                    item.Timestamp = value != null
                        ? (DateTime?)value.Timestamp
                        : null;

                    item.Available = false;

                    item.Error = value != null &&
                                 !String.IsNullOrEmpty(value.ErrorMessage)
                        ? value.ErrorMessage
                        : "Aguardando primeira leitura OPC.";
                }

                result.Add(item);
            }

            return result;
        }

        public IList<ApiAlarm> GetActiveAlarms()
        {
            IList<ApiAlarm> result =
                new List<ApiAlarm>();

            IList<AlarmOccurrence> active =
                alarms.GetActiveAlarms();

            if (active == null)
                return result;

            foreach (AlarmOccurrence occurrence in active)
            {
                if (occurrence == null)
                    continue;

                ApiAlarm item = new ApiAlarm();

                item.Id = occurrence.Id;
                item.TagId = occurrence.TagId;
                item.TagName = occurrence.TagName;
                item.AlarmType = occurrence.AlarmType;
                item.State = occurrence.State;
                item.Value = occurrence.TriggerValue;
                item.AlarmLimit = occurrence.AlarmLimit;
                item.TriggeredAt = occurrence.TriggeredAt;
                item.AcknowledgedAt =
                    occurrence.AcknowledgedAt;
                item.NormalizedAt =
                    occurrence.NormalizedAt;
                item.AcknowledgedBy =
                    occurrence.AcknowledgedBy;
                item.Description =
                    occurrence.Description;

                result.Add(item);
            }

            return result;
        }

        public IList<ApiEvent> GetEvents(
            DateTime start,
            DateTime end,
            string tagId,
            int maxResults)
        {
            IList<ApiEvent> result =
                new List<ApiEvent>();

            if (end < start)
                throw new ArgumentException(
                    "A data final nao pode ser anterior a data inicial."
                );

            if (maxResults <= 0)
                maxResults = 500;

            IList<AlarmEvent> events =
                alarms.GetEvents(
                    start,
                    end,
                    tagId,
                    maxResults
                );

            if (events == null)
                return result;

            foreach (AlarmEvent alarmEvent in events)
            {
                if (alarmEvent == null)
                    continue;

                ApiEvent item = new ApiEvent();

                item.Id = alarmEvent.Id;
                item.Timestamp = alarmEvent.Timestamp;
                item.EventType = alarmEvent.EventType;
                item.TagId = alarmEvent.TagId;
                item.TagName = alarmEvent.TagName;
                item.AlarmType = alarmEvent.AlarmType;
                item.Description = alarmEvent.Description;
                item.Value = alarmEvent.Value;
                item.AlarmLimit = alarmEvent.AlarmLimit;
                item.User = alarmEvent.User;
                item.PreviousLimit =
                    alarmEvent.PreviousLimit;
                item.NewLimit = alarmEvent.NewLimit;
                item.State = alarmEvent.State;

                result.Add(item);
            }

            return result;
        }

        // Compatibilidade com a rota /api/events
        // quando o EventHistoryApiService nao estiver configurado.
        // Nesse caso, retorna os eventos das ultimas 24 horas.
        public IList<ApiEvent> GetEvents()
        {
            DateTime end = DateTime.Now;
            DateTime start = end.AddHours(-24);

            return GetEvents(
                start,
                end,
                null,
                500
            );
        }

        public IList<ApiTrendPoint> GetTrend(
            string tagId,
            DateTime start,
            DateTime end)
        {
            IList<ApiTrendPoint> result =
                new List<ApiTrendPoint>();

            if (String.IsNullOrEmpty(tagId))
                throw new ArgumentException(
                    "Identificador da tag nao informado."
                );

            if (end < start)
                throw new ArgumentException(
                    "A data final nao pode ser anterior a data inicial."
                );

            IList<TrendSample> samples =
                trends.Query(tagId, start, end);

            if (samples == null)
                return result;

            foreach (TrendSample sample in samples)
            {
                if (sample == null || !sample.IsValid)
                    continue;

                ApiTrendPoint point = new ApiTrendPoint();

                point.Timestamp = sample.Timestamp;
                point.Value = sample.Value;
                point.Quality = sample.Quality;

                result.Add(point);
            }

            return result;
        }

        // Sobrecarga utilizada pelo ApiController,
        // que recebe datas como texto pela URL.
        public IList<ApiTrendPoint> GetTrend(
            string tagId,
            string start,
            string end)
        {
            DateTime startDate;
            DateTime endDate;

            ParseDateRange(
                start,
                end,
                out startDate,
                out endDate
            );

            return GetTrend(
                tagId,
                startDate,
                endDate
            );
        }

        private void ParseDateRange(
            string start,
            string end,
            out DateTime startDate,
            out DateTime endDate)
        {
            DateTime now = DateTime.Now;

            startDate = now.AddHours(-24);
            endDate = now;

            if (!String.IsNullOrEmpty(start))
            {
                if (!DateTime.TryParse(
                    start,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out startDate))
                {
                    throw new ArgumentException(
                        "Data inicial invalida."
                    );
                }
            }

            if (!String.IsNullOrEmpty(end))
            {
                if (!DateTime.TryParse(
                    end,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out endDate))
                {
                    throw new ArgumentException(
                        "Data final invalida."
                    );
                }
            }

            if (endDate < startDate)
                throw new ArgumentException(
                    "A data final nao pode ser anterior a data inicial."
                );
        }

        public bool AcknowledgeAlarm(
            string alarmId,
            string user)
        {
            if (String.IsNullOrEmpty(alarmId))
                return false;

            return alarms.Acknowledge(
                alarmId,
                user
            );
        }
    }
}
