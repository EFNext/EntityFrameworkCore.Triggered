using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EntityFrameworkCore.Triggered.Lifecycles;

namespace EntityFrameworkCore.Triggered.Extensions
{
    /// <summary>
    /// Internal helpers shared between <c>ServiceCollectionExtensions</c> and
    /// <c>TriggersContextOptionsBuilderExtensions</c> for trigger-type discovery.
    /// </summary>
    static internal class TriggerTypeHelper
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
        /// Returns the non-abstract classes defined in <paramref name="assembly"/>, gracefully handling
        /// <see cref="ReflectionTypeLoadException"/> that occurs when some types cannot be loaded due to missing
        /// dependencies (e.g. optional assemblies not present at runtime).
        /// </summary>
        static internal IEnumerable<Type> GetAssemblyConcreteClasses(Assembly assembly) => 
            GetAssemblyTypes(assembly).Where(t => t is { IsClass: true, IsAbstract: false });

        /// <summary>
        /// Returns the subset of <paramref name="triggerImplementationType"/>'s interfaces that
        /// match a known trigger interface, using a per-type cache.
        /// </summary>
        static internal Type[] GetTriggerInterfaces(Type triggerImplementationType)
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
    }
}



