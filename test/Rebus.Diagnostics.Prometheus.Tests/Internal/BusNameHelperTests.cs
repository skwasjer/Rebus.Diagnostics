using FluentAssertions;
using Moq;
using Rebus.Bus;
using Rebus.TestHelpers;
using Rebus.Transport;
using Xunit;

namespace Rebus.Diagnostics.Prometheus.Internal
{
    public class BusNameHelperTests
    {
        public class BusName
        {
            [Fact]
            public void Given_that_bus_is_null_when_getting_name_it_should_return_unknown()
            {
                IBus bus = null;

                // Act
                // ReSharper disable once AssignNullToNotNullAttribute
                string actual = BusNameHelper.GetBusName(bus);

                // Assert
                actual.Should().Be("<unknown>");
            }

            [Theory]
            [InlineData(null, "<unknown>")]
            [InlineData("RebusBus Rebus 1", "Rebus 1")]
            [InlineData("RebusBus Rebus 14", "Rebus 14")]
            [InlineData("MyCustomName", "MyCustomName")]
            public void Given_rebusBus_when_getting_name_it_should_return_value(string toStringName, string expectedName)
            {
                using IBus bus = ArrangeBus(toStringName);

                // Act
                string actual = BusNameHelper.GetBusName(bus);

                // Assert
                actual.Should().Be(expectedName);
            }

            [Fact]
            public void Given_that_bus_is_null_when_getting_name_or_null_it_should_return_null()
            {
                IBus bus = null;

                // Act
                // ReSharper disable once AssignNullToNotNullAttribute
                string actual = BusNameHelper.GetBusNameOrNull(bus);

                // Assert
                actual.Should().BeNull();
            }

            [Theory]
            [InlineData(null, null)]
            [InlineData("RebusBus Rebus 1", "Rebus 1")]
            [InlineData("RebusBus Rebus 14", "Rebus 14")]
            [InlineData("MyCustomName", "MyCustomName")]
            public void Given_rebusBus_when_getting_name_or_null_it_should_return_value(string toStringName, string expectedName)
            {
                using IBus bus = ArrangeBus(toStringName);

                // Act
                string actual = BusNameHelper.GetBusNameOrNull(bus);

                // Assert
                actual.Should().Be(expectedName);
            }
        }

        public class BusNameViaTransactionContext
        {
            [Fact]
            public void Given_that_tx_is_null_when_getting_name_it_should_return_unknown()
            {
                ITransactionContext tx = null;

                // Act
                // ReSharper disable once AssignNullToNotNullAttribute
                string actual = BusNameHelper.GetBusName(tx);

                // Assert
                actual.Should().Be("<unknown>");
            }

            [Fact]
            public void Given_that_tx_has_no_owning_bus_when_getting_name_it_should_return_unknown()
            {
                ITransactionContext tx = Mock.Of<ITransactionContext>();

                // Act
                string actual = BusNameHelper.GetBusName(tx);

                // Assert
                actual.Should().Be("<unknown>");
            }

            [Theory]
            [InlineData(null, "<unknown>")]
            [InlineData("RebusBus Rebus 1", "Rebus 1")]
            [InlineData("RebusBus Rebus 14", "Rebus 14")]
            [InlineData("MyCustomName", "MyCustomName")]
            public void Given_tx_with_owning_bus_when_getting_name_it_should_return_value(string toStringName, string expectedName)
            {
                ITransactionContext tx = ArrangeTransactionContextWithOwningBus(toStringName);

                // Act
                string actual = BusNameHelper.GetBusName(tx);

                // Assert
                actual.Should().Be(expectedName);
            }

            [Fact]
            public void Given_that_tx_is_null_when_getting_name_or_null_it_should_return_null()
            {
                ITransactionContext tx = null;

                // Act
                // ReSharper disable once AssignNullToNotNullAttribute
                string actual = BusNameHelper.GetBusNameOrNull(tx);

                // Assert
                actual.Should().BeNull();
            }

            [Fact]
            public void Given_that_tx_has_no_owning_bus_when_getting_name_or_null_it_should_return_null()
            {
                ITransactionContext tx = Mock.Of<ITransactionContext>();

                // Act
                string actual = BusNameHelper.GetBusNameOrNull(tx);

                // Assert
                actual.Should().BeNull();
            }

            [Theory]
            [InlineData(null, null)]
            [InlineData("RebusBus Rebus 1", "Rebus 1")]
            [InlineData("RebusBus Rebus 14", "Rebus 14")]
            [InlineData("MyCustomName", "MyCustomName")]
            public void Given_tx_with_owning_bus_when_getting_name_or_null_it_should_return_value(string toStringName, string expectedName)
            {
                ITransactionContext tx = ArrangeTransactionContextWithOwningBus(toStringName);

                // Act
                string actual = BusNameHelper.GetBusNameOrNull(tx);

                // Assert
                actual.Should().Be(expectedName);
            }
        }

        private static IBus ArrangeBus(string busName)
        {
            var busMock = new Mock<IBus>();
            busMock.Setup(m => m.ToString()).Returns(busName);
            return busMock.Object;
        }

        private static ITransactionContext ArrangeTransactionContextWithOwningBus(string busName)
        {
            var txMock = new Mock<IFakeTransactionContextWithBus>();
            txMock.Setup(m => m.OwningBus).Returns(ArrangeBus(busName));
            return txMock.Object;
        }

        // ReSharper disable once MemberCanBePrivate.Global - Needed for mock.
        internal interface IFakeTransactionContextWithBus : ITransactionContext
        {
            IBus OwningBus {get;}
        }
    }
}
