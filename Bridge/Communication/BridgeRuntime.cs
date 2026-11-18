
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
                throw new ArgumentNullException("connectionManager");

            if (acquisitionService == null)
                throw new ArgumentNullException("acquisitionService");

            if (trendService == null)
                throw new ArgumentNullException("trendService");

            if (alarmBinding == null)
                throw new ArgumentNullException("alarmBinding");

            this.connectionManager = connectionManager;
            this.acquisitionService = acquisitionService;
            this.trendService = trendService;
            this.alarmBinding = alarmBinding;
            this.retentionService = retentionService;
            this.logger = logger;
        }

        public bool IsStarted
        {
            get
            {
                lock (syncRoot)
                {
                    return started;
                }
            }
        }

        public void Start()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(
                        "BridgeRuntime"
                    );
                }

                if (started)
                    return;

                try
                {
                    LogInfo(
                        "Inicializando Supervisorio ECA."
                    );

                    ExecuteRetention();

                    // Prepara primeiro a integracao dos alarmes.
                    alarmBinding.Start();

                    // Inicia as tentativas de conexao OPC.
                    connectionManager.Start();

                    // Inicia a leitura periodica das variaveis.
                    acquisitionService.Start();

                    // Inicia a gravacao historica.
                    trendService.Start();

                    started = true;

                    LogInfo(
                        "Servicos do Supervisorio ECA iniciados."
                    );
                }
                catch (Exception ex)
                {
                    started = false;

                    StopServices();

                    LogError(
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
                if (disposed || !started)
                    return;

                // Atualiza o estado antes de iniciar o encerramento.
                started = false;

                StopServices();

                LogInfo(
                    "Supervisorio ECA encerrado."
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

                if (result == null)
                {
                    LogWarning(
                        "Retencao nao retornou resultado."
                    );

                    return;
                }

                LogInfo(
                    "Retencao executada. Arquivos removidos: " +
                    result.DeletedFiles +
                    ". Arquivos ignorados: " +
                    result.SkippedFiles +
                    "."
                );

                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        LogWarning(
                            "Erro na retencao: " + error
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(
                    "Falha ao executar a retencao.",
                    ex
                );

                // A falha de retencao nao impede a comunicacao OPC.
            }
        }

        private void StopServices()
        {
            // 1. Interrompe primeiro a gravacao historica.
            try
            {
                trendService.Stop();
            }
            catch (Exception ex)
            {
                LogStopError(
                    "TrendAcquisitionService",
                    ex
                );
            }

            // 2. Impede novas avaliacoes de alarmes.
            try
            {
                alarmBinding.Stop();
            }
            catch (Exception ex)
            {
                LogStopError(
                    "AlarmAcquisitionBinding",
                    ex
                );
            }

            // 3. Aguarda e interrompe a aquisicao OPC.
            try
            {
                acquisitionService.Stop();
            }
            catch (Exception ex)
            {
                LogStopError(
                    "OpcAcquisitionService",
                    ex
                );
            }

            // 4. Desconecta o RSLinx por ultimo.
            try
            {
                connectionManager.Stop();
            }
            catch (Exception ex)
            {
                LogStopError(
                    "OpcConnectionManager",
                    ex
                );
            }
        }

        private void LogStopError(
            string service,
            Exception exception)
        {
            LogError(
                "Erro ao encerrar " + service + ".",
                exception
            );
        }

        private void LogInfo(string message)
        {
            if (logger == null)
                return;

            try
            {
                logger.Info(message);
            }
            catch
            {
                // Falhas de log nao devem interromper o runtime.
            }
        }

        private void LogWarning(string message)
        {
            if (logger == null)
                return;

            try
            {
                logger.Warning(message);
            }
            catch
            {
                // Falhas de log nao devem interromper o runtime.
            }
        }

        private void LogError(
            string message,
            Exception exception)
        {
            if (logger == null)
                return;

            try
            {
                logger.Error(message, exception);
            }
            catch
            {
                // Falhas de log nao devem interromper o runtime.
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;

                // Bloqueia novas inicializacoes imediatamente.
                disposed = true;
                started = false;

                // Encerra os servicos na ordem correta.
                StopServices();

                // Libera os recursos na ordem inversa de dependencia.
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

                LogInfo(
                    "Recursos do Supervisorio ECA liberados."
                );
            }
        }
    }
}
