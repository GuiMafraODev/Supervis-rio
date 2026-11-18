
using System;
using System.Collections.Generic;
using SupervisorioECA.Bridge.Opc;

namespace SupervisorioECA.Bridge.Alarms
{
    public class AlarmEngine
    {
        private readonly object syncRoot = new object();

        private readonly Dictionary<string, OpcTagDefinition> tags =
            new Dictionary<string, OpcTagDefinition>(
                StringComparer.OrdinalIgnoreCase
            );

        private readonly Dictionary<string, AlarmDefinition> definitions =
            new Dictionary<string, AlarmDefinition>(
                StringComparer.OrdinalIgnoreCase
            );

        private readonly Dictionary<string, AlarmOccurrence> activeAlarms =
            new Dictionary<string, AlarmOccurrence>(
                StringComparer.OrdinalIgnoreCase
            );

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
                if (tag == null ||
                    String.IsNullOrEmpty(tag.TagId))
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
            if (values == null || values.Count == 0)
                return;

            bool changed = false;

            lock (syncRoot)
            {
                foreach (OpcValue value in values)
                {
                    if (value == null)
                        continue;

                    if (String.IsNullOrEmpty(value.TagId))
                        continue;

                    // Leituras invalidas nao podem gerar
                    // alarmes nem normalizar ocorrencias.
                    if (!value.IsGood())
                        continue;

                    AlarmDefinition definition;

                    if (!definitions.TryGetValue(
                        value.TagId,
                        out definition))
                        continue;

                    if (!definition.Enabled)
                        continue;

                    OpcTagDefinition tag;

                    if (!tags.TryGetValue(
                        value.TagId,
                        out tag))
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
                key,
                out occurrence
            );

            double limit;
            bool triggered;
            bool normalized;

            if (alarmType == "HIGH")
            {
                limit = definition.HighLimit;

                triggered = value.Value > limit;

                normalized =
                    value.Value <= limit - definition.Hysteresis;
            }
            else
            {
                limit = definition.LowLimit;

                triggered = value.Value < limit;

                normalized =
                    value.Value >= limit + definition.Hysteresis;
            }

            // Ainda nao existe ocorrencia ativa.
            if (!isActive)
            {
                if (!triggered)
                    return false;

                // Impede reutilizacao ou estouro do identificador.
                if (nextId <= 0)
                    return false;

                occurrence = new AlarmOccurrence();

                occurrence.Id = nextId;

                if (nextId == Int64.MaxValue)
                    nextId = 0;
                else
                    nextId++;

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

            // Normaliza somente quando o valor ultrapassa
            // a faixa de histerese configurada.
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
            if (occurrenceId <= 0)
                return false;

            if (String.IsNullOrEmpty(user))
                user = "OPERADOR";

            bool changed = false;

            lock (syncRoot)
            {
                foreach (AlarmOccurrence occurrence in occurrences)
                {
                    if (occurrence == null)
                        continue;

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

        // Compatibilidade com chamadas que recebam o ID como texto.
        public bool Acknowledge(string occurrenceId, string user)
        {
            long id;

            if (!Int64.TryParse(occurrenceId, out id))
                return false;

            return Acknowledge(id, user);
        }

        public void RestoreOccurrences(
            IList<AlarmOccurrence> restoredOccurrences)
        {
            if (restoredOccurrences == null)
                return;

            lock (syncRoot)
            {
                occurrences.Clear();
                activeAlarms.Clear();

                nextId = 1;

                for (int i = 0;
                    i < restoredOccurrences.Count;
                    i++)
                {
                    AlarmOccurrence occurrence =
                        restoredOccurrences[i];

                    if (occurrence == null)
                        continue;

                    if (occurrence.Id <= 0)
                        continue;

                    if (String.IsNullOrEmpty(occurrence.TagId) ||
                        String.IsNullOrEmpty(occurrence.AlarmType))
                        continue;

                    // Restaura a ocorrencia no historico.
                    occurrences.Add(occurrence);

                    // Recupera a sequencia dos identificadores.
                    if (occurrence.Id >= nextId)
                    {
                        if (occurrence.Id == Int64.MaxValue)
                            nextId = 0;
                        else
                            nextId = occurrence.Id + 1;
                    }

                    string key =
                        occurrence.TagId + "|" +
                        occurrence.AlarmType;

                    if (String.Equals(
                        occurrence.State,
                        "ACTIVE",
                        StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(
                        occurrence.State,
                        "ACKNOWLEDGED",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        activeAlarms[key] = occurrence;
                    }
                    else if (String.Equals(
                        occurrence.State,
                        "NORMALIZED",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        AlarmOccurrence current;

                        // Uma ocorrencia antiga normalizada nao pode
                        // remover outra ocorrencia ativa mais recente.
                        if (activeAlarms.TryGetValue(
                            key,
                            out current))
                        {
                            if (current.Id == occurrence.Id)
                                activeAlarms.Remove(key);
                        }
                    }
                }
            }
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
