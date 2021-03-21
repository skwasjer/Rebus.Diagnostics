using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Prometheus;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Pipeline;
using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;
using Xunit;

namespace Rebus.Prometheus
{
    public class MetricsIntegrationTests : IDisposable
    {
        private readonly BuiltinHandlerActivator _activator;
        private readonly ManualResetEvent _waitHandle;

        public MetricsIntegrationTests()
        {
            Metrics.SuppressDefaultMetrics();

            _activator = new BuiltinHandlerActivator();
            _waitHandle = new ManualResetEvent(false);
        }

        [Fact]
        public async Task When_a_batch_of_message_is_sent_it_should_produce_metrics()
        {
            int receiveErrors = 0;
            int cmdSuccess = 0;
            int eventSuccess = 0;
            const int total = 100;
            int receiveTotal = total;
            var rnd = new Random(DateTime.UtcNow.Ticks.GetHashCode());

            Task HandleMessage(IMessageContext ctx, object message)
            {
                bool isEvent = message is TestEvent;
                ctx.TransactionContext.OnCommitted(_ =>
                {
                    if (isEvent)
                    {
                        eventSuccess++;
                    }
                    else
                    {
                        cmdSuccess++;
                    }

                    return Task.CompletedTask;
                });
                ctx.TransactionContext.OnAborted(_ =>
                {
                    receiveErrors++;
                    receiveTotal++;
                });
                ctx.TransactionContext.OnDisposed(_ =>
                {
                    if (cmdSuccess + eventSuccess + receiveErrors == receiveTotal)
                    {
                        _waitHandle.Set();
                    }
                });

                if (message is TestCommand && rnd.Next(0, 10) == 0)
                {
                    throw new InvalidOperationException();
                }

                return Task.Delay(rnd.Next(0, 1000));
            }

            _activator.Handle<TestEvent>((_, ctx, msg) => HandleMessage(ctx, msg));
            _activator.Handle<TestCommand>((_, ctx, msg) => HandleMessage(ctx, msg));

            using IBus bus = ArrangeBus(_activator);
            await bus.Subscribe<TestEvent>();

            // Act
            var sendTasks = new Task[total];
            int cmdCount = 0;
            int eventCount = 0;
            for (int i = 0; i < total; i++)
            {
                if (rnd.Next(0, 2) == 0)
                {
                    sendTasks[i] = bus.Send(new TestCommand());
                    cmdCount++;
                }
                else
                {
                    sendTasks[i] = bus.Publish(new TestEvent());
                    eventCount++;
                }
            }

            await Task.WhenAll(sendTasks);
            await Task.Delay(5);

            // Assert
            string[] metrics = await ExportMetricsAsync();
            metrics.Should().Contain("messaging_workers_total{instance=\"Rebus 1\"} 1");

            // Wait for all messages to be processed.
            _waitHandle.WaitOne(Debugger.IsAttached ? -1 : 30000);

            metrics = await ExportMetricsAsync();
            metrics.Should()
                .Contain("messaging_workers_total{instance=\"Rebus 1\"} 1")
                .And.Contain($"messaging_outgoing_total {cmdCount + eventCount}")
                .And.Contain($"messaging_outgoing_duration_seconds_count {total}")
                .And.Contain("messaging_outgoing_in_flight_total 0")
                .And.ContainMatch("messaging_outgoing_duration_seconds_sum *")
                .And.Contain($"messaging_incoming_total {cmdSuccess + receiveErrors + eventSuccess}")
                .And.Contain($"messaging_incoming_duration_seconds_count {receiveTotal}")
                .And.Contain("messaging_incoming_in_flight_total 0")
                .And.Contain($"messaging_incoming_aborted_total {receiveErrors}")
                .And.ContainMatch("messaging_incoming_duration_seconds_sum *")
                ;

            bus.Dispose();
            metrics = await ExportMetricsAsync();
            metrics.Should().Contain("messaging_workers_total{instance=\"Rebus 1\"} 0");
        }

        public void Dispose()
        {
            _waitHandle?.Dispose();
            _activator?.Dispose();
        }

        private async Task<string[]> ExportMetricsAsync()
        {
            await using var ms = new MemoryStream();
            await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(ms);
            string s = Encoding.UTF8.GetString(ms.ToArray());
            return s.Split('\n');
        }

        private static IBus ArrangeBus(BuiltinHandlerActivator activator)
        {
            return Configure.With(activator)
                    .Options(o => o.EnablePrometheusMetrics())
                    .Routing(r => r.TypeBased().MapFallback("queue"))
                    .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "queue"))
                    .Subscriptions(s => s.StoreInMemory(new InMemorySubscriberStore()))
                    .Start()
                ;
        }
    }
}
