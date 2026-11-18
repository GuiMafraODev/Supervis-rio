
using System;
using System.Collections.Generic;
using SupervisorioECA.Bridge.Opc;
using SupervisorioECA.Bridge.Storage;

namespace SupervisorioECA.Bridge.Alarms
{
    public class AlarmService
    {
        private readonly AlarmEngine engine;
        private readonly AlarmRepository repository;
        private readonly AlarmEventStore eventStore;

        private readonly Dictionary<string, AlarmDefinition>
            definitions =
            new Dictionary<string, AlarmDefinition>(
                StringComparer.OrdinalIgnoreCase
            );

        private readonly Dictionary<string, string> tagNames =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );

        private readonly object syncRoot = new object();

        public event EventHandler AlarmChanged;

        public AlarmService(
            IList<OpcTagDefinition> tags,
            string configFile,
            string eventsFolder)
        {
            engine = new AlarmEngine(tags);

            repository = new AlarmRepository(configFile);

            eventStore = new AlarmEventStore(eventsFolder);

            if (tags != null)
            {
                foreach (OpcTagDefinition tag in tags)
                {
                    if (tag != null)
                        tagNames[tag.TagId] = tag.Name;
                }
            }

            LoadDefinitions();
        }

        private void LoadDefinitions()
        {
            IList<AlarmDefinition> saved =
                repository.Load();

            lock (syncRoot)
            {
                foreach (AlarmDefinition definition in saved)
                {
                    if (!tagNames.ContainsKey(definition.TagId))
                        continue;

                    definitions[definition.TagId] = definition;

                    engine.SetDefinition(definition);
                }
            }
        }

        public void Evaluate(IList<OpcValue> values)
        {
            if (values == null || values.Count == 0)
                return;

            IList<AlarmOccurrence> before =
                engine.GetOccurrences();

            HashSet<string> previousIds =
                new HashSet<string>();

            foreach (AlarmOccurrence occurrence in before)
                previousIds.Add(occurrence.Id);

            engine.Evaluate(values);

            IList<AlarmOccurrence> after =
                engine.GetOccurrences();

            foreach (AlarmOccurrence occurrence in after)
            {
                if (previousIds.Contains(occurrence.Id))
                    continue;

                SaveEvent(
                    "TRIGGERED",
                    occurrence,
                    occurrence.TriggerValue,
                    occurrence.AlarmLimit,
                    ""
                );
            }

            foreach (AlarmOccurrence occurrence in before)
            {
                if (occurrence.State == "NORMALIZED")
                {
                    SaveEvent(
                        "NORMALIZED",
                        occurrence,
                        occurrence.TriggerValue,
                        occurrence.AlarmLimit,
                        ""
                    );
                }
            }

            if (after.Count != before.Count)
                RaiseAlarmChanged();
        }

        public bool Acknowledge(string alarmId, string user)
        {
            if (String.IsNullOrEmpty(user))
                user = "OPERADOR";

            bool acknowledged;

            lock (syncRoot)
            {
                acknowledged = engine.Acknowledge(
                    alarmId,
                    user
                );
            }

            if (!acknowledged)
                return false;

            AlarmOccurrence occurrence =
                FindOccurrence(alarmId);

            if (occurrence != null)
            {
                SaveEvent(
                    "ACKNOWLEDGED",
                    occurrence,
                    occurrence.TriggerValue,
                    occurrence.AlarmLimit,
                    user
                );
            }

            RaiseAlarmChanged();

            return true;
        }

