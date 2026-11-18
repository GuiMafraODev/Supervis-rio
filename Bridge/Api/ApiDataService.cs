
using System;
using System.Collections.Generic;
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
            this.tags = tags;
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

                OpcValue value;

                if (latest.TryGetValue(
                    definition.TagId, out value) &&
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

                    item.Error = value != null
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

            foreach (AlarmOccurrence occurrence in active)
            {
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

            IList<AlarmEvent> events =
                alarms.GetEvents(
                    start,
                    end,
                    tagId,
                    maxResults
                );

            foreach (AlarmEvent alarmEvent in events)
            {
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

        public IList<ApiTrendPoint> GetTrend(
            string tagId,
            DateTime start,
            DateTime end)
        {
            IList<ApiTrendPoint> result =
                new List<ApiTrendPoint>();

            IList<TrendSample> samples =
                trends.Query(tagId, start, end);

            foreach (TrendSample sample in samples)
            {
                if (!sample.IsValid)
                    continue;

                ApiTrendPoint point = new ApiTrendPoint();

                point.Timestamp = sample.Timestamp;
                point.Value = sample.Value;
                point.Quality = sample.Quality;

                result.Add(point);
            }

            return result;
        }

        public bool AcknowledgeAlarm(
            string alarmId,
            string user)
        {
            return alarms.Acknowledge(
                alarmId,
                user
            );
        }
    }
}
