
using System;
using SupervisorioECA.Bridge.Alarms;

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
                    throw new ObjectDisposedException(
                        "AlarmAcquisitionBinding"
                    );

                if (started)
                    return;

                acquisition.ValuesUpdated +=
                    OnValuesUpdated;

                started = true;

                if (logger != null)
                    logger.Info(
                        "Integracao entre aquisicao OPC e alarmes iniciada."
                    );
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                if (!started)
                    return;

                acquisition.ValuesUpdated -=
                    OnValuesUpdated;

                started = false;

                if (logger != null)
                    logger.Info(
                        "Integracao entre aquisicao OPC e alarmes encerrada."
                    );
            }
        }

        private void OnValuesUpdated(
            object sender,
            OpcValuesUpdatedEventArgs args)
        {
            if (args == null || args.Values == null)
                return;

            try
            {
                alarmService.Evaluate(args.Values);
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.Error(
                        "Falha ao avaliar alarmes recebidos via OPC.",
                        ex
                    );
            }
        }

        public void Dispose()
        {
            Stop();

            lock (syncRoot)
            {
                disposed = true;
            }
        }
    }
}
