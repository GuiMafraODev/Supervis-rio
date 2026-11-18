
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
        private bool connecting;

        public bool IsConnected
        {
            get { return client.IsConnected; }
        }

        public event EventHandler ConnectionStateChanged;

        public OpcConnectionManager(
            IOpcClient client,
            int reconnectIntervalMs)
        {
            if (client == null)
                throw new ArgumentNullException("client");

            if (reconnectIntervalMs < 1000)
                throw new ArgumentOutOfRangeException(
                    "reconnectIntervalMs"
                );

            this.client = client;
            this.reconnectIntervalMs = reconnectIntervalMs;
        }

        public void Start()
        {
            lock (syncRoot)
            {
                if (disposed)
                    throw new ObjectDisposedException(
                        "OpcConnectionManager"
                    );

                if (reconnectTimer != null)
                    return;

                reconnectTimer = new Timer(
                    ReconnectCallback,
                    null,
                    0,
                    reconnectIntervalMs
                );
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                if (reconnectTimer != null)
                {
                    reconnectTimer.Dispose();
                    reconnectTimer = null;
                }
            }

            lock (syncRoot)
            {
                if (client.IsConnected)
                    client.Disconnect();
            }
        }

        private void ReconnectCallback(object state)
        {
            lock (syncRoot)
            {
                if (disposed || connecting || client.IsConnected)
                    return;

                connecting = true;
            }

            try
            {
                client.Connect();

                RaiseConnectionStateChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    " - Falha na conexao OPC: " + ex.Message
                );

                RaiseConnectionStateChanged();
            }
            finally
            {
                lock (syncRoot)
                {
                    connecting = false;
                }
            }
        }

        private void RaiseConnectionStateChanged()
        {
            EventHandler handler = ConnectionStateChanged;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;

                disposed = true;

                if (reconnectTimer != null)
                {
                    reconnectTimer.Dispose();
                    reconnectTimer = null;
                }
            }

            if (client.IsConnected)
                client.Disconnect();

            client.Dispose();
        }
    }
}
