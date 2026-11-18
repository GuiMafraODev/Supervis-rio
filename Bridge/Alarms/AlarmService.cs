
using System;
using System.Collections.Generic;
using System.Globalization;
using SupervisorioECA.Bridge.Opc;
using SupervisorioECA.Bridge.Storage;

namespace SupervisorioECA.Bridge.Alarms
{
    public class AlarmService
    {
        private readonly AlarmEngine engine;
        private readonly AlarmRepository repository;
        private readonly AlarmEventStore eventStore;
        private readonly AlarmOccurrencePersistence occurrencePersistence;

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

        public event Action<AlarmEvent> AlarmEventRecorded;

        public AlarmService(
            IList<OpcTagDefinition> tags,
            string configFile,
            string eventsFolder)
        {
            engine = new AlarmEngine(tags);

            repository = new AlarmRepository(configFile);
            eventStore = new AlarmEventStore(eventsFolder);

            occurrencePersistence =
                new AlarmOccurrencePersistence(eventsFolder);

            if (tags != null)
            {
                foreach (OpcTagDefinition tag in tags)
                {
                    if (tag != null &&
                        !String.IsNullOrEmpty(tag.TagId))
                    {
                        tagNames[tag.TagId] = tag.Name;
                    }
                }
            }

            RestoreOccurrences();
            LoadDefinitions();
        }

        private void RestoreOccurrences()
        {
            try
            {
                IList<AlarmOccurrence> saved =
                    occurrencePersistence.Restore();

                if (saved == null || saved.Count == 0)
                    return;

                engine.RestoreOccurrences(saved);
            }
            catch (Exception ex)
            {
                PublishAlarmEvent(
                    AlarmEvent.Create(
                        "RESTORE_ERROR",
                        "SYSTEM",
                        "SUPERVISORIO",
                        "SYSTEM",
                        "Falha ao recuperar ocorrencias de alarmes: " +
                            ex.Message,
                        null,
                        null,
                        "SYSTEM",
                        "ERROR"
                    )
                );
            }
        }

