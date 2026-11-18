
using System;
using System.Collections.Generic;
using SupervisorioECA.Bridge.Alarms;
using SupervisorioECA.Bridge.Opc;

namespace SupervisorioECA.Bridge.Communication
{
    public class AlarmAcquisitionBinding : IDisposable
    {
        private readonly OpcAcquisitionService acquisition;
        private readonly AlarmService alarmService;
        private readonly BridgeLogger logger;

        private bool started;
        private bool disposed;

        private readonly object syncRoot = new object();

        public AlarmAcquisitionBinding(
            OpcAcquisitionService acquisition,
            AlarmService alarmService,
            BridgeLogger logger)
        {
            if (acquisition == null)
                throw new ArgumentNullException("acquisition");

            if (alarmService == null)
                throw new ArgumentNullException("alarmService");

            this.acquisition = acquisition;
            this.alarmService = alarmService;
            this.logger = logger;
        }

        public void Start()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(
                        "AlarmAcquisitionBinding"
                    );
                }

                if (started)
                    return;

                acquisition.ValuesUpdated += OnValuesUpdated;

                started = true;
            }

            if (logger != null)
            {
                logger.Info(
                    "Integracao entre aquisicao OPC e alarmes iniciada."
                );
            }
        }

        public void Stop()
        {
            bool wasStarted;

            lock (syncRoot)
            {
                wasStarted = started;

                if (!started)
                    return;

                acquisition.ValuesUpdated -= OnValuesUpdated;

                started = false;
            }

            if (wasStarted && logger != null)
            {
                logger.Info(
                    "Integracao entre aquisicao OPC e alarmes encerrada."
                );
            }
        }

        private void OnValuesUpdated(
            object sender,
            EventArgs args)
        {
            lock (syncRoot)
            {
                if (!started || disposed)
                    return;
            }

            try
            {
                IList<OpcValue> values =
                    acquisition.GetLatestValues();

                if (values == null || values.Count == 0)
                    return;

                alarmService.Evaluate(values);
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.Error(
                        "Falha ao avaliar alarmes recebidos via OPC.",
                        ex
                    );
                }
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;
            }

            Stop();

            lock (syncRoot)
            {
                disposed = true;
            }
        }
    }
}
