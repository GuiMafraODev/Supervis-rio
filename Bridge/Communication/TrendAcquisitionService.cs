
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
        private readonly TimeSpan maximumSampleAge;

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

            int freshnessLimit = Math.Max(
                10000,
                intervalMs * 2
            );

            maximumSampleAge = TimeSpan.FromMilliseconds(
                freshnessLimit
            );
        }

        public void Start()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(
                        "TrendAcquisitionService"
                    );
                }

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

                // Aguarda a gravacao em andamento.
                // Os eventos sao disparados somente depois
                // de liberar o estado acquiring.
                while (acquiring)
                    Monitor.Wait(syncRoot);
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

            bool samplesWereSaved = false;
            bool storageFailed = false;
            string errorMessage = null;

            try
            {
                IList<OpcValue> values =
                    acquisition.GetLatestValues();

                if (values == null || values.Count == 0)
                    return;

                IList<TrendSample> samples =
                    new List<TrendSample>();

                DateTime sampleTime = DateTime.Now;

                foreach (OpcValue value in values)
                {
                    if (value == null || !value.IsGood())
                        continue;

                    if (!IsFresh(value, sampleTime))
                        continue;

                    TrendSample sample =
                        TrendSample.FromOpcValue(value);

                    // Horario da amostragem historica.
                    sample.Timestamp = sampleTime;

                    samples.Add(sample);
                }

                if (samples.Count > 0)
                {
                    store.SaveBatch(samples);
                    samplesWereSaved = true;
                }
            }
            catch (Exception ex)
            {
                storageFailed = true;
                errorMessage = ex.Message;
            }
            finally
            {
                lock (syncRoot)
                {
                    acquiring = false;
                    Monitor.PulseAll(syncRoot);
                }
            }

            // Notificacoes fora do estado acquiring.
            // Isso evita deadlock se um assinante solicitar Stop().
            if (storageFailed)
            {
                Console.WriteLine(
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss"
                    ) +
                    " - Falha ao gravar tendencias: " +
                    errorMessage
                );

                RaiseStorageError();
            }

            if (samplesWereSaved)
                RaiseSamplesSaved();
        }

        private bool IsFresh(
            OpcValue value,
            DateTime referenceTime)
        {
            if (value.Timestamp == DateTime.MinValue)
                return false;

            TimeSpan age = referenceTime - value.Timestamp;

            // Rejeita leituras muito antigas e timestamps
            // excessivamente adiantados em relacao ao relogio local.
            if (age > maximumSampleAge)
                return false;

            if (age < TimeSpan.FromMinutes(-2))
                return false;

            return true;
        }

        private void RaiseSamplesSaved()
        {
            EventHandler handler = SamplesSaved;

            if (handler == null)
                return;

            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Erro no evento SamplesSaved: " +
                    ex.Message
                );
            }
        }

        private void RaiseStorageError()
        {
            EventHandler handler = StorageError;

            if (handler == null)
                return;

            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "Erro no evento StorageError: " +
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
            }
        }
    }
}
