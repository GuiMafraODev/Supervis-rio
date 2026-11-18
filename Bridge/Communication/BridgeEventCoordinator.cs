
using System;
using SupervisorioECA.Bridge.Storage;
using SupervisorioECA.Bridge.Alarms;

namespace SupervisorioECA.Bridge.Communication
{
    public sealed class BridgeEventCoordinator
    {
        private readonly EventHistoryService history;
        private readonly BridgeLogger logger;
        private readonly object syncRoot = new object();

        public BridgeEventCoordinator(
            EventHistoryService history,
            BridgeLogger logger)
        {
            if (history == null)
                throw new ArgumentNullException("history");

            this.history = history;
            this.logger = logger;
        }

        public void RegisterAlarm(AlarmEvent alarmEvent)
        {
            if (alarmEvent == null)
                return;

            try
            {
                lock (syncRoot)
                {
                    history.AddAlarmEvent(alarmEvent);
                }
            }
            catch (Exception ex)
            {
                LogError(
                    "Falha ao registrar evento de alarme",
                    ex);
            }
        }

        public void RegisterCommunication(
            CommunicationEvent communicationEvent)
        {
            if (communicationEvent == null)
                return;

            try
            {
                lock (syncRoot)
                {
                    history.AddCommunicationEvent(
                        communicationEvent);
                }
            }
            catch (Exception ex)
            {
                LogError(
                    "Falha ao registrar evento de comunicacao",
                    ex);
            }
        }

        public void RegisterSystemEvent(
            string eventType,
            string description,
            string source,
            bool isError)
        {
            try
            {
                UnifiedEvent item = new UnifiedEvent();

                item.Id = Guid.NewGuid().ToString("N");
                item.Timestamp = DateTime.Now;
                item.EventType = eventType ?? "SYSTEM";
                item.Description = description ?? "";
                item.Source = source ?? "SUPERVISORIO";
                item.IsError = isError;
                item.Category = "SYSTEM";

                lock (syncRoot)
                {
                    history.Add(item);
                }
            }
            catch (Exception ex)
            {
                LogError(
                    "Falha ao registrar evento do sistema",
                    ex);
            }
        }

        private void LogError(
            string message,
            Exception ex)
        {
            if (logger == null)
                return;

            try
            {
                logger.Error(
                    message + ": " + ex.Message);
            }
            catch
            {
                // Falha de log nao deve interromper a aquisicao.
            }
        }
    }
}
