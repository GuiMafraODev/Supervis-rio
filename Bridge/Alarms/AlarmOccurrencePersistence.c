
using System;
using System.Collections.Generic;
using SupervisorioECA.Bridge.Storage;

namespace SupervisorioECA.Bridge.Alarms
{
    public sealed class AlarmOccurrencePersistence
    {
        private readonly AlarmOccurrenceStore store;

        public AlarmOccurrencePersistence(string directory)
        {
            if (String.IsNullOrEmpty(directory))
                throw new ArgumentException(
                    "Diretorio de ocorrencias invalido.");

            store = new AlarmOccurrenceStore(directory);
        }

        public IList<AlarmOccurrence> Restore()
        {
            return store.LoadAll();
        }

        public void Save(AlarmOccurrence occurrence)
        {
            if (occurrence == null)
                return;

            store.Save(occurrence);
        }

        public void SaveAll(
            IList<AlarmOccurrence> occurrences)
        {
            if (occurrences == null)
                return;

            store.SaveAll(occurrences);
        }
    }
}
