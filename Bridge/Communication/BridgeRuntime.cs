
using System;

namespace SupervisorioECA.Bridge.Communication
{
    public class BridgeRuntime : IDisposable
    {
        private readonly OpcConnectionManager connectionManager;
        private readonly OpcAcquisitionService acquisitionService;
        private readonly TrendAcquisitionService trendService;
        private readonly AlarmAcquisitionBinding alarmBinding;
        private readonly RetentionService retentionService;
        private readonly BridgeLogger logger;

        private readonly object syncRoot = new object();

        private bool started;
        private bool disposed;

        public BridgeRuntime(
            OpcConnectionManager connectionManager,
            OpcAcquisitionService acquisitionService,
            TrendAcquisitionService trendService,
            AlarmAcquisitionBinding alarmBinding,
            RetentionService retentionService,
            BridgeLogger logger)
        {
            if (connectionManager == null)
                throw new ArgumentNullException(
                    "connectionManager"
                );

            if (acquisitionService == null)
                throw new ArgumentNullException(
                    "acquisitionService"
                );

            if (trendService == null)
                throw new ArgumentNullException(
                    "trendService"
                );

            if (alarmBinding == null)
                throw new ArgumentNullException(
                    "alarmBinding"
                );

            this.connectionManager = connectionManager;
            this.acquisitionService = acquisitionService;
            this.trendService = trendService;
            this.alarmBinding = alarmBinding;
            this.retentionService = retentionService;
            this.logger = logger;
        }

        public void Start()
        {
            lock (syncRoot)
            {
                if (disposed)
                    throw new ObjectDisposedException(
                        "BridgeRuntime"
                    );

                if (started)
                    return;

                try
                {
                    if (logger != null)
                        logger.Info(
                            "Inicializando Supervisório ECA."
                        );

                    ExecuteRetention();

                    // Primeiro prepara a integracao dos alarmes.
                    alarmBinding.Start();

                    // Inicia as tentativas de conexao OPC.
                    connectionManager.Start();

                    // Inicia a leitura periodica das variaveis.
                    acquisitionService.Start();

                    // Inicia a gravacao historica.
                    trendService.Start();

                    started = true;

                    if (logger != null)
                        logger.Info(
                            "Servicos do Supervisório ECA iniciados."
                        );
                }
                catch (Exception ex)
                {
                    StopServices();

                    if (logger != null)
                        logger.Error(
                            "Falha durante a inicializacao da bridge.",
                            ex
                        );

                    throw;
                }
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                if (!started)
                    return;

                StopServices();

                started = false;

                if (logger != null)
                    logger.Info(
                        "Supervisório ECA encerrado."
                    );
            }
        }

        private void ExecuteRetention()
        {
            if (retentionService == null)
                return;

            try
            {
                RetentionResult result =
                    retentionService.Execute();

                if (logger != null)
                {
                    logger.Info(
                        "Retencao executada. Arquivos removidos: " +
                        result.DeletedFiles +
                        ". Arquivos ignorados: " +
                        result.SkippedFiles +
                        "."
                    );

                    foreach (string error in result.Errors)
                    {
                        logger.Warning(
                            "Erro na retencao: " + error
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                    logger.Error(
                        "Falha ao executar a retencao.",
                        ex
                    );

                // Falha na limpeza nao deve impedir
                // a inicializacao da comunicacao.
            }
        }

        private void StopServices()
        {
            // Para primeiro a gravacao historica.
            try
            {
                trendService.Stop();
            }
            catch (Exception ex)
            {
                LogStopError("TrendAcquisitionService", ex);
            }

            // Impede novas avaliacoes de alarmes.
            try
            {
                alarmBinding.Stop();
            }
            catch (Exception ex)
            {
                LogStopError("AlarmAcquisitionBinding", ex);
            }

            // Interrompe a leitura das variaveis.
            try
            {
                acquisitionService.Stop();
            }
            catch (Exception ex)
            {
                LogStopError("OpcAcquisitionService", ex);
            }

            // Por ultimo, encerra a conexao OPC.
            try
            {
                connectionManager.Stop();
            }
            catch (Exception ex)
            {
                LogStopError("OpcConnectionManager", ex);
            }
        }

        private void LogStopError(
            string service,
            Exception exception)
        {
            if (logger != null)
                logger.Error(
                    "Erro ao encerrar " + service + ".",
                    exception
                );
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;

                StopServices();

                try
                {
                    trendService.Dispose();
                }
                catch (Exception ex)
                {
                    LogStopError(
                        "TrendAcquisitionService.Dispose",
                        ex
                    );
                }

                try
                {
                    alarmBinding.Dispose();
                }
                catch (Exception ex)
                {
                    LogStopError(
                        "AlarmAcquisitionBinding.Dispose",
                        ex
                    );
                }

                try
                {
                    acquisitionService.Dispose();
                }
                catch (Exception ex)
                {
                    LogStopError(
                        "OpcAcquisitionService.Dispose",
                        ex
                    );
                }

                try
                {
                    connectionManager.Dispose();
                }
                catch (Exception ex)
                {
                    LogStopError(
                        "OpcConnectionManager.Dispose",
                        ex
                    );
                }

                started = false;
                disposed = true;
            }
        }
    }
}
