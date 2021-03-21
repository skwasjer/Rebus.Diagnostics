using Prometheus;

namespace Rebus.Prometheus
{
    internal interface IMessageCounters
    {
        Counter Total { get; }
        Gauge InFlight { get; }
        Counter Aborted { get; }
        Histogram Duration { get; }
    }
}
