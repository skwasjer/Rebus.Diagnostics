using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prometheus;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Retry;
using Rebus.Retry.Simple;

namespace Rebus.Prometheus.Steps
{
    /// <summary>
    /// Incoming message step that instruments the incoming message.
    /// </summary>
    [StepDocumentation(@"Instruments individual incoming messages per type for total count, in-flight, duration, and error rate.")]
    public sealed class InstrumentIncomingStep : IIncomingStep
    {
        private readonly IMessageCounters _counters;
        private readonly IErrorTracker _errorTracker;

        internal InstrumentIncomingStep(IMessageCounters counters, IErrorTracker errorTracker)
        {
            _counters = counters;
            _errorTracker = errorTracker;
        }

        /// <inheritdoc />
        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            TransportMessage transportMessage = context.Load<TransportMessage>();
            string messageType = transportMessage.GetMessageType();

            _counters.Total.WithLabels(messageType).Inc();
            _counters.InFlight.WithLabels(messageType).Inc();
            ITimer messageInTimer = _counters.Duration.WithLabels(messageType).NewTimer();

            try
            {
                await next();

                // Check if error tracker contains an exception.
                // Grab message ID from message (if possible).
                Dictionary<string, string> messageHeaders = context.Load<Message>()?.Headers ?? transportMessage.Headers;
                string? messageId = messageHeaders.GetValueOrNull(Headers.MessageId);
                if (HasCausedError(messageId))
                {
                    _counters.Errors.WithLabels(messageType).Inc();
                }
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

        private bool HasCausedError(string? messageId)
        {
            return messageId is null
             || _errorTracker.GetExceptions(messageId).Any()
             || _errorTracker.GetExceptions(SimpleRetryStrategyStep.GetSecondLevelMessageId(messageId)).Any();
        }
    }
}
