
using System;
using System.Collections.Generic;
using System.Globalization;
using SupervisorioECA.Bridge.Storage;

namespace SupervisorioECA.Bridge.Api
{
    public sealed class EventHistoryApiService
    {
        private readonly EventHistoryService history;

        public EventHistoryApiService(EventHistoryService history)
        {
            if (history == null)
                throw new ArgumentNullException("history");

            this.history = history;
        }

        public IList<UnifiedEvent> GetEvents(
            string startText,
            string endText,
            string tagId,
            int maxResults)
        {
            DateTime? start = ParseDate(startText);
            DateTime? end = ParseDate(endText);

            if (start.HasValue && end.HasValue &&
                start.Value > end.Value)
            {
                throw new ArgumentException(
                    "A data inicial não pode ser posterior à data final.");
            }

            if (maxResults <= 0)
                maxResults = 500;

            if (maxResults > 2000)
                maxResults = 2000;

            IList<UnifiedEvent> results =
                history.Query(start, end, tagId);

            List<UnifiedEvent> limited =
                new List<UnifiedEvent>();

            if (results == null)
                return limited;

            for (int i = results.Count - 1;
                 i >= 0 && limited.Count < maxResults;
                 i--)
            {
                limited.Add(results[i]);
            }

            return limited;
        }

        private DateTime? ParseDate(string value)
        {
            if (String.IsNullOrEmpty(value))
                return null;

            DateTime parsed;

            bool valid = DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out parsed);

            if (!valid)
            {
                throw new ArgumentException(
                    "Formato de data inválido: " + value);
            }

            return parsed;
        }
    }
}
