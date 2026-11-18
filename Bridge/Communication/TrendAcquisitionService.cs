
using System;
using System.Collections.Generic;
using System.Threading;
using SupervisorioECA.Bridge.Opc;
using SupervisorioECA.Bridge.Storage;

namespace SupervisorioECA.Bridge.Communication
{
    public class TrendAcquisitionService : IDisposable
    {
        private readonly OpcAcquisitionService acquisition;
        private readonly TrendStore store;
        private readonly int intervalMs;

        private readonly object syncRoot = new object();

        private Timer timer;
        private bool running;
        private bool acquiring;
        private bool disposed;

        public event EventHandler SamplesSaved;

        public event EventHandler StorageError;

        public TrendAcquisitionService(
            OpcAcquisitionService acquisition,
            TrendStore store,
            int intervalMs)
        {
            if (acquisition == null)
                throw new ArgumentNullException("acquisition");

            if (store == null)
                throw new ArgumentNullException("store");

            if (intervalMs < 1000)
                throw new ArgumentOutOfRangeException("intervalMs");

            this.acquisition = acquisition;
            this.store = store;
            this.intervalMs = intervalMs;
        }

        public void Start()
        {
            lock (syncRoot)
            {
                if (disposed)
                    throw new ObjectDisposedException(
                        "TrendAcquisitionService"
                    );

                if (running)
                    return;

                running = true;

                timer = new Timer(
                    SaveCallback,
                    null,
                    intervalMs,
                    intervalMs
                );
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                running = false;

                if (timer != null)
                {
                    timer.Dispose();
                    timer = null;
                }
            }
        }

        private void SaveCallback(object state)
        {
            lock (syncRoot)
            {
                if (!running || disposed || acquiring)
                    return;

                acquiring = true;
            }

            try
            {
                IList<OpcValue> values =
                    acquisition.GetLatestValues();

                IList<TrendSample> samples =
                    new List<TrendSample>();

                DateTime sampleTime = DateTime.Now;

                foreach (OpcValue value in values)
                {
                    if (value == null || !value.IsGood())
                        continue;

                    TrendSample sample =
                        TrendSample.FromOpcValue(value);

                    // O horario representa o momento da
                    // amostragem historica no supervisório.
                    sample.Timestamp = sampleTime;

                    samples.Add(sample);
                }

                if (samples.Count > 0)
                {
                    store.SaveBatch(samples);
                    RaiseSamplesSaved();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Falha ao gravar tendencias: " + ex.Message
                );

                RaiseStorageError();
            }
            finally
            {
                lock (syncRoot)
                {
                    acquiring = false;
                }
            }
        }

        private void RaiseSamplesSaved()
        {
            EventHandler handler = SamplesSaved;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void RaiseStorageError()
        {
            EventHandler handler = StorageError;

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
            }
        }
    }
}
