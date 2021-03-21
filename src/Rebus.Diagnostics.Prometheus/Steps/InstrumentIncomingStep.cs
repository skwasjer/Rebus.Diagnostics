using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prometheus;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Diagnostics.Prometheus.Internal;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Retry;
using Rebus.Retry.Simple;
using Rebus.Transport;

namespace Rebus.Diagnostics.Prometheus.Steps
{
    /// <summary>
    /// Incoming message step that instruments the incoming message.
    /// </summary>
    [StepDocumentation(@"Instruments individual incoming messages per type for total count, in-flight, duration, and error rate.")]
    public sealed class InstrumentIncomingStep : IIncomingStep
    {
        private readonly IMessageCounters _counters;
        private readonly IErrorTracker _errorTracker;
        private readonly Options _options;

        internal InstrumentIncomingStep(IMessageCounters counters, IErrorTracker errorTracker, Options options)
        {
            _counters = counters;
            _errorTracker = errorTracker;
            _options = options;
        }

        /// <inheritdoc />
        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            TransportMessage transportMessage = context.Load<TransportMessage>();
            string messageType = transportMessage.GetMessageType();
            string busName = _options.OptionalBusName ?? BusNameHelper.GetBusName(context.Load<ITransactionContext>());
            string[] values = { busName, messageType };

            _counters.Total.WithLabels(values).Inc();
            Gauge.Child inFlight = _counters.InFlight.WithLabels(values);
            inFlight.Inc();
            ITimer messageInTimer = _counters.Duration.WithLabels(values).NewTimer();

            try
            {
                await next();

                // Check if error tracker contains an exception.
                // Grab message ID from message (if possible).
                Dictionary<string, string> messageHeaders = context.Load<Message>()?.Headers ?? transportMessage.Headers;
                string? messageId = messageHeaders.GetValueOrNull(Headers.MessageId);
                if (HasCausedError(messageId))
                {
                    _counters.Errors.WithLabels(values).Inc();
                }
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

        private bool HasCausedError(string? messageId)
        {
            return messageId is null
             || _errorTracker.GetExceptions(messageId).Any()
             || _errorTracker.GetExceptions(SimpleRetryStrategyStep.GetSecondLevelMessageId(messageId)).Any();
        }
    }
}
