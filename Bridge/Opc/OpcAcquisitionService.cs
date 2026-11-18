
using System;
using System.Collections.Generic;
using System.Threading;
using SupervisorioECA.Bridge.Opc;

namespace SupervisorioECA.Bridge.Communication
{
    public class OpcAcquisitionService : IDisposable
    {
        private readonly IOpcClient client;
        private readonly int acquisitionIntervalMs;

        private readonly object syncRoot = new object();

        private Timer acquisitionTimer;

        private bool running;
        private bool acquiring;
        private bool disposed;

        private readonly Dictionary<string, OpcValue> latestValues =
            new Dictionary<string, OpcValue>();

        public event EventHandler ValuesUpdated;
        public event EventHandler AcquisitionError;

        public OpcAcquisitionService(
            IOpcClient client,
            int acquisitionIntervalMs)
        {
            if (client == null)
                throw new ArgumentNullException("client");

            if (acquisitionIntervalMs < 100)
            {
                throw new ArgumentOutOfRangeException(
                    "acquisitionIntervalMs"
                );
            }

            this.client = client;
            this.acquisitionIntervalMs = acquisitionIntervalMs;
        }

        public void Start()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(
                        "OpcAcquisitionService"
                    );
                }

                if (running)
                    return;

                running = true;

                acquisitionTimer = new Timer(
                    AcquisitionCallback,
                    null,
                    0,
                    acquisitionIntervalMs
                );
            }
        }

        public void Stop()
        {
            Timer timer;

            lock (syncRoot)
            {
                running = false;

                timer = acquisitionTimer;
                acquisitionTimer = null;
            }

            if (timer != null)
            {
                timer.Dispose();

                lock (syncRoot)
                {
                    while (acquiring)
                        Monitor.Wait(syncRoot);
                }
            }
        }

        private void AcquisitionCallback(object state)
        {
            lock (syncRoot)
            {
                if (!running || disposed || acquiring)
                    return;

                acquiring = true;
            }

            try
            {
                if (!client.IsConnected)
                {
                    MarkAllValuesBad(
                        "Cliente OPC desconectado."
                    );

                    RaiseAcquisitionError();
                    RaiseValuesUpdated();

                    return;
                }

                IList<OpcValue> values = client.ReadAll();

                if (values == null)
                {
                    throw new InvalidOperationException(
                        "Cliente OPC retornou uma lista nula."
                    );
                }

                lock (syncRoot)
                {
                    if (!running || disposed)
                        return;

                    for (int i = 0; i < values.Count; i++)
                    {
                        OpcValue value = values[i];

                        if (value == null ||
                            String.IsNullOrEmpty(value.TagId))
                            continue;

                        latestValues[value.TagId] = value;
                    }
                }

                RaiseValuesUpdated();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss"
                    ) +
                    " - Erro na aquisicao OPC: " +
                    ex.Message
                );

                MarkAllValuesBad(
                    "Falha na aquisicao OPC: " + ex.Message
                );

                RaiseAcquisitionError();
                RaiseValuesUpdated();
            }
            finally
            {
                lock (syncRoot)
                {
                    acquiring = false;
                    Monitor.PulseAll(syncRoot);
                }
            }
        }

        private void MarkAllValuesBad(string errorMessage)
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;

                List<string> tagIds =
                    new List<string>(latestValues.Keys);

                for (int i = 0; i < tagIds.Count; i++)
                {
                    string tagId = tagIds[i];

                    latestValues[tagId] =
                        OpcValue.CreateBad(
                            tagId,
                            "Bad",
                            errorMessage
                        );
                }
            }
        }

        public IList<OpcValue> GetLatestValues()
        {
            IList<OpcValue> result = new List<OpcValue>();

            lock (syncRoot)
            {
                foreach (OpcValue value in latestValues.Values)
                {
                    result.Add(value);
                }
            }

            return result;
        }

        public OpcValue GetLatestValue(string tagId)
        {
            lock (syncRoot)
            {
                OpcValue value;

                if (latestValues.TryGetValue(tagId, out value))
                    return value;

                return OpcValue.CreateBad(
                    tagId,
                    "Bad",
                    "Nenhuma leitura recebida."
                );
            }
        }

        private void RaiseValuesUpdated()
        {
            EventHandler handler = ValuesUpdated;

            if (handler == null)
                return;

            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Erro no evento ValuesUpdated: " +
                    ex.Message
                );
            }
        }

        private void RaiseAcquisitionError()
        {
            EventHandler handler = AcquisitionError;

            if (handler == null)
                return;

            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Erro no evento AcquisitionError: " +
                    ex.Message
                );
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
                if (disposed)
                    return;

                disposed = true;
                latestValues.Clear();
            }
        }
    }
}
