using System.Threading;
using System.Threading.Tasks;
using Prometheus;
using Rebus.Config;
using Rebus.Diagnostics.Prometheus.Internal;
using Rebus.Messages;
using Rebus.Transport;

namespace Rebus.Diagnostics.Prometheus
{
    internal class TransportMetrics : ITransport
    {
        private readonly ITransport _decoratee;
        private readonly Options _options;

        public TransportMetrics(ITransport decoratee, Options options)
        {
            _decoratee = decoratee;
            _options = options;
        }

        public void CreateQueue(string address)
        {
            _decoratee.CreateQueue(address);
        }

        public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
        {
            InstrumentMessage(context, Counters.OutgoingTransport);
            return _decoratee.Send(destinationAddress, message, context);
        }

        public async Task<TransportMessage?> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            TransportMessage message = await _decoratee.Receive(context, cancellationToken);
            if (message is not null)
            {
                InstrumentMessage(context, Counters.IncomingTransport);
            }

            return message;
        }

        public string Address => _decoratee.Address;

        private void InstrumentMessage(ITransactionContext context, IMessageCounters counters)
        {
            string busName = _options.OptionalBusName ?? BusNameHelper.GetBusName(context);

            counters.Total.WithLabels(busName).Inc();
            Gauge.Child inFlight = counters.InFlight.WithLabels(busName);
            inFlight.Inc();
            ITimer messageInTimer = counters.Duration.WithLabels(busName).NewTimer();

            context.OnAborted(_ => counters.Aborted.WithLabels(busName).Inc());
            context.OnDisposed(_ =>
            {
                inFlight.Dec();
                messageInTimer.Dispose();
            });
        }
    }
}
