using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.Triggered.Internal
{
    public sealed class TriggerFactory
    {
        static readonly ConcurrentDictionary<Type, Type> _instanceFactoryTypeCache = new();
        readonly IServiceProvider _internalServiceProvider;

        public TriggerFactory(IServiceProvider internalServiceProvider)
        {
            _internalServiceProvider = internalServiceProvider;
        }

        public IEnumerable<object> Resolve(IServiceProvider serviceProvider, Type triggerType)
        {
            // triggers may be directly registered with our DI container
            if (serviceProvider is not null)
            {
                var triggers = serviceProvider.GetServices(triggerType);
                foreach (var trigger in triggers)
                {
                    if (trigger is not null)
                    {
                        yield return trigger;
                    }
                }
            }

            // Alternatively, triggers may be registered with the extension configuration
            var instanceFactoryType = _instanceFactoryTypeCache.GetOrAdd(triggerType,
                t => typeof(ITriggerInstanceFactory<>).MakeGenericType(t)
            );

            // Iterate once — eliminates the former .Any() + foreach double-enumeration
            foreach (var triggerServiceFactory in _internalServiceProvider.GetServices(instanceFactoryType))
            {
                if (triggerServiceFactory is ITriggerInstanceFactory factory)
                {
                    yield return factory.Create(serviceProvider ?? _internalServiceProvider);
                }
            }
        }
    }
}
