using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EntityFrameworkCore.Triggered;
using EntityFrameworkCore.Triggered.Lifecycles;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        // Open generic trigger interfaces — add new generic lifecycle interfaces here
        private readonly static HashSet<Type> _genericTriggerTypes = new HashSet<Type>
        {
            typeof(IBeforeSaveTrigger<>),
            typeof(IBeforeSaveAsyncTrigger<>),
            typeof(IAfterSaveTrigger<>),
            typeof(IAfterSaveAsyncTrigger<>),
            typeof(IAfterSaveFailedTrigger<>),
            typeof(IAfterSaveFailedAsyncTrigger<>),
        };

        // Non-generic lifecycle interfaces — add new non-generic lifecycle interfaces here
        private readonly static HashSet<Type> _nonGenericTriggerTypes = new HashSet<Type>
        {
            typeof(IBeforeSaveStartingTrigger),
            typeof(IBeforeSaveStartingAsyncTrigger),
            typeof(IBeforeSaveCompletedTrigger),
            typeof(IBeforeSaveCompletedAsyncTrigger),
            typeof(IAfterSaveFailedStartingTrigger),
            typeof(IAfterSaveFailedStartingAsyncTrigger),
            typeof(IAfterSaveFailedCompletedTrigger),
            typeof(IAfterSaveFailedCompletedAsyncTrigger),
            typeof(IAfterSaveStartingTrigger),
            typeof(IAfterSaveStartingAsyncTrigger),
            typeof(IAfterSaveCompletedTrigger),
            typeof(IAfterSaveCompletedAsyncTrigger),
        };

        // Caches the resolved trigger interfaces per implementation type to avoid repeated reflection
        private readonly static ConcurrentDictionary<Type, Type[]> _triggerInterfaceCache =
            new ConcurrentDictionary<Type, Type[]>();

        /// <summary>
        /// Returns the types defined in <paramref name="assembly"/>, gracefully handling
        /// <see cref="ReflectionTypeLoadException"/> that occurs when some types cannot be
        /// loaded due to missing dependencies (e.g. optional assemblies not present at runtime).
        /// </summary>
        private static IEnumerable<Type> GetAssemblyTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                // Return only the types that could be loaded; nulls represent types that failed
                return e.Types.OfType<Type>();
            }
        }

        /// <summary>
        /// Returns the subset of <paramref name="triggerImplementationType"/>'s interfaces
        /// that match a known trigger interface, using a per-type cache.
        /// </summary>
        private static Type[] GetTriggerInterfaces(Type triggerImplementationType)
            => _triggerInterfaceCache.GetOrAdd(triggerImplementationType, t =>
            {
                var interfaces = t.GetInterfaces();
                var result = new List<Type>(interfaces.Length);

                foreach (var iface in interfaces)
                {
                    if (iface.IsConstructedGenericType)
                    {
                        if (_genericTriggerTypes.Contains(iface.GetGenericTypeDefinition()))
                        {
                            result.Add(iface);
                        }
                    }
                    else if (_nonGenericTriggerTypes.Contains(iface))
                    {
                        result.Add(iface);
                    }
                }

                return result.ToArray();
            });

        private static void RegisterTriggerTypes(Type triggerImplementationType, IServiceCollection services)
        {
            var triggerInterfaces = GetTriggerInterfaces(triggerImplementationType);

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

            var assemblyTypes = assemblies
                .SelectMany(GetAssemblyTypes)
                .Where(x => x is { IsClass: true, IsAbstract: false });

            foreach (var assemblyType in assemblyTypes)
            {
                var triggerInterfaces = GetTriggerInterfaces(assemblyType);

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
