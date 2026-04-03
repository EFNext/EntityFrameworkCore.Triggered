using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EntityFrameworkCore.Triggered.Infrastructure.Internal;

namespace EntityFrameworkCore.Triggered.Internal
{
    public sealed class TriggerTypeRegistry<TTriggerTypeDescriptor>
    {
        readonly Type _entityType;
        readonly Func<Type, TTriggerTypeDescriptor> _triggerTypeDescriptorFactory;

        TTriggerTypeDescriptor[]? _resolvedDescriptors;

        // Populated after the first resolution: only the descriptors that produced at least one
        // trigger instance. null = not yet computed; empty array = computed, none were active.
        // Written once (first-write-wins via CompareExchange) so no further synchronisation needed.
        TTriggerTypeDescriptor[]? _activeDescriptors;

        public TriggerTypeRegistry(Type entityType, Func<Type, TTriggerTypeDescriptor> triggerTypeDescriptorFactory)
        {
            _entityType = entityType;
            _triggerTypeDescriptorFactory = triggerTypeDescriptorFactory;
        }

        IEnumerable<Type> GetEntityTypeHierarchy()
        {
            // Enumerable of the type hierarchy from base to concrete
            var typeHierarchy = TypeHelpers.EnumerateTypeHierarchy(_entityType).Reverse();
            foreach (var type in typeHierarchy)
            {
                foreach (var interfaceType in type.GetInterfaces())
                {
                    yield return interfaceType;
                }

                yield return type;
            }
        }

        public TTriggerTypeDescriptor[] GetTriggerTypeDescriptors()
        {
            if (_resolvedDescriptors == null)
            {
                var result = new List<TTriggerTypeDescriptor>();

                foreach (var triggerType in GetEntityTypeHierarchy().Distinct())
                {
                    var descriptor = _triggerTypeDescriptorFactory(triggerType);
                    result.Add(descriptor);
                }

                _resolvedDescriptors = result.ToArray();
            }

            return _resolvedDescriptors;
        }

        /// <summary>
        /// Returns the subset of descriptors that produced at least one trigger instance during a
        /// previous resolution, or <c>null</c> if this information has not yet been computed.
        /// An empty array means a previous resolution confirmed that no triggers are registered.
        /// </summary>
        public TTriggerTypeDescriptor[]? GetActiveDescriptors()
            => Volatile.Read(ref _activeDescriptors);

        /// <summary>
        /// Records the subset of descriptors that produced triggers during a resolution.
        /// Only the first call has any effect (first-write-wins); subsequent calls are ignored.
        /// </summary>
        public void SetActiveDescriptors(TTriggerTypeDescriptor[] activeDescriptors)
            => Interlocked.CompareExchange(ref _activeDescriptors, activeDescriptors, null);
    }
}
