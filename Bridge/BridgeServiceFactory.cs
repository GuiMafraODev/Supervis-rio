
using System;
using System.Collections.Generic;
using System.IO;
using SupervisorioECA.Bridge.Opc;
using SupervisorioECA.Bridge.Communication;
using SupervisorioECA.Bridge.Storage;
using SupervisorioECA.Bridge.Alarms;
using SupervisorioECA.Bridge.Api;

namespace SupervisorioECA.Bridge
{
    public class BridgeServiceFactory : IDisposable
    {
        public BridgeLogger Logger { get; private set; }
        public OpcTagRepository TagRepository { get; private set; }
        public IList<OpcTagDefinition> Tags { get; private set; }

        public IOpcClient OpcClient { get; private set; }
        public OpcConnectionManager ConnectionManager { get; private set; }
        public OpcAcquisitionService Acquisition { get; private set; }

        public TrendStore TrendStore { get; private set; }
        public TrendAcquisitionService TrendAcquisition { get; private set; }

        public AlarmService AlarmService { get; private set; }

        public EventHistoryService EventHistory { get; private set; }
        public EventHistoryApiService EventHistoryApi { get; private set; }
        public BridgeEventCoordinator EventCoordinator { get; private set; }

        public RetentionService Retention { get; private set; }
        public AlarmAcquisitionBinding AlarmBinding { get; private set; }
        public BridgeRuntime Runtime { get; private set; }

        public ApiDataService ApiData { get; private set; }
        public ApiController ApiController { get; private set; }
        public ApiRequestHandler ApiHandler { get; private set; }

        public TrendExportService TrendExport { get; private set; }
        public TrendExportController TrendExportController { get; private set; }
        public ApiHttpServer HttpServer { get; private set; }

        private readonly string root;

        public BridgeServiceFactory(string projectRoot)
        {
            if (String.IsNullOrEmpty(projectRoot))
                throw new ArgumentException(
                    "Diretorio do projeto nao informado."
                );

            root = Path.GetFullPath(projectRoot);
        }

        public void Build()
        {
            string data = Path.Combine(root, "Data");
            string config = Path.Combine(data, "Config");
            string events = Path.Combine(data, "Events");
            string trends = Path.Combine(data, "Trends");
            string backup = Path.Combine(data, "Backup");
            string web = Path.Combine(root, "Web");

            Directory.CreateDirectory(config);
            Directory.CreateDirectory(events);
            Directory.CreateDirectory(trends);
            Directory.CreateDirectory(backup);

            Logger = new BridgeLogger(
                events,
                "bridge.log",
                5,
                10
            );

            TagRepository = new OpcTagRepository(
                Path.Combine(config, "tags.json")
            );

            Tags = TagRepository.Load();

            OpcClient = new RslinxOpcClient(
                "RSLinx OPC Server",
                "localhost",
                Tags
            );

            ConnectionManager = new OpcConnectionManager(
                OpcClient,
                Logger,
                5000
            );

            Acquisition = new OpcAcquisitionService(
                OpcClient,
                1000
            );

            TrendStore = new TrendStore(trends);

            TrendAcquisition = new TrendAcquisitionService(
                Acquisition,
                TrendStore,
                5000
            );

            // Serviço de alarmes.
            // O próprio AlarmService cria seu repositório
            // de definições e seu armazenamento de eventos.
            AlarmService = new AlarmService(
                Tags,
                Path.Combine(config, "alarms.json"),
                events
            );

            // Histórico unificado.
            EventHistory = new EventHistoryService(events);

            EventCoordinator = new BridgeEventCoordinator(
                EventHistory,
                Logger
            );

            // Conecta os eventos de alarme ao histórico unificado.
            AlarmService.AlarmEventRecorded +=
                EventCoordinator.RegisterAlarm;

            EventHistoryApi = new EventHistoryApiService(
                EventHistory
            );

            AlarmBinding = new AlarmAcquisitionBinding(
                Acquisition,
                AlarmService,
                Logger
            );

            Retention = new RetentionService(
                trends,
                events,
                backup,
                30
            );

            Runtime = new BridgeRuntime(
                ConnectionManager,
                Acquisition,
                TrendAcquisition,
                AlarmBinding,
                Retention,
                Logger
            );

            ApiData = new ApiDataService(
                Tags,
                Acquisition,
                AlarmService,
                TrendStore,
                delegate { return OpcClient.IsConnected; },
                delegate { return OpcClient.ServerName; }
            );

            ApiController = new ApiController(
                ApiData,
                EventHistoryApi
            );

            ApiHandler = new ApiRequestHandler(
                ApiController
            );

            TrendExport = new TrendExportService(
                TrendStore
            );

            TrendExportController = new TrendExportController(
                TrendExport,
                backup
            );

            HttpServer = new ApiHttpServer(
                8765,
                web,
                ApiHandler,
                TrendExportController,
                Logger
            );

            Logger.Info(
                "Componentes da bridge preparados."
            );
        }

        public void Start()
        {
            if (Runtime == null || HttpServer == null)
                throw new InvalidOperationException(
                    "Execute Build antes de Start."
                );

            HttpServer.Start();
            Runtime.Start();
        }

        public void Stop()
        {
            if (Runtime != null)
                Runtime.Stop();

            if (HttpServer != null)
                HttpServer.Stop();
        }

        public void Dispose()
        {
            Stop();

            if (HttpServer != null)
                HttpServer.Dispose();

            if (Runtime != null)
                Runtime.Dispose();

            if (OpcClient != null)
                OpcClient.Dispose();
        }
    }
}