        public bool SaveDefinition(
            AlarmDefinition definition,
            string user)
        {
            if (definition == null)
                return false;

            if (!tagNames.ContainsKey(definition.TagId))
                return false;

            if (String.IsNullOrEmpty(user))
                user = "OPERADOR";

            if (!definition.IsValid())
                return false;

            lock (syncRoot)
            {
                AlarmDefinition previous;

                definitions.TryGetValue(
                    definition.TagId,
                    out previous
                );

                definition.ModifiedBy = user;
                definition.LastModified = DateTime.Now;

                engine.SetDefinition(definition);

                definitions[definition.TagId] = definition;

                repository.Save(
                    new List<AlarmDefinition>(
                        definitions.Values
                    )
                );

                if (previous != null)
                {
                    if (previous.HighAlarmEnabled !=
                        definition.HighAlarmEnabled ||
                        previous.HighLimit != definition.HighLimit)
                    {
                        SaveLimitChange(
                            definition.TagId,
                            "HIGH",
                            previous.HighAlarmEnabled
                                ? (double?)previous.HighLimit : null,
                            definition.HighAlarmEnabled
                                ? (double?)definition.HighLimit : null,
                            user
                        );
                    }

                    if (previous.LowAlarmEnabled !=
                        definition.LowAlarmEnabled ||
                        previous.LowLimit != definition.LowLimit)
                    {
                        SaveLimitChange(
                            definition.TagId,
                            "LOW",
                            previous.LowAlarmEnabled
                                ? (double?)previous.LowLimit : null,
                            definition.LowAlarmEnabled
                                ? (double?)definition.LowLimit : null,
                            user
                        );
                    }
                }
                else
                {
                    if (definition.HighAlarmEnabled)
                    {
                        SaveLimitChange(
                            definition.TagId,
                            "HIGH",
                            null,
                            definition.HighLimit,
                            user
                        );
                    }

                    if (definition.LowAlarmEnabled)
                    {
                        SaveLimitChange(
                            definition.TagId,
                            "LOW",
                            null,
                            definition.LowLimit,
                            user
                        );
                    }
                }
            }

            RaiseAlarmChanged();

            return true;
        }

        public IList<AlarmOccurrence> GetActiveAlarms()
        {
            return engine.GetActiveOccurrences();
        }

        public IList<AlarmOccurrence> GetAlarmHistory()
        {
            return engine.GetOccurrences();
        }

        public IList<AlarmEvent> GetEvents(
            DateTime start,
            DateTime end,
            string tagId,
            int maxResults)
        {
            return eventStore.Query(
                start,
                end,
                tagId,
                maxResults
            );
        }

        public IList<AlarmDefinition> GetDefinitions()
        {
            lock (syncRoot)
            {
                return new List<AlarmDefinition>(
                    definitions.Values
                );
            }
        }

        private AlarmOccurrence FindOccurrence(string alarmId)
        {
            IList<AlarmOccurrence> occurrences =
                engine.GetOccurrences();

            foreach (AlarmOccurrence occurrence in occurrences)
            {
                if (occurrence.Id == alarmId)
                    return occurrence;
            }

            return null;
        }

        private void SaveLimitChange(
            string tagId,
            string alarmType,
            double? previousLimit,
            double? newLimit,
            string user)
        {
            string name = tagNames.ContainsKey(tagId)
                ? tagNames[tagId] : tagId;

            AlarmEvent alarmEvent =
                AlarmEvent.CreateLimitChange(
                    tagId,
                    name,
                    alarmType,
                    previousLimit,
                    newLimit,
                    user
                );

            eventStore.Save(alarmEvent);
        }

        private void SaveEvent(
            string eventType,
            AlarmOccurrence occurrence,
            double? value,
            double? limit,
            string user)
        {
            string name = occurrence.TagName;

            if (String.IsNullOrEmpty(name) &&
                tagNames.ContainsKey(occurrence.TagId))
            {
                name = tagNames[occurrence.TagId];
            }

            AlarmEvent alarmEvent = AlarmEvent.Create(
                eventType,
                occurrence.TagId,
                name,
                occurrence.AlarmType,
                occurrence.Description,
                value,
                limit,
                user,
                occurrence.State
            );

            eventStore.Save(alarmEvent);
        }

        private void RaiseAlarmChanged()
        {
            EventHandler handler = AlarmChanged;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
