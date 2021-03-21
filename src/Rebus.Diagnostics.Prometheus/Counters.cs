using System;
using Prometheus;

namespace Rebus.Diagnostics.Prometheus
{
    internal static class Counters
    {
        private static class LabelNames
        {
            private const string Type = "type";
            private const string Instance = "instance";

            /// <summary>
            /// Contains: instance.
            /// </summary>
            internal static readonly string[] LabelsInstance = { Instance };

            /// <summary>
            /// Contains: instance, type.
            /// </summary>
            internal static readonly string[] LabelsInstanceType = { Instance, Type };
        }

        internal static class Instance
        {
            private static Gauge? _workers;
            public static Gauge Workers => _workers ??= Metrics.CreateGauge(
                "messaging_workers_total",
                "The total number of workers processing messages.",
                LabelNames.LabelsInstance
            );
        }

        private class IncomingMessageCounters : IMessageCounters
        {
            private Counter? _messagesInTotal;
            public Counter Total => _messagesInTotal ??= Metrics.CreateCounter(
                "messaging_incoming_type_total",
                "The total incoming messages per type.",
                LabelNames.LabelsInstanceType
            );

            private Gauge? _inflight;
            public Gauge InFlight => _inflight ??= Metrics.CreateGauge(
                "messaging_incoming_type_in_flight_total",
                "The total incoming messages per type currently being processed.",
                LabelNames.LabelsInstanceType
            );

            public Counter Aborted => throw new NotImplementedException();

            private Counter? _messagesInError;
            public Counter Errors => _messagesInError ??= Metrics.CreateCounter(
                "messaging_incoming_type_error_total",
                "The total of incoming messages per type which resulted in an error.",
                LabelNames.LabelsInstanceType
            );

            private Histogram? _messagesInDuration;
            public Histogram Duration => _messagesInDuration ??= Metrics.CreateHistogram(
                "messaging_incoming_type_duration_seconds",
                "The duration of incoming messages per type processed.",
                new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.001, 2, 16), LabelNames = LabelNames.LabelsInstanceType }
            );
        }

        private class OutgoingMessageCounters : IMessageCounters
        {
            private Counter? _total;
            public Counter Total => _total ??= Metrics.CreateCounter(
                "messaging_outgoing_type_total",
                "The total outgoing messages per type..",
                LabelNames.LabelsInstanceType
            );

            private Gauge? _inflight;
            public Gauge InFlight => _inflight ??= Metrics.CreateGauge(
                "messaging_outgoing_type_in_flight_total",
                "The total outgoing messages per type currently being sent.",
                LabelNames.LabelsInstanceType
            );

            public Counter Aborted => throw new NotImplementedException();

            private Counter? _messagesInError;
            public Counter Errors => _messagesInError ??= Metrics.CreateCounter(
                "messaging_outgoing_type_aborted_total",
                "The total of outgoing messages per type which resulted in an error.",
                LabelNames.LabelsInstanceType
            );

            private Histogram? _duration;
            public Histogram Duration => _duration ??= Metrics.CreateHistogram(
                "messaging_outgoing_type_duration_seconds",
                "The duration of outgoing messages per type sent.",
                new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.001, 2, 16), LabelNames = LabelNames.LabelsInstanceType }
            );
        }

        private class IncomingTransportCounters : IMessageCounters
        {
            private Counter? _messagesInTotal;
            public Counter Total => _messagesInTotal ??= Metrics.CreateCounter(
                "messaging_incoming_total",
                "The total incoming messages.",
                LabelNames.LabelsInstance
            );

            private Gauge? _inflight;
            public Gauge InFlight => _inflight ??= Metrics.CreateGauge(
                "messaging_incoming_in_flight_total",
                "The total incoming messages currently being processed.",
                LabelNames.LabelsInstance
            );

            private Counter? _messagesInAborted;
            public Counter Aborted => _messagesInAborted ??= Metrics.CreateCounter(
                "messaging_incoming_aborted_total",
                "The total of incoming messages for which the transaction was aborted.",
                LabelNames.LabelsInstance
            );
            public Counter Errors => throw new NotImplementedException();

            private Histogram? _messagesInDuration;
            public Histogram Duration => _messagesInDuration ??= Metrics.CreateHistogram(
                "messaging_incoming_duration_seconds",
                "The duration of incoming messages processed.",
                new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.001, 2, 16), LabelNames = LabelNames.LabelsInstance }
            );
        }

        private class OutgoingTransportCounters : IMessageCounters
        {
            private Counter? _total;
            public Counter Total => _total ??= Metrics.CreateCounter(
                "messaging_outgoing_total",
                "The total outgoing messages.",
                LabelNames.LabelsInstance
            );

            private Gauge? _inflight;
            public Gauge InFlight => _inflight ??= Metrics.CreateGauge(
                "messaging_outgoing_in_flight_total",
                "The total outgoing messages currently being sent.",
                LabelNames.LabelsInstance
            );

            private Counter? _aborted;
            public Counter Aborted => _aborted ??= Metrics.CreateCounter(
                "messaging_outgoing_aborted_total",
                "The total of outgoing messages for which the transaction was aborted.",
                LabelNames.LabelsInstance
            );
            public Counter Errors => throw new NotImplementedException();

            private Histogram? _duration;
            public Histogram Duration => _duration ??= Metrics.CreateHistogram(
                "messaging_outgoing_duration_seconds",
                "The duration of outgoing messages sent.",
                new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.001, 2, 16), LabelNames = LabelNames.LabelsInstance }
            );
        }

        internal static IMessageCounters IncomingTransport { get; } = new IncomingTransportCounters();
        internal static IMessageCounters OutgoingTransport { get; } = new OutgoingTransportCounters();
        internal static IMessageCounters IncomingMessages { get; } = new IncomingMessageCounters();
        internal static IMessageCounters OutgoingMessages { get; } = new OutgoingMessageCounters();
    }
}
