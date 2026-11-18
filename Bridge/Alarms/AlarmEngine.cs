
using System;
using System.Collections.Generic;
using SupervisorioECA.Bridge.Opc;

namespace SupervisorioECA.Bridge.Alarms
{
    public class AlarmEngine
    {
        private readonly object syncRoot = new object();

        private readonly Dictionary<string, OpcTagDefinition> tags =
            new Dictionary<string, OpcTagDefinition>();

        private readonly Dictionary<string, AlarmDefinition> definitions =
            new Dictionary<string, AlarmDefinition>();

        private readonly Dictionary<string, AlarmOccurrence> activeAlarms =
            new Dictionary<string, AlarmOccurrence>();

        private readonly List<AlarmOccurrence> occurrences =
            new List<AlarmOccurrence>();

        private long nextId = 1;

        public event EventHandler AlarmChanged;

        public AlarmEngine(IList<OpcTagDefinition> tagDefinitions)
        {
            if (tagDefinitions == null)
                throw new ArgumentNullException("tagDefinitions");

            foreach (OpcTagDefinition tag in tagDefinitions)
            {
                if (tag == null || String.IsNullOrEmpty(tag.TagId))
                    continue;

                tags[tag.TagId] = tag;
            }
        }

        public void SetDefinition(AlarmDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            if (!definition.IsValid())
                throw new ArgumentException(
                    "Configuracao de alarme invalida."
                );

            if (!tags.ContainsKey(definition.TagId))
                throw new ArgumentException(
                    "A tag informada nao existe no inventario."
                );

            lock (syncRoot)
            {
                definitions[definition.TagId] = definition;
            }
        }

        public void Evaluate(IList<OpcValue> values)
        {
            if (values == null)
                return;

            bool changed = false;

            lock (syncRoot)
            {
                foreach (OpcValue value in values)
                {
                    if (value == null || !value.IsGood())
                        continue;

                    AlarmDefinition definition;

                    if (!definitions.TryGetValue(
                        value.TagId, out definition))
                        continue;

                    if (!definition.Enabled)
                        continue;

                    OpcTagDefinition tag;

                    if (!tags.TryGetValue(value.TagId, out tag))
                        continue;

                    if (definition.HighAlarmEnabled)
                    {
                        changed |= EvaluateCondition(
                            tag,
                            definition,
                            value,
                            "HIGH"
                        );
                    }

                    if (definition.LowAlarmEnabled)
                    {
                        changed |= EvaluateCondition(
                            tag,
                            definition,
                            value,
                            "LOW"
                        );
                    }
                }
            }

            if (changed)
                RaiseAlarmChanged();
        }

        private bool EvaluateCondition(
            OpcTagDefinition tag,
            AlarmDefinition definition,
            OpcValue value,
            string alarmType)
        {
            string key = value.TagId + "|" + alarmType;

            AlarmOccurrence occurrence;

            bool isActive = activeAlarms.TryGetValue(
                key, out occurrence
            );

            double limit;
            bool triggered;
            bool normalized;

            if (alarmType == "HIGH")
            {
                limit = definition.HighLimit;
                triggered = value.Value > limit;
                normalized = value.Value <=
                    limit - definition.Hysteresis;
            }
            else
            {
                limit = definition.LowLimit;
                triggered = value.Value < limit;
                normalized = value.Value >=
                    limit + definition.Hysteresis;
            }

            if (!isActive)
            {
                if (!triggered)
                    return false;

                occurrence = new AlarmOccurrence();

                occurrence.Id = nextId++;
                occurrence.TagId = value.TagId;
                occurrence.TagName = tag.Name;
                occurrence.AlarmType = alarmType;
                occurrence.State = "ACTIVE";
                occurrence.TriggerValue = value.Value;
                occurrence.AlarmLimit = limit;
                occurrence.TriggeredAt = DateTime.Now;

                occurrence.Description =
                    alarmType == "HIGH"
                    ? "Alarme de valor alto"
                    : "Alarme de valor baixo";

                activeAlarms[key] = occurrence;
                occurrences.Add(occurrence);

                return true;
            }

            if (normalized)
            {
                occurrence.Normalize();
                activeAlarms.Remove(key);

                return true;
            }

            return false;
        }

        public bool Acknowledge(long occurrenceId, string user)
        {
            bool changed = false;

            lock (syncRoot)
            {
                foreach (AlarmOccurrence occurrence in occurrences)
                {
                    if (occurrence.Id != occurrenceId)
                        continue;

                    if (!occurrence.IsActive() ||
                        occurrence.IsAcknowledged())
                        return false;

                    occurrence.Acknowledge(user);
                    changed = true;
                    break;
                }
            }

            if (changed)
                RaiseAlarmChanged();

            return changed;
        }

        public IList<AlarmOccurrence> GetOccurrences()
        {
            IList<AlarmOccurrence> result =
                new List<AlarmOccurrence>();

            lock (syncRoot)
            {
                foreach (AlarmOccurrence occurrence in occurrences)
                {
                    result.Add(occurrence);
                }
            }

            return result;
        }

        public IList<AlarmOccurrence> GetActiveOccurrences()
        {
            IList<AlarmOccurrence> result =
                new List<AlarmOccurrence>();

            lock (syncRoot)
            {
                foreach (AlarmOccurrence occurrence in activeAlarms.Values)
                {
                    result.Add(occurrence);
                }
            }

            return result;
        }

        private void RaiseAlarmChanged()
        {
            EventHandler handler = AlarmChanged;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
