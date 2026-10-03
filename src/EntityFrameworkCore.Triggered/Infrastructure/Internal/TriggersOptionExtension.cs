using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityFrameworkCore.Triggered.Internal;
using EntityFrameworkCore.Triggered.Internal.CascadeStrategies;
using EntityFrameworkCore.Triggered.Lifecycles;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EntityFrameworkCore.Triggered.Infrastructure.Internal
{
    public class TriggersOptionExtension : IDbContextOptionsExtension
    {
        sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            private string? _logFragment;
            public ExtensionInfo(IDbContextOptionsExtension extension) : base(extension)
            {
            }

            public override bool IsDatabaseProvider => false;
            public override string LogFragment
            {
                get
                {
                    if (_logFragment == null)
                    {
                        _logFragment = string.Empty;
                    }

                    return _logFragment;
                }
            }

            public new TriggersOptionExtension Extension => (TriggersOptionExtension)base.Extension;

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                if (debugInfo == null)
                {
                    throw new ArgumentNullException(nameof(debugInfo));
                }

                debugInfo["Triggers:TriggersCount"] = (Extension._triggers?.Count() ?? 0).ToString();
                debugInfo["Triggers:TriggerTypesCount"] = (Extension._triggerTypes?.Count() ?? 0).ToString();
                debugInfo["Triggers:MaxCascadeCycles"] = Extension._maxCascadeCycles.ToString();
                debugInfo["Triggers:CascadeBehavior"] = Extension._cascadeBehavior.ToString();
            }

            public override int GetServiceProviderHashCode()
            {
                var hashCode = new HashCode();

                if (Extension._triggers != null)
                {
                    foreach (var trigger in Extension._triggers)
                    {
                        hashCode.Add(trigger);
                    }
                }

                if (Extension._triggerTypes != null)
                {
                    foreach (var triggerType in Extension._triggerTypes)
                    {
                        hashCode.Add(triggerType);
                    }
                }

                hashCode.Add(Extension._maxCascadeCycles);
                hashCode.Add(Extension._cascadeBehavior);

                if (Extension._serviceProviderTransform != null)
                {
                    hashCode.Add(Extension._serviceProviderTransform);
                }

                return hashCode.ToHashCode();
            }

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
                => other is ExtensionInfo otherInfo
                    && Enumerable.SequenceEqual(Extension._triggers ?? Enumerable.Empty<ValueTuple<object, ServiceLifetime>>(), otherInfo.Extension._triggers ?? Enumerable.Empty<ValueTuple<object, ServiceLifetime>>())
                    && Enumerable.SequenceEqual(Extension._triggerTypes ?? Enumerable.Empty<Type>(), otherInfo.Extension._triggerTypes ?? Enumerable.Empty<Type>())
                    && Extension._maxCascadeCycles == otherInfo.Extension._maxCascadeCycles
                    && Extension._cascadeBehavior == otherInfo.Extension._cascadeBehavior
                    && Extension._serviceProviderTransform == otherInfo.Extension._serviceProviderTransform;
        }

        private ExtensionInfo? _info;
        private IEnumerable<(object typeOrInstance, ServiceLifetime lifetime)>? _triggers;
        private IEnumerable<Type> _triggerTypes;
        private int _maxCascadeCycles = 100;
        private CascadeBehavior _cascadeBehavior = CascadeBehavior.EntityAndType;
        private Func<IServiceProvider, IServiceProvider>? _serviceProviderTransform;

        public TriggersOptionExtension()
        {
            _triggerTypes = new[] {
                typeof(IBeforeSaveTrigger<>),
                typeof(IBeforeSaveAsyncTrigger<>),
                typeof(IAfterSaveTrigger<>),
                typeof(IAfterSaveAsyncTrigger<>),
                typeof(IAfterSaveFailedTrigger<>),
                typeof(IAfterSaveFailedAsyncTrigger<>),
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
                typeof(IAfterSaveCompletedAsyncTrigger)
            };
        }

        public TriggersOptionExtension(TriggersOptionExtension copyFrom)
        {
            if (copyFrom._triggers != null)
            {
                _triggers = copyFrom._triggers;
            }

            _triggerTypes = copyFrom._triggerTypes;
            _maxCascadeCycles = copyFrom._maxCascadeCycles;
            _cascadeBehavior = copyFrom._cascadeBehavior;
            _serviceProviderTransform = copyFrom._serviceProviderTransform;
        }

        public DbContextOptionsExtensionInfo Info
            => _info ??= new ExtensionInfo(this);

        public int MaxCascadeCycles => _maxCascadeCycles;
        public CascadeBehavior CascadeBehavior => _cascadeBehavior;
        public IEnumerable<(object typeOrInstance, ServiceLifetime lifetime)> Triggers => _triggers ?? Enumerable.Empty<(object typeOrInstance, ServiceLifetime lifetime)>();

        public void ApplyServices(IServiceCollection services)
        {
            services.AddScoped(serviceProvider => new ApplicationTriggerServiceProviderAccessor(serviceProvider, _serviceProviderTransform));
            services.AddScoped<IResettableService>(serviceProvider => serviceProvider.GetRequiredService<ApplicationTriggerServiceProviderAccessor>());
            services.TryAddScoped<ITriggerServiceProviderAccessor>(serviceProvider => serviceProvider.GetRequiredService<ApplicationTriggerServiceProviderAccessor>());

            services.TryAddSingleton<ITriggerTypeRegistryService, TriggerTypeRegistryService>();

            services.AddScoped<TriggerDiscoveryService>();
            services.AddScoped<IResettableService>(serviceProvider => serviceProvider.GetRequiredService<TriggerDiscoveryService>());
            services.TryAddScoped<ITriggerDiscoveryService>(serviceProvider => serviceProvider.GetRequiredService<TriggerDiscoveryService>());

            services.AddScoped<TriggerService>();
            services.AddScoped<IResettableService>(serviceProvider => serviceProvider.GetRequiredService<TriggerService>());
            services.TryAddScoped<ITriggerService>(serviceProvider => serviceProvider.GetRequiredService<TriggerService>());

            services.AddScoped<TriggerFactory>();

            services.AddScoped<TriggerSessionSaveChangesInterceptor>();
            services.AddScoped<IResettableService>(serviceProvider => serviceProvider.GetRequiredService<TriggerSessionSaveChangesInterceptor>());
            services.TryAddScoped<IInterceptor>(serviceProvider => serviceProvider.GetRequiredService<TriggerSessionSaveChangesInterceptor>());

            services.Configure<TriggerOptions>(triggerServiceOptions => {
                triggerServiceOptions.MaxCascadeCycles = _maxCascadeCycles;
            });

            var cascadeStrategyType = _cascadeBehavior switch {
                CascadeBehavior.None => typeof(NoCascadeStrategy),
                CascadeBehavior.EntityAndType => typeof(EntityAndTypeCascadeStrategy),
                _ => throw new InvalidOperationException("Unsupported cascading mode")
            };

            services.TryAddTransient(typeof(ICascadeStrategy), cascadeStrategyType);

            if (_triggers != null && _triggerTypes != null)
            {
                foreach (var (typeOrInstance, lifetime) in _triggers)
                {
                    var (triggerServiceType, triggerServiceInstance) = typeOrInstance switch {
                        Type type => (type, null),
                        object instance => (instance.GetType(), instance),
                        _ => throw new InvalidOperationException("Unknown type registration")
                    };

                    var instanceParamExpression = Expression.Parameter(typeof(object), "object");

                    Func<object?, object>? triggerInstanceFactoryBuilder = null;

                    foreach (var triggerType in _triggerTypes.Distinct())
                    {
                        var triggerTypeImplementations = triggerType.IsGenericTypeDefinition
                            ? TypeHelpers.FindGenericInterfaces(triggerServiceType, triggerType)
                            : triggerServiceType.GetInterfaces().Where(x => x == triggerType);

                        foreach (var triggerTypeImplementation in triggerTypeImplementations)
                        {
                            if (triggerInstanceFactoryBuilder is null)
                            {
                                triggerInstanceFactoryBuilder =
                                    Expression.Lambda<Func<object?, object>>(
                                            Expression.New(
                                                typeof(TriggerInstanceFactory<>).MakeGenericType(triggerServiceType).GetConstructor(new[] { typeof(object) })!,
                                                instanceParamExpression
                                            ),
                                            instanceParamExpression
                                    )
                                    .Compile();
                            }

                            var triggerTypeImplementationFactoryType = typeof(ITriggerInstanceFactory<>).MakeGenericType(triggerTypeImplementation);
                            services.Add(new ServiceDescriptor(triggerTypeImplementationFactoryType, _ => triggerInstanceFactoryBuilder(triggerServiceInstance), lifetime));
                            services.AddScoped(typeof(IResettableService), serviceProvider => serviceProvider.GetRequiredService(triggerTypeImplementationFactoryType));
                        }
                    }
                }
            }
        }

        public void Validate(IDbContextOptions options) { }

        protected TriggersOptionExtension Clone() => new(this);

        private bool TypeIsValidTrigger(Type type)
        {
            if (TypeHelpers.FindGenericInterfaces(type, typeof(IBeforeSaveTrigger<>)) != null || TypeHelpers.FindGenericInterfaces(type, typeof(IAfterSaveTrigger<>)) != null)
            {
                return true;
            }
            else if (_triggerTypes != null)
            {
                return _triggerTypes.Any(triggerType => TypeHelpers.FindGenericInterfaces(type, triggerType) != null);
            }
            else
            {
                return false;
            }
        }

        public TriggersOptionExtension WithCascadeBehavior(CascadeBehavior cascadeBehavior)
        {
            var clone = Clone();

            clone._cascadeBehavior = cascadeBehavior;

            return clone;
        }

        public TriggersOptionExtension WithMaxCascadeCycles(int maxCascadeCycles)
        {
            var clone = Clone();

            clone._maxCascadeCycles = maxCascadeCycles;

            return clone;
        }

        public TriggersOptionExtension WithAdditionalTrigger(Type triggerType, ServiceLifetime lifetime)
        {
            if (!TypeIsValidTrigger(triggerType))
            {
                throw new ArgumentException("A trigger needs to implement either or both IBeforeSaveChangeTrigger or IAfterSaveChangeTriggerHandler", nameof(triggerType));
            }

            var clone = Clone();
            var triggerEnumerable = Enumerable.Repeat(((object)triggerType, lifetime), 1);

            if (clone._triggers == null)
            {
                clone._triggers = triggerEnumerable;
            }
            else
            {
                clone._triggers = clone._triggers.Concat(triggerEnumerable);
            }


            return clone;
        }

        public TriggersOptionExtension WithAdditionalTrigger(object instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!TypeIsValidTrigger(instance.GetType()))
            {
                throw new ArgumentException("A trigger needs to implement either or both IBeforeSaveChangeTrigger or IAfterSaveChangeTriggerHandler", nameof(instance));
            }

            var clone = Clone();
            var triggersEnumerable = Enumerable.Repeat((instance, ServiceLifetime.Singleton), 1);

            if (clone._triggers == null)
            {
                clone._triggers = triggersEnumerable;
            }
            else
            {
                clone._triggers = clone._triggers.Concat(triggersEnumerable);
            }


            return clone;
        }

        public TriggersOptionExtension WithAdditionalTriggerType(Type triggerType)
        {
            if (triggerType == null)
            {
                throw new ArgumentNullException(nameof(triggerType));
            }


            var clone = Clone();
            var triggerTypesEnumerable = Enumerable.Repeat(triggerType, 1);

            if (clone._triggerTypes == null)
            {
                clone._triggerTypes = triggerTypesEnumerable;
            }
            else
            {
                clone._triggerTypes = clone._triggerTypes.Concat(triggerTypesEnumerable);
            }


            return clone;
        }

        public TriggersOptionExtension WithApplicationScopedServiceProviderAccessor(Func<IServiceProvider, IServiceProvider> serviceProviderTransform)
        {
            var clone = Clone();
            clone._serviceProviderTransform = serviceProviderTransform;

            return clone;
        }
    }
}
