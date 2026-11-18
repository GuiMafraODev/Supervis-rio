
using System;
using SupervisorioECA.Bridge.Storage;
using SupervisorioECA.Bridge.Alarms;

namespace SupervisorioECA.Bridge.Communication
{
    public class BridgeEventCoordinator
    {
        private readonly EventHistoryService history;
        private readonly BridgeLogger logger;

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
                history.AddAlarmEvent(alarmEvent);

                if (logger != null)
                {
                    logger.Info(
                        "Evento de alarme registrado: " +
                        alarmEvent.EventType +
                        " - " +
                        alarmEvent.TagId
                    );
                }
            }
            catch (Exception ex)
            {
                LogFailure(
                    "Falha ao registrar evento de alarme.",
                    ex
                );
            }
        }

        public void RegisterCommunication(
            CommunicationEvent communicationEvent)
        {
            if (communicationEvent == null)
                return;

            try
            {
                history.AddCommunicationEvent(
                    communicationEvent
                );

                if (logger != null)
                {
                    logger.Info(
                        "Evento de comunicacao registrado: " +
                        communicationEvent.EventType
                    );
                }
            }
            catch (Exception ex)
            {
                LogFailure(
                    "Falha ao registrar evento de comunicacao.",
                    ex
                );
            }
        }

        private void LogFailure(
            string message,
            Exception exception)
        {
            if (logger == null)
                return;

            logger.Error(
                message + " " + exception.Message
            );
        }
    }
}
