using System;
using System.Threading.Tasks;
using Prometheus;
using Rebus.Bus;
using Rebus.Messages;
using Rebus.Pipeline;

namespace Rebus.Diagnostics.Prometheus.Steps
{
    /// <summary>
    /// Incoming message step that instruments the incoming message.
    /// </summary>
    [StepDocumentation(@"Instruments individual outgoing messages per type for total count, in-flight, duration, and error rate.")]
    public sealed class InstrumentOutgoingStep : IOutgoingStep
    {
        private readonly IMessageCounters _counters;

        internal InstrumentOutgoingStep(IMessageCounters counters)
        {
            _counters = counters;
        }

        /// <inheritdoc />
        public async Task Process(OutgoingStepContext context, Func<Task> next)
        {
            Message message = context.Load<Message>();
            string messageType = message.GetMessageType();

            _counters.Total.WithLabels(messageType).Inc();
            _counters.InFlight.WithLabels(messageType).Inc();
            ITimer messageInTimer = _counters.Duration.WithLabels(messageType).NewTimer();

            try
            {
                await next();
            }
            catch
            {
                _counters.Errors.WithLabels(messageType).Inc();
                throw;
            }
            finally
            {
                _counters.InFlight.WithLabels(messageType).Dec();
                messageInTimer.Dispose();
            }
        }
    }
}
