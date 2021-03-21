using System;
using System.Collections.Generic;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Prometheus.Steps;
using Rebus.Retry;
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
            configurer.EnablePrometheusMetrics(null!);
        }

        /// <summary>
        /// Configures Rebus to be instrumented with Prometheus.
        /// </summary>
        /// <param name="configurer">The options configurer.</param>
        /// <param name="options">Delegate to configure Rebus metrics.</param>
        /// <returns>The <see cref="OptionsConfigurer" /> instance to continue chaining.</returns>
        public static void EnablePrometheusMetrics(this OptionsConfigurer configurer, Action<RebusMetricsOptions> options)
        {
            configurer.Decorate<ITransport>(ctx => new TransportMetrics(ctx.Get<ITransport>()));

            configurer.Decorate(ctx =>
            {
                // We're not actually decorating bus, but using it
                // as a means to start metrics on instance/workers.

                IBus bus = ctx.Get<IBus>();
                Options busOptions = ctx.Get<Options>();
                DisposableTracker disposableTracker = ctx.Get<DisposableTracker>();

                string busName = busOptions.OptionalBusName ?? bus.ToString()!.Replace($"{nameof(RebusBus)} ", "");
                disposableTracker.Add(
                    new InstanceMetrics(
                        bus.Advanced,
                        ctx.Get<BusLifetimeEvents>(),
                        busName
                    )
                );

                return bus;
            });

            configurer.Register(_ => new DisposableTracker());

            configurer.Register(_ =>
            {
                var opts = new RebusMetricsOptions();
                options.Invoke(opts);
                return opts;
            });

            configurer.Decorate(ctx =>
            {
                RebusMetricsOptions opts = ctx.Get<RebusMetricsOptions>();
                if (!opts.MessageMetrics)
                {
                    return ctx.Get<IPipeline>();
                }

                return new PipelineStepConcatenator(ctx.Get<IPipeline>())
                    .OnReceive(new InstrumentIncomingStep(
                            Counters.IncomingMessages,
                            ctx.Get<IErrorTracker>()
                        ),
                        PipelineAbsolutePosition.Front)
                    .OnSend(new InstrumentOutgoingStep(
                            Counters.OutgoingMessages
                        ),
                        PipelineAbsolutePosition.Front
                    );
            });
        }

        private sealed class DisposableTracker : IDisposable
        {
            private readonly IList<IDisposable> _items = new List<IDisposable>();

            public void Add(IDisposable disposable)
            {
                _items.Add(disposable);
            }

            public void Dispose()
            {
                foreach (IDisposable disposable in _items)
                {
                    disposable?.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// </summary>
    public class RebusMetricsOptions
    {
        /// <summary>
        /// Gets or sets whether or not to emit message metrics. Defaults to <see langword="false" />.
        /// <para>
        /// Note: message metrics can be very verbose, depending on the number of different types of messages you are consuming/producing.
        /// </para>
        /// </summary>
        public bool MessageMetrics { get; set; }
    }
}
