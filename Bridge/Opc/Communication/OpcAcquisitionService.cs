
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
                throw new ArgumentOutOfRangeException(
                    "acquisitionIntervalMs"
                );

            this.client = client;
            this.acquisitionIntervalMs = acquisitionIntervalMs;
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
            lock (syncRoot)
            {
                running = false;

                if (acquisitionTimer != null)
                {
                    acquisitionTimer.Dispose();
                    acquisitionTimer = null;
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
                    RaiseAcquisitionError();
                    return;
                }

                IList<OpcValue> values = client.ReadAll();

                lock (syncRoot)
                {
                    foreach (OpcValue value in values)
                    {
                        if (value == null ||
                            String.IsNullOrEmpty(value.TagId))
                            continue;

                        if (value.IsGood())
                        {
                            latestValues[value.TagId] = value;
                        }
                        else
                        {
                            OpcValue previous;

                            if (latestValues.TryGetValue(
                                value.TagId, out previous))
                            {
                                OpcValue unavailable =
                                    OpcValue.CreateBad(
                                        value.TagId,
                                        value.Quality,
                                        value.ErrorMessage
                                    );

                                latestValues[value.TagId] = unavailable;
                            }
                            else
                            {
                                latestValues[value.TagId] = value;
                            }
                        }
                    }
                }

                RaiseValuesUpdated();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Erro na aquisicao OPC: " + ex.Message
                );

                RaiseAcquisitionError();
            }
            finally
            {
                lock (syncRoot)
                {
                    acquiring = false;
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

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void RaiseAcquisitionError()
        {
            EventHandler handler = AcquisitionError;

            if (handler != null)
                handler(this, EventArgs.Empty);
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
