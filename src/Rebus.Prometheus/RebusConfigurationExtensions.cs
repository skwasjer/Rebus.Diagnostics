using Rebus.Config;
using Rebus.Transport;

namespace Rebus.Prometheus
{
    /// <summary>
    /// Rebus extensions to configure/enable Prometheus metrics support.
    /// </summary>
    public static class RebusConfigurationExtensions
    {
        /// <summary>
        /// Configures Rebus to be instrumented with Prometheus.
        /// </summary>
        /// <param name="configurer">The options configurer.</param>
        /// <returns>The <see cref="OptionsConfigurer" /> instance to continue chaining.</returns>
        public static void EnablePrometheusMetrics(this OptionsConfigurer configurer)
        {
            configurer.Decorate<ITransport>(ctx => new TransportMetrics(ctx.Get<ITransport>()));
        }
    }
}
