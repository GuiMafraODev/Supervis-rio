using System;
using System.Threading;
using SupervisorioECA.Bridge.Opc;

namespace SupervisorioECA.Bridge.Communication
{
    public class OpcConnectionManager : IDisposable
    {
        private readonly IOpcClient client;
        private readonly int reconnectIntervalMs;

        private readonly object syncRoot = new object();

        private Timer reconnectTimer;

        private bool disposed;
        private bool running;
        private bool lastKnownConnected;

        public bool IsConnected
        {
            get
            {
                lock (syncRoot)
                {
                    if (disposed)
                        return false;

                    return client.IsConnected;
                }
            }
        }

        public event EventHandler ConnectionStateChanged;

        public OpcConnectionManager(
            IOpcClient client,
            int reconnectIntervalMs)
        {
            if (client == null)
                throw new ArgumentNullException("client");

            if (reconnectIntervalMs < 1000)
            {
                throw new ArgumentOutOfRangeException(
                    "reconnectIntervalMs"
                );
            }

            this.client = client;
            this.reconnectIntervalMs = reconnectIntervalMs;

            lastKnownConnected = false;
        }

        public void Start()
        {
            bool stateChanged = false;

            lock (syncRoot)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(
                        "OpcConnectionManager"
                    );
                }

                if (running)
                    return;

                running = true;

                bool connected = client.IsConnected;

                if (connected != lastKnownConnected)
                {
                    lastKnownConnected = connected;
                    stateChanged = true;
                }

                reconnectTimer = new Timer(
                    ReconnectCallback,
                    null,
                    0,
                    reconnectIntervalMs
                );
            }

            if (stateChanged)
                RaiseConnectionStateChanged();
        }

        public void Stop()
        {
            bool stateChanged = false;

            lock (syncRoot)
            {
                if (disposed)
                    return;

                running = false;

                if (reconnectTimer != null)
                {
                    reconnectTimer.Dispose();
                    reconnectTimer = null;
                }

                try
                {
                    if (client.IsConnected)
                        client.Disconnect();
                }
                catch (Exception ex)
                {
                    LogError(
                        "Erro ao desconectar OPC: " + ex.Message
                    );
                }

                bool connected = SafeGetConnectionState();

                if (connected != lastKnownConnected)
                {
                    lastKnownConnected = connected;
                    stateChanged = true;
                }
            }

            if (stateChanged)
                RaiseConnectionStateChanged();
        }

        private void ReconnectCallback(object state)
        {
            bool stateChanged = false;

            lock (syncRoot)
            {
                if (disposed || !running)
                    return;

                try
                {
                    bool connected = client.IsConnected;

                    if (connected)
                    {
                        if (!lastKnownConnected)
                        {
                            lastKnownConnected = true;
                            stateChanged = true;
                        }
                    }
                    else
                    {
                        if (lastKnownConnected)
                        {
                            lastKnownConnected = false;
                            stateChanged = true;
                        }

                        client.Connect();

                        connected = client.IsConnected;

                        if (connected != lastKnownConnected)
                        {
                            lastKnownConnected = connected;
                            stateChanged = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError(
                        "Falha na conexao OPC: " + ex.Message
                    );

                    bool connected = SafeGetConnectionState();

                    if (connected != lastKnownConnected)
                    {
                        lastKnownConnected = connected;
                        stateChanged = true;
                    }
                }
            }

            if (stateChanged)
                RaiseConnectionStateChanged();
        }

        private bool SafeGetConnectionState()
        {
            try
            {
                return client.IsConnected;
            }
            catch
            {
                return false;
            }
        }

        private void RaiseConnectionStateChanged()
        {
            EventHandler handler = ConnectionStateChanged;

            if (handler == null)
                return;

            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogError(
                    "Erro ao notificar mudanca de estado OPC: " +
                    ex.Message
                );
            }
        }

        private void LogError(string message)
        {
            try
            {
                Console.WriteLine(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    " - " + message
                );
            }
            catch
            {
                // Evita que falhas de log interrompam o serviço.
            }
        }

        public void Dispose()
        {
            bool stateChanged = false;

            lock (syncRoot)
            {
                if (disposed)
                    return;

                disposed = true;
                running = false;

                if (reconnectTimer != null)
                {
                    reconnectTimer.Dispose();
                    reconnectTimer = null;
                }

                try
                {
                    if (client.IsConnected)
                        client.Disconnect();
                }
                catch (Exception ex)
                {
                    LogError(
                        "Erro ao desconectar OPC durante encerramento: " +
                        ex.Message
                    );
                }

                bool connected = SafeGetConnectionState();

                if (connected != lastKnownConnected)
                {
                    lastKnownConnected = connected;
                    stateChanged = true;
                }

                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    LogError(
                        "Erro ao liberar cliente OPC: " + ex.Message
                    );
                }
            }

            if (stateChanged)
                RaiseConnectionStateChanged();
        }
    }
}