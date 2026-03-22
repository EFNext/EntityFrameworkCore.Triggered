using System;
using System.Linq;
using System.Reflection;
using EntityFrameworkCore.Triggered.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        private static void RegisterTriggerTypes(Type triggerImplementationType, IServiceCollection services)
        {
            var triggerInterfaces = TriggerTypeHelper.GetTriggerInterfaces(triggerImplementationType);

            foreach (var triggerInterface in triggerInterfaces)
            {
                services.Add(new ServiceDescriptor(triggerInterface, sp => sp.GetRequiredService(triggerImplementationType), ServiceLifetime.Transient));
            }
        }

        public static IServiceCollection AddTrigger<TTrigger>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TTrigger : class
        {
            services.TryAdd(new ServiceDescriptor(typeof(TTrigger), typeof(TTrigger), lifetime));

            RegisterTriggerTypes(typeof(TTrigger), services);

            return services;
        }

        public static IServiceCollection AddTrigger(this IServiceCollection services, object triggerInstance)
        {
            if (triggerInstance is null)
            {
                throw new ArgumentNullException(nameof(triggerInstance));
            }

            services.TryAddSingleton(triggerInstance);

            RegisterTriggerTypes(triggerInstance.GetType(), services);

            return services;
        }

        public static IServiceCollection AddAssemblyTriggers(this IServiceCollection services)
            => AddAssemblyTriggers(services, Assembly.GetCallingAssembly());

        public static IServiceCollection AddAssemblyTriggers(this IServiceCollection services, ServiceLifetime lifetime)
            => AddAssemblyTriggers(services, lifetime, Assembly.GetCallingAssembly());

        public static IServiceCollection AddAssemblyTriggers(this IServiceCollection services, params Assembly[] assemblies)
            => AddAssemblyTriggers(services, ServiceLifetime.Scoped, assemblies);

        public static IServiceCollection AddAssemblyTriggers(this IServiceCollection services, ServiceLifetime lifetime, params Assembly[] assemblies)
        {
            if (assemblies is null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            if (assemblies.Length == 0)
            {
                return services;
            }

            var assemblyTypes = assemblies.SelectMany(TriggerTypeHelper.GetAssemblyConcreteClasses);

            foreach (var assemblyType in assemblyTypes)
            {
                var triggerInterfaces = TriggerTypeHelper.GetTriggerInterfaces(assemblyType);

                if (triggerInterfaces.Length == 0)
                {
                    continue;
                }

                services.TryAdd(new ServiceDescriptor(assemblyType, assemblyType, lifetime));

                foreach (var triggerInterface in triggerInterfaces)
                {
                    services.Add(new ServiceDescriptor(triggerInterface, sp => sp.GetRequiredService(assemblyType), ServiceLifetime.Transient));
                }
            }

            return services;
        }
    }
}
