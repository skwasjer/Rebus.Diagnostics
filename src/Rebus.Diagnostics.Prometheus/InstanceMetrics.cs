using System;
using System.Diagnostics;
using System.Threading;
using Prometheus;
using Rebus.Bus;
using Rebus.Bus.Advanced;

namespace Rebus.Diagnostics.Prometheus
{
    internal sealed class InstanceMetrics : IDisposable
    {
        private bool _disposed;

        private readonly IAdvancedApi _advancedApi;
        private readonly BusLifetimeEvents _busLifetimeEvents;
        private readonly Gauge.Child _workersGauge;
        private readonly Timer _periodTimer;

        public InstanceMetrics(IAdvancedApi advancedApi, BusLifetimeEvents busLifetimeEvents, string busName)
        {
            _advancedApi = advancedApi;
            _busLifetimeEvents = busLifetimeEvents;
            _workersGauge = Counters.Instance.Workers.WithLabels(busName);

            _busLifetimeEvents.BusStarting += UpdateMetrics;
            _busLifetimeEvents.BusStarted += UpdateMetrics;
            _busLifetimeEvents.BusDisposing += UpdateMetrics;
            _busLifetimeEvents.BusDisposed += UpdateMetrics;
            _busLifetimeEvents.WorkersStopped += UpdateMetrics;

            _periodTimer = new Timer(_ => UpdateMetrics(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        private void UpdateMetrics()
        {
            Debug.WriteLine(_advancedApi.Workers.Count);
            _workersGauge.Set(_advancedApi.Workers.Count);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _periodTimer.Dispose();

                _busLifetimeEvents.BusStarting -= UpdateMetrics;
                _busLifetimeEvents.BusStarted -= UpdateMetrics;
                _busLifetimeEvents.BusDisposing -= UpdateMetrics;
                _busLifetimeEvents.BusDisposed -= UpdateMetrics;
                _busLifetimeEvents.WorkersStopped -= UpdateMetrics;

            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