        private void LoadDefinitions()
        {
            IList<AlarmDefinition> saved =
                repository.Load();

            if (saved == null)
                return;

            lock (syncRoot)
            {
                foreach (AlarmDefinition definition in saved)
                {
                    if (definition == null ||
                        String.IsNullOrEmpty(definition.TagId))
                        continue;

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

            // Somente leituras OPC validas participam
            // da avaliacao dos alarmes.
            List<OpcValue> validValues =
                new List<OpcValue>();

            foreach (OpcValue value in values)
            {
                if (value == null)
                    continue;

                if (!value.IsGood())
                    continue;

                if (String.IsNullOrEmpty(value.TagId))
                    continue;

                validValues.Add(value);
            }

            // Falha de comunicacao nao significa normalizacao.
            if (validValues.Count == 0)
                return;

            Dictionary<long, string> previousStates =
                new Dictionary<long, string>();

            IList<AlarmOccurrence> after;

            lock (syncRoot)
            {
                IList<AlarmOccurrence> before =
                    engine.GetOccurrences();

                foreach (AlarmOccurrence occurrence in before)
                {
                    if (occurrence == null ||
                        occurrence.Id <= 0)
                        continue;

                    previousStates[occurrence.Id] =
                        occurrence.State;
                }

                engine.Evaluate(validValues);

                after = engine.GetOccurrences();
            }

            bool changed = false;

            foreach (AlarmOccurrence occurrence in after)
            {
                if (occurrence == null ||
                    occurrence.Id <= 0)
                    continue;

                string previousState;

                bool existed = previousStates.TryGetValue(
                    occurrence.Id,
                    out previousState
                );

                if (!existed)
                {
                    occurrencePersistence.Save(occurrence);

                    SaveEvent(
                        "TRIGGERED",
                        occurrence,
                        occurrence.TriggerValue,
                        occurrence.AlarmLimit,
                        ""
                    );

                    changed = true;
                    continue;
                }

                if (!String.Equals(
                    previousState,
                    occurrence.State,
                    StringComparison.OrdinalIgnoreCase))
                {
                    occurrencePersistence.Save(occurrence);

                    if (String.Equals(
                        occurrence.State,
                        "NORMALIZED",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        SaveEvent(
                            "NORMALIZED",
                            occurrence,
                            occurrence.TriggerValue,
                            occurrence.AlarmLimit,
                            ""
                        );
                    }

                    changed = true;
                }
            }

            if (changed)
                RaiseAlarmChanged();
        }

        public bool Acknowledge(string alarmId, string user)
        {
            long occurrenceId;

            if (String.IsNullOrEmpty(alarmId))
                return false;

            if (!Int64.TryParse(
                alarmId,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out occurrenceId))
                return false;

            return Acknowledge(occurrenceId, user);
        }

        public bool Acknowledge(long occurrenceId, string user)
        {
            if (occurrenceId <= 0)
                return false;

            if (String.IsNullOrEmpty(user))
                user = "OPERADOR";

            bool acknowledged;

            lock (syncRoot)
            {
                acknowledged = engine.Acknowledge(
                    occurrenceId,
                    user
                );
            }

            if (!acknowledged)
                return false;

            AlarmOccurrence occurrence =
                FindOccurrence(occurrenceId);

            if (occurrence != null)
            {
                occurrencePersistence.Save(occurrence);

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

            if (String.IsNullOrEmpty(definition.TagId) ||
                !tagNames.ContainsKey(definition.TagId))
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

                bool oldHighEnabled =
                    previous != null && previous.HighAlarmEnabled;

                double oldHighLimit =
                    previous != null ? previous.HighLimit : 0;

                bool oldLowEnabled =
                    previous != null && previous.LowAlarmEnabled;

                double oldLowLimit =
                    previous != null ? previous.LowLimit : 0;

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
                    if (oldHighEnabled != definition.HighAlarmEnabled ||
                        oldHighLimit != definition.HighLimit)
                    {
                        SaveLimitChange(
                            definition.TagId,
                            "HIGH",
                            oldHighEnabled
                                ? (double?)oldHighLimit : null,
                            definition.HighAlarmEnabled
                                ? (double?)definition.HighLimit : null,
                            user
                        );
                    }

                    if (oldLowEnabled != definition.LowAlarmEnabled ||
                        oldLowLimit != definition.LowLimit)
                    {
                        SaveLimitChange(
                            definition.TagId,
                            "LOW",
                            oldLowEnabled
                                ? (double?)oldLowLimit : null,
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

        private AlarmOccurrence FindOccurrence(long occurrenceId)
        {
            IList<AlarmOccurrence> occurrences =
                engine.GetOccurrences();

            foreach (AlarmOccurrence occurrence in occurrences)
            {
                if (occurrence != null &&
                    occurrence.Id == occurrenceId)
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

            PublishAlarmEvent(alarmEvent);
        }

        private void SaveEvent(
            string eventType,
            AlarmOccurrence occurrence,
            double? value,
            double? limit,
            string user)
        {
            if (occurrence == null)
                return;

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

            PublishAlarmEvent(alarmEvent);
        }

        private void PublishAlarmEvent(AlarmEvent alarmEvent)
        {
            if (alarmEvent == null)
                return;

            // Historico individual de alarmes.
            eventStore.Save(alarmEvent);

            // Notifica o historico unificado.
            Action<AlarmEvent> handler = AlarmEventRecorded;

            if (handler != null)
            {
                try
                {
                    handler(alarmEvent);
                }
                catch
                {
                    // Falha no historico unificado nao deve
                    // interromper a aquisicao ou os alarmes.
                }
            }
        }

        private void RaiseAlarmChanged()
        {
            EventHandler handler = AlarmChanged;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
