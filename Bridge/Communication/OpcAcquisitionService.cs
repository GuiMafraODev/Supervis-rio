
using System;
using System.Collections.Generic;
using System.Threading;
using SupervisorioECA.Bridge.Opc;

namespace SupervisorioECA.Bridge.Communication
{
    public class OpcValuesUpdatedEventArgs : EventArgs
    {
        public IList<OpcValue> Values { get; private set; }

        public DateTime Timestamp { get; private set; }

        public OpcValuesUpdatedEventArgs(IList<OpcValue> values)
        {
            Values = values;
            Timestamp = DateTime.Now;
        }
    }

    public class OpcAcquisitionService : IDisposable
    {
        private readonly IOpcClient client;
        private readonly int intervalMs;

        private readonly object syncRoot = new object();

        private readonly Dictionary<string, OpcValue> latestValues =
            new Dictionary<string, OpcValue>(
                StringComparer.OrdinalIgnoreCase
            );

        private Timer timer;
        private bool running;
        private bool disposed;
        private int acquisitionInProgress;

        public event EventHandler<OpcValuesUpdatedEventArgs>
            ValuesUpdated;

        public event EventHandler AcquisitionFailed;

        public OpcAcquisitionService(
            IOpcClient client,
            int intervalMs)
        {
            if (client == null)
                throw new ArgumentNullException("client");

            if (intervalMs < 100)
                throw new ArgumentException(
                    "Intervalo de aquisicao invalido."
                );

            this.client = client;
            this.intervalMs = intervalMs;
        }

        public void Start()
        {
            lock (syncRoot)
            {
                if (disposed)
                    throw new ObjectDisposedException(
                        "OpcAcquisitionService"
                    );

                if (running)
                    return;

                running = true;

                timer = new Timer(
                    AcquisitionCallback,
                    null,
                    0,
                    intervalMs
                );
            }
        }

        public void Stop()
        {
            Timer currentTimer = null;

            lock (syncRoot)
            {
                if (!running)
                    return;

                running = false;

                currentTimer = timer;
                timer = null;
            }

            if (currentTimer != null)
            {
                currentTimer.Change(
                    Timeout.Infinite,
                    Timeout.Infinite
                );

                currentTimer.Dispose();
            }
        }

        public IDictionary<string, OpcValue> GetLatestValues()
        {
            Dictionary<string, OpcValue> result =
                new Dictionary<string, OpcValue>(
                    StringComparer.OrdinalIgnoreCase
                );

            lock (syncRoot)
            {
                foreach (
                    KeyValuePair<string, OpcValue> item
                    in latestValues)
                {
                    result[item.Key] = item.Value;
                }
            }

            return result;
        }

        private void AcquisitionCallback(object state)
        {
            if (Interlocked.Exchange(
                ref acquisitionInProgress, 1) != 0)
                return;

            try
            {
                lock (syncRoot)
                {
                    if (!running || disposed)
                        return;
                }

                if (!client.IsConnected)
                    return;

                IList<OpcValue> values = client.ReadAll();

                if (values == null)
                    return;

                lock (syncRoot)
                {
                    if (!running || disposed)
                        return;

                    foreach (OpcValue value in values)
                    {
                        if (value == null ||
                            String.IsNullOrEmpty(value.TagId))
                            continue;

                        // Guarda tambem leituras ruins.
                        // Assim, um valor antigo nao continua
                        // aparecendo como se fosse atual.
                        latestValues[value.TagId] = value;
                    }
                }

                RaiseValuesUpdated(values);
            }
            catch
            {
                RaiseAcquisitionFailed();
            }
            finally
            {
                Interlocked.Exchange(
                    ref acquisitionInProgress, 0
                );
            }
        }

        private void RaiseValuesUpdated(
            IList<OpcValue> values)
        {
            EventHandler<OpcValuesUpdatedEventArgs> handler =
                ValuesUpdated;

            if (handler == null)
                return;

            try
            {
                handler(
                    this,
                    new OpcValuesUpdatedEventArgs(
                        new List<OpcValue>(values)
                    )
                );
            }
            catch
            {
                // Falha em um assinante nao interrompe
                // o ciclo de aquisicao.
            }
        }

        private void RaiseAcquisitionFailed()
        {
            EventHandler handler = AcquisitionFailed;

            if (handler == null)
                return;

            try
            {
                handler(this, EventArgs.Empty);
            }
            catch
            {
                // Mantem o servico de aquisicao ativo.
            }
        }

        public void Dispose()
        {
            Stop();

            lock (syncRoot)
            {
                if (disposed)
                    return;

                disposed = true;
                latestValues.Clear();
            }
        }
    }
}
