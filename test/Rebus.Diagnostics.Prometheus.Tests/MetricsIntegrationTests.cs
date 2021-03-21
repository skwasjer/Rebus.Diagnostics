using System;
using System.Collections.Generic;
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
using Rebus.Diagnostics.Prometheus.Internal;
using Rebus.Diagnostics.Prometheus.Messages;
using Rebus.Extensions;
using Rebus.Persistence.InMem;
using Rebus.Pipeline;
using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;
using Xunit;

namespace Rebus.Diagnostics.Prometheus
{
    [Collection(nameof(DisableParallelization))]
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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task When_a_batch_of_message_is_sent_it_should_produce_metrics(bool messageMetrics)
        {
            int receiveErrors = 0;
            int cmdSuccess = 0;
            int eventSuccess = 0;
            const int total = 100;
            int receiveTotal = total;
            var rnd = new Random(DateTime.UtcNow.Ticks.GetHashCode());
            var failedMessages = new HashSet<int>();

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

                if (message is TestCommand cmd
                 && !failedMessages.Contains(cmd.Index)
                 && (cmd.Index == 30 || cmd.Index == 60))
                {
                    failedMessages.Add(cmd.Index);
                    throw new InvalidOperationException();
                }

                return Task.Delay(rnd.Next(0, 500));
            }

            _activator.Handle<TestEvent>((_, ctx, msg) => HandleMessage(ctx, msg));
            _activator.Handle<TestCommand>((_, ctx, msg) => HandleMessage(ctx, msg));

            using IBus bus = ArrangeBus(_activator, messageMetrics);
            await bus.Subscribe<TestEvent>();

            // Act
            await Task.WhenAll(SendMessages(total, bus, out int cmdCount, out int eventCount));
            await Task.Delay(5);

            // Assert
            string[] metrics = await ExportMetricsAsync();
            metrics.Should().ContainMatch("messaging_workers_total{instance=\"Rebus *\"} 1");

            // Wait for all messages to be processed.
            _waitHandle.WaitOne(Debugger.IsAttached ? -1 : 30000);
            await Task.Delay(500);

            string busName = BusNameHelper.GetBusName(bus);
            string dimensionInstance = $"{{instance=\"{busName}\"}}";
            string dimensionInstanceEventType = $"{{instance=\"{busName}\",type=\"{typeof(TestEvent).GetSimpleAssemblyQualifiedName()}\"}}";
            string dimensionInstanceCommandType = $"{{instance=\"{busName}\",type=\"{typeof(TestCommand).GetSimpleAssemblyQualifiedName()}\"}}";

            metrics = await ExportMetricsAsync();
            metrics.Should()
                .ContainMatch($"messaging_workers_total{dimensionInstance} 1")
                .And.ContainMatch($"messaging_outgoing_total{dimensionInstance} {cmdCount + eventCount}")
                .And.ContainMatch($"messaging_outgoing_duration_seconds_count{dimensionInstance} {total}")
                .And.ContainMatch($"messaging_outgoing_in_flight_total{dimensionInstance} 0")
                .And.ContainMatch($"messaging_outgoing_duration_seconds_sum{dimensionInstance} *")
                .And.ContainMatch($"messaging_incoming_total{dimensionInstance} {cmdSuccess + receiveErrors + eventSuccess}")
                .And.ContainMatch($"messaging_incoming_duration_seconds_count{dimensionInstance} {receiveTotal}")
                .And.ContainMatch($"messaging_incoming_in_flight_total{dimensionInstance} 0")
                .And.ContainMatch($"messaging_incoming_aborted_total{dimensionInstance} {receiveErrors}")
                .And.ContainMatch($"messaging_incoming_duration_seconds_sum{dimensionInstance} *")
                ;

            if (messageMetrics)
            {
                metrics.Should()
                    .Contain($"messaging_outgoing_type_total{dimensionInstanceEventType} 66")
                    .And.Contain($"messaging_outgoing_type_total{dimensionInstanceCommandType} 34")
                    .And.Contain($"messaging_incoming_type_total{dimensionInstanceEventType} {eventCount}")
                    .And.Contain($"messaging_incoming_type_total{dimensionInstanceCommandType} {cmdCount + receiveErrors}")
                    .And.Contain($"messaging_incoming_type_error_total{dimensionInstanceCommandType} {receiveErrors}")
                    ;
            }
            else
            {
                metrics.Should()
                    .NotContain($"messaging_outgoing_type_total{dimensionInstanceEventType} 66")
                    .And.NotContain($"messaging_outgoing_type_total{dimensionInstanceCommandType} 34")
                    .And.NotContain($"messaging_incoming_type_total{dimensionInstanceEventType} {eventCount}")
                    .And.NotContain($"messaging_incoming_type_total{dimensionInstanceCommandType} {cmdCount + receiveErrors}")
                    .And.NotContain($"messaging_incoming_type_error_total{dimensionInstanceCommandType} {receiveErrors}")
                    ;
            }

            bus.Dispose();
            metrics = await ExportMetricsAsync();
            metrics.Should().ContainMatch("messaging_workers_total{instance=\"Rebus *\"} 0");
        }

        private static Task[] SendMessages(int total, IBus bus, out int cmdCount, out int eventCount)
        {
            var sendTasks = new Task[total];
            cmdCount = 0;
            eventCount = 0;
            for (int i = 0; i < total; i++)
            {
                // 1 in 3 is a command.
                if (i % 3 == 0)
                {
                    sendTasks[i] = bus.Send(new TestCommand
                    {
                        Index = i
                    });
                    cmdCount++;
                }
                else
                {
                    sendTasks[i] = bus.Publish(new TestEvent());
                    eventCount++;
                }
            }

            return sendTasks;
        }

        public void Dispose()
        {
            _waitHandle?.Dispose();
            _activator?.Dispose();
        }

        private static async Task<string[]> ExportMetricsAsync()
        {
            // ReSharper disable once UseAwaitUsing
            using var ms = new MemoryStream();
            await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(ms);
            string s = Encoding.UTF8.GetString(ms.ToArray());
            return s.Split('\n');
        }

        private static IBus ArrangeBus(IHandlerActivator activator, bool messageMetrics)
        {
            return Configure.With(activator)
                    .Options(o => o.EnablePrometheusMetrics(options => options.MessageMetrics = messageMetrics))
                    .Routing(r => r.TypeBased().MapFallback("queue"))
                    .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "queue"))
                    .Subscriptions(s => s.StoreInMemory(new InMemorySubscriberStore()))
                    .Start()
                ;
        }
    }
}
