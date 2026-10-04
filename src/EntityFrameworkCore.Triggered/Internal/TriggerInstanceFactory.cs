using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.Triggered.Internal
{

    public interface ITriggerInstanceFactory
    {
        object Create(IServiceProvider serviceProvider);
    }

    public interface ITriggerInstanceFactory<out TTriggerType> : ITriggerInstanceFactory, IResettableService
    {

    }

    public sealed class TriggerInstanceFactory<TTriggerType> : ITriggerInstanceFactory<TTriggerType>
    {
        static ObjectFactory? _internalFactory;

        readonly object? _capturedInstance;
        object? _instance;

        public TriggerInstanceFactory(object? instance)
        {
            _capturedInstance = instance;
            _instance = instance;
        }

        public object Create(IServiceProvider serviceProvider)
        {
            if (_instance is not null)
            {
                return _instance;
            }

            if (_internalFactory is null)
            {
                _internalFactory = ActivatorUtilities.CreateFactory(typeof(TTriggerType), Array.Empty<Type>());
            }

            _instance = _internalFactory(serviceProvider, null);
            return _instance;
        }

        public void ResetState()
        {
            _instance = _capturedInstance;
        }

        public Task ResetStateAsync(CancellationToken cancellationToken = default)
        {
            ResetState();
            return Task.CompletedTask;
        }
    }
}
