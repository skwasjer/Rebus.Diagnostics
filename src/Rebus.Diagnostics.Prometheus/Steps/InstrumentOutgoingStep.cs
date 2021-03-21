using System;
using System.Threading.Tasks;
using Prometheus;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Diagnostics.Prometheus.Internal;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;

namespace Rebus.Diagnostics.Prometheus.Steps
{
    /// <summary>
    /// Incoming message step that instruments the incoming message.
    /// </summary>
    [StepDocumentation(@"Instruments individual outgoing messages per type for total count, in-flight, duration, and error rate.")]
    public sealed class InstrumentOutgoingStep : IOutgoingStep
    {
        private readonly IMessageCounters _counters;
        private readonly Options _options;

        internal InstrumentOutgoingStep(IMessageCounters counters, Options options)
        {
            _counters = counters;
            _options = options;
        }

        /// <inheritdoc />
        public async Task Process(OutgoingStepContext context, Func<Task> next)
        {
            Message message = context.Load<Message>();
            string messageType = message.GetMessageType();
            string busName = _options.OptionalBusName ?? BusNameHelper.GetBusName(context.Load<ITransactionContext>());
            string[] values = { busName, messageType };

            _counters.Total.WithLabels(values).Inc();
            Gauge.Child inFlight = _counters.InFlight.WithLabels(values);
            inFlight.Inc();
            ITimer messageInTimer = _counters.Duration.WithLabels(values).NewTimer();

            try
            {
                await next();
            }
            catch
            {
                _counters.Errors.WithLabels(values).Inc();
                throw;
            }
            finally
            {
                inFlight.Dec();
                messageInTimer.Dispose();
            }
        }
    }
}
