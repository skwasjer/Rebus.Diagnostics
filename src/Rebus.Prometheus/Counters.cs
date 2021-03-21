using Prometheus;

namespace Rebus.Prometheus
{
    internal static class Counters
    {
        internal static class Instance
        {
            private static Gauge? _workers;
            public static Gauge Workers => _workers ??= Metrics.CreateGauge(
                "messaging_workers_total",
                "The total number of workers processing messages.",
                "instance"
            );
        }

        private class IncomingCounters : IMessageCounters
        {
            private Counter? _messagesInTotal;
            public Counter Total => _messagesInTotal ??= Metrics.CreateCounter(
                "messaging_incoming_total",
                "The total incoming messages."
            );

            private Gauge? _inflight;
            public Gauge InFlight => _inflight ??= Metrics.CreateGauge(
                "messaging_incoming_in_flight_total",
                "The total incoming messages currently being processed."
            );

            //public static readonly Counter MessagesInErrors = Metrics.CreateCounter(
            //    "messaging_incoming_errors_total",
            //    "The total of incoming messages that failed to be processed."
            //);

            private Counter? _messagesInAborted;
            public Counter Aborted => _messagesInAborted ??= Metrics.CreateCounter(
                "messaging_incoming_aborted_total",
                "The total of incoming messages for which the transaction was aborted."
            );

            private Histogram? _messagesInDuration;
            public Histogram Duration => _messagesInDuration ??= Metrics.CreateHistogram(
                "messaging_incoming_duration_seconds",
                "The duration of incoming messages processed.",
                new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.001, 2, 16) }
            );
        }

        private class OutgoingCounters : IMessageCounters
        {
            private Counter? _total;
            public Counter Total => _total ??= Metrics.CreateCounter(
                "messaging_outgoing_total",
                "The total outgoing messages."
            );

            private Gauge? _inflight;
            public Gauge InFlight => _inflight ??= Metrics.CreateGauge(
                "messaging_outgoing_in_flight_total",
                "The total outgoing messages currently being sent."
            );

            //public static readonly Counter MessagesOutErrors = Metrics.CreateCounter(
            //    "messaging_outgoing_errors_total",
            //    "The total of outgoing messages that failed to be sent."
            //);

            private Counter? _aborted;
            public Counter Aborted => _aborted ??= Metrics.CreateCounter(
                "messaging_outgoing_aborted_total",
                "The total of outgoing messages for which the transaction was aborted."
            );

            private Histogram? _duration;
            public Histogram Duration => _duration ??= Metrics.CreateHistogram(
                "messaging_outgoing_duration_seconds",
                "The duration of outgoing messages sent.",
                new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.001, 2, 16) }
            );
        }

        internal static IMessageCounters Incoming { get; } = new IncomingCounters();
        internal static IMessageCounters Outgoing { get; } = new OutgoingCounters();
    }
}
