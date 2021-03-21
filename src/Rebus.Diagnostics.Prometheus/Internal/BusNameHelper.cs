using System;
using System.Collections.Concurrent;
using System.Reflection;
using Rebus.Bus;
using Rebus.Transport;

namespace Rebus.Diagnostics.Prometheus.Internal
{
    internal static class BusNameHelper
    {
        private const string DefaultBusPreamble = nameof(RebusBus) + " ";

        private static readonly ConcurrentDictionary<Type, PropertyInfo?> OwningBusPropertyCache = new();

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

            PropertyInfo? owningBusProperty = GetOwningBusProperty(transactionContext.GetType());
            if (owningBusProperty is null)
            {
                return null;
            }

            try
            {
                var busInstance = (IBus?)owningBusProperty.GetValue(transactionContext);
                return busInstance is null ? null : GetBusNameOrNull(busInstance);
            }
            catch (TargetException)
            {
                return null;
            }
        }

        private static PropertyInfo? GetOwningBusProperty(Type txType)
        {
            if (OwningBusPropertyCache.TryGetValue(txType, out PropertyInfo? owningBusProperty))
            {
                return owningBusProperty;
            }

            return OwningBusPropertyCache[txType] = txType.GetProperty(
                "OwningBus",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                typeof(IBus),
                Array.Empty<Type>(),
                null);
        }
    }
}
