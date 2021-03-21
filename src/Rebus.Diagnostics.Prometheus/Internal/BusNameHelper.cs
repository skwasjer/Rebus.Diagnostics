using System;
using System.Reflection;
using Rebus.Bus;
using Rebus.Transport;

namespace Rebus.Diagnostics.Prometheus.Internal
{
    internal static class BusNameHelper
    {
        private const string DefaultBusPreamble = nameof(RebusBus) + " ";

        private static PropertyInfo? _owningBusProperty;

        public static string GetBusName(IBus bus)
        {
            return GetBusNameOrNull(bus) ?? "<unknown>";
        }

        public static string? GetBusNameOrNull(IBus bus)
        {
            // ReSharper disable once ConstantConditionalAccessQualifier
            if (bus?.ToString() is not { } busName)
            {
                return null;
            }

            if (busName.StartsWith(DefaultBusPreamble, StringComparison.InvariantCulture)
             && busName.Length > DefaultBusPreamble.Length)
            {
                return busName.Substring(DefaultBusPreamble.Length);
            }

            return busName;
        }

        public static string GetBusName(ITransactionContext transactionContext)
        {
            return GetBusNameOrNull(transactionContext) ?? "<unknown>";
        }

        public static string? GetBusNameOrNull(ITransactionContext transactionContext)
        {
            if (transactionContext is null!)
            {
                return null;
            }

            Type txType = transactionContext.GetType();
            _owningBusProperty ??= txType.GetProperty(
                "OwningBus",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                typeof(IBus),
                Array.Empty<Type>(),
                null);

            if (_owningBusProperty is null)
            {
                return null;
            }

            try
            {
                var busInstance = (IBus?)_owningBusProperty.GetValue(transactionContext);
                return busInstance is null ? null : GetBusNameOrNull(busInstance);
            }
            catch (TargetException)
            {
                return null;
            }
        }
    }
}
