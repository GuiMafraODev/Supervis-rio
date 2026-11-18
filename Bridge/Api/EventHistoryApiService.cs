
using System;
using System.Collections.Generic;
using System.Globalization;
using SupervisorioECA.Bridge.Storage;

namespace SupervisorioECA.Bridge.Api
{
    public class EventHistoryApiService
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
            DateTime start;
            DateTime end;

            if (!DateTime.TryParse(
                startText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out start))
            {
                throw new ArgumentException(
                    "Data inicial invalida."
                );
            }

            if (!DateTime.TryParse(
                endText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out end))
            {
                throw new ArgumentException(
                    "Data final invalida."
                );
            }

            if (end < start)
                throw new ArgumentException(
                    "A data final deve ser posterior a inicial."
                );

            if (maxResults < 1)
                maxResults = 1000;

            if (maxResults > 10000)
                maxResults = 10000;

            return history.Query(
                start,
                end,
                tagId,
                maxResults
            );
        }
    }
}
