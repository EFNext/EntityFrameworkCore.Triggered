using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggered.Internal.Descriptors;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.Triggered.Internal
{
    public sealed class TriggerDiscoveryService : ITriggerDiscoveryService, IResettableService
    {
        readonly static TriggerDescriptorComparer _triggerDescriptorComparer = new();
        readonly ITriggerServiceProviderAccessor _triggerServiceProviderAccessor;
        readonly ITriggerTypeRegistryService _triggerTypeRegistryService;
        readonly TriggerFactory _triggerFactory;

        IServiceProvider? _serviceProvider;

        public TriggerDiscoveryService(ITriggerServiceProviderAccessor triggerServiceProviderAccessor, ITriggerTypeRegistryService triggerTypeRegistryService, TriggerFactory triggerFactory)
        {
            _triggerServiceProviderAccessor = triggerServiceProviderAccessor;
            _triggerTypeRegistryService = triggerTypeRegistryService ?? throw new ArgumentNullException(nameof(triggerTypeRegistryService));
            _triggerFactory = triggerFactory;
        }

        public IEnumerable<TriggerDescriptor> DiscoverTriggers(Type openTriggerType, Type entityType, Func<Type, ITriggerTypeDescriptor> triggerTypeDescriptorFactory)
        {
            var registry = _triggerTypeRegistryService.ResolveRegistry(openTriggerType, entityType, triggerTypeDescriptorFactory);

            // On the first call for this (openTriggerType, entityType) combination the active
            // descriptor cache is null — use the full hierarchy.  On subsequent calls we skip
            // descriptors that are known to produce no results, eliminating empty DI lookups.
            var precomputedActive = registry.GetActiveDescriptors();
            var triggerTypeDescriptors = precomputedActive ?? registry.GetTriggerTypeDescriptors();

            if (triggerTypeDescriptors.Length == 0)
            {
                if (precomputedActive == null)
                {
                    registry.SetActiveDescriptors(Array.Empty<ITriggerTypeDescriptor>());
                }

                return Enumerable.Empty<TriggerDescriptor>();
            }

            List<TriggerDescriptor>? triggerDescriptors = null;
            // Only track active descriptors when the cache has not been populated yet
            List<ITriggerTypeDescriptor>? newActiveDescriptors = precomputedActive == null ? new List<ITriggerTypeDescriptor>() : null;

            foreach (var triggerTypeDescriptor in triggerTypeDescriptors)
            {
                var triggers = _triggerFactory.Resolve(ServiceProvider, triggerTypeDescriptor.TriggerType);
                var addedToActive = false;

                foreach (var trigger in triggers)
                {
                    if (trigger != null)
                    {
                        (triggerDescriptors ??= new List<TriggerDescriptor>()).Add(new TriggerDescriptor(triggerTypeDescriptor, trigger));

                        if (newActiveDescriptors != null && !addedToActive)
                        {
                            newActiveDescriptors.Add(triggerTypeDescriptor);
                            addedToActive = true;
                        }
                    }
                }
            }

            // Persist the active set so future calls skip the empty lookups
            if (newActiveDescriptors != null)
            {
                registry.SetActiveDescriptors(newActiveDescriptors.ToArray());
            }

            if (triggerDescriptors == null)
            {
                return Enumerable.Empty<TriggerDescriptor>();
            }
            else
            {
                triggerDescriptors.Sort(_triggerDescriptorComparer);
                return triggerDescriptors;
            }
        }

        public IEnumerable<AsyncTriggerDescriptor> DiscoverAsyncTriggers(Type openTriggerType, Type entityType, Func<Type, IAsyncTriggerTypeDescriptor> triggerTypeDescriptorFactory)
        {
            var registry = _triggerTypeRegistryService.ResolveRegistry(openTriggerType, entityType, triggerTypeDescriptorFactory);

            var precomputedActive = registry.GetActiveDescriptors();
            var triggerTypeDescriptors = precomputedActive ?? registry.GetTriggerTypeDescriptors();

            if (triggerTypeDescriptors.Length == 0)
            {
                if (precomputedActive == null)
                {
                    registry.SetActiveDescriptors(Array.Empty<IAsyncTriggerTypeDescriptor>());
                }

                return Enumerable.Empty<AsyncTriggerDescriptor>();
            }

            List<AsyncTriggerDescriptor>? triggerDescriptors = null;
            List<IAsyncTriggerTypeDescriptor>? newActiveDescriptors = precomputedActive == null ? new List<IAsyncTriggerTypeDescriptor>() : null;

            foreach (var triggerTypeDescriptor in triggerTypeDescriptors)
            {
                var triggers = _triggerFactory.Resolve(ServiceProvider, triggerTypeDescriptor.TriggerType);
                var addedToActive = false;

                foreach (var trigger in triggers)
                {
                    if (trigger != null)
                    {
                        (triggerDescriptors ??= new List<AsyncTriggerDescriptor>()).Add(new AsyncTriggerDescriptor(triggerTypeDescriptor, trigger));

                        if (newActiveDescriptors != null && !addedToActive)
                        {
                            newActiveDescriptors.Add(triggerTypeDescriptor);
                            addedToActive = true;
                        }
                    }
                }
            }

            if (newActiveDescriptors != null)
            {
                registry.SetActiveDescriptors(newActiveDescriptors.ToArray());
            }

            if (triggerDescriptors == null)
            {
                return Enumerable.Empty<AsyncTriggerDescriptor>();
            }
            else
            {
                triggerDescriptors.Sort(_triggerDescriptorComparer);
                return triggerDescriptors;
            }
        }

        public IEnumerable<TTrigger> DiscoverTriggers<TTrigger>()
        {
            // We can skip the registry as there is no generic argument
            var resolvedTriggers = _triggerFactory.Resolve(ServiceProvider, typeof(TTrigger));

            // Materialise eagerly so we can short-circuit for the common case of 0 registered
            // lifecycle triggers, avoiding the LINQ chain allocations on every SaveChanges call.
            List<(object trigger, int defaultPriority, int customPriority)>? sorted = null;
            var index = 0;

            foreach (var trigger in resolvedTriggers)
            {
                (sorted ??= new List<(object, int, int)>()).Add(
                    (trigger, index++, (trigger as ITriggerPriority)?.Priority ?? 0));
            }

            if (sorted == null)
            {
                return Enumerable.Empty<TTrigger>();
            }

            return sorted
                .OrderBy(x => x.customPriority)
                .ThenBy(x => x.defaultPriority)
                .Select(x => (TTrigger)x.trigger);
        }

        public IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                {
                    _serviceProvider = _triggerServiceProviderAccessor.GetTriggerServiceProvider();
                }

                return _serviceProvider;
            }
            set => _serviceProvider = value;
        }

        public void ResetState()
        {
            _serviceProvider = null;
        }

        public Task ResetStateAsync(CancellationToken cancellationToken = default)
        {
            ResetState();
            return Task.CompletedTask;
        }
    }
}
