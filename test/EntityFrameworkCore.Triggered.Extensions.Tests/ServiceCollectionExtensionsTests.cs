using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityFrameworkCore.Triggered.Extensions.Tests;

public class ServiceCollectionExtensionsTests
{
    public static IEnumerable<object[]> Lifetimes()
    {
        yield return new object[] { ServiceLifetime.Transient };
        yield return new object[] { ServiceLifetime.Scoped };
        yield return new object[] { ServiceLifetime.Singleton };
    }

    public static IEnumerable<object[]> TriggerTypes()
    {
        yield return new object[] { typeof(IBeforeSaveTrigger<object>) };
        yield return new object[] { typeof(IAfterSaveTrigger<object>) };
        yield return new object[] { typeof(IAfterSaveFailedTrigger<object>) };
    }

    public class TestModel
    {
        public int Id { get; set; }
    }

    public class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<TestModel> TestModels { get; set; }
    }

    [Theory]
    [MemberData(nameof(Lifetimes))]
    public void AddTriggerOfT_WithCustomLifetime_RegistersWithThatLifetime(ServiceLifetime lifetime)
    {
        var serviceCollection = new ServiceCollection()
            .AddTrigger<Trigger<object>>(lifetime);

        Assert.Equal(lifetime, serviceCollection.First().Lifetime);
    }

    [Theory]
    [MemberData(nameof(TriggerTypes))]
    public void AddTriggerOfT_RegistersAllLifetimes(Type triggerLifetimeType)
    {
        var serviceCollection = new ServiceCollection()
            .AddTrigger<Trigger<object>>();

        Assert.Contains(serviceCollection, x => x.ServiceType == triggerLifetimeType);
    }

    [Theory]
    [MemberData(nameof(TriggerTypes))]
    public void AddTriggerInstance_RegistersAllTypes(Type triggerLifetimeType)
    {
        var serviceCollection = new ServiceCollection()
            .AddTrigger(new Trigger<object>());

        Assert.Contains(serviceCollection, x => x.ServiceType == triggerLifetimeType);
    }

    [Theory]
    [MemberData(nameof(Lifetimes))]
    public void AddAssemblyTriggers_WithCustomLifetime_RegistersWithThatLifetime(ServiceLifetime lifetime)
    {
        var serviceCollection = new ServiceCollection()
            .AddAssemblyTriggers(lifetime);

        Assert.Equal(lifetime, serviceCollection.First().Lifetime);
    }

    [Fact]
    public void AddAssemblyTriggers_WithAssembly_RegistersWithThatAssembly()
    {
        var serviceCollection = new ServiceCollection()
            .AddAssemblyTriggers(typeof(ServiceCollectionExtensionsTests).Assembly);

        Assert.Equal(16, serviceCollection.Count);
    }

    protected async Task SaveChanges_TriggeredAddedThroughDI_Template(Func<IServiceCollection, IServiceCollection> transform)
    {
        var services = transform(new ServiceCollection()
            .AddTriggeredDbContext<TestDbContext>(options => {
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            }));

        var serviceProvider = services
            .BuildServiceProvider();

        using var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
        dbContext.TestModels.Add(new TestModel { });

        await dbContext.SaveChangesAsync();

        var trigger = serviceProvider.GetRequiredService<SampleTrigger>();
        var triggerClone = serviceProvider.GetRequiredService<SampleTriggerClone>();

        Assert.Equal(1, trigger.BeforeSaveStartingTriggerCalls);
        Assert.Equal(1, trigger.BeforeSaveCalls);
        Assert.Equal(1, trigger.BeforeSaveAsyncCalls);
        Assert.Equal(1, trigger.AfterSaveCalls);
        Assert.Equal(1, trigger.AfterSaveAsyncCalls);

        Assert.Equal(1, triggerClone.BeforeSaveStartingTriggerCalls);
        Assert.Equal(1, triggerClone.BeforeSaveCalls);
        Assert.Equal(1, triggerClone.BeforeSaveAsyncCalls);
        Assert.Equal(1, triggerClone.AfterSaveCalls);
        Assert.Equal(1, triggerClone.AfterSaveAsyncCalls);
    }

    [Fact]
    public Task SaveChanges_ExplicitlyAddedTriggerThroughDI_RaisesAllTriggerTypes()
        => SaveChanges_TriggeredAddedThroughDI_Template(x => {
            x.AddTrigger<SampleTrigger>();
            x.AddTrigger<SampleTriggerClone>();

            return x;
        });

    [Fact]
    public Task SaveChanges_DiscoveredTriggerThroughDI_RaisesAllTriggerTypes()
        => SaveChanges_TriggeredAddedThroughDI_Template(x => x.AddAssemblyTriggers());

    [Fact]
    public void AddAssemblyTriggers_AbstractTrigger_GetsIgnored()
    {
        var serviceCollection = new ServiceCollection()
            .AddAssemblyTriggers();

        // Ensure that we did not register the AbstractTrigger
        Assert.DoesNotContain(serviceCollection, x => x.ImplementationType == typeof(AbstractTrigger));
    }

    [Theory]
    [MemberData(nameof(Lifetimes))]
    public void AddTrigger_Multiple_AddsRegistrationForAll(ServiceLifetime lifetime)
    {
        var serviceCollection = new ServiceCollection()
            .AddTrigger<SampleTrigger>(lifetime)
            .AddTrigger<SampleTriggerClone>(lifetime)
            .AddTrigger<Trigger<object>>(lifetime);

        Assert.Equal(23, serviceCollection.Count);
    }

    [Theory]
    [MemberData(nameof(Lifetimes))]
    public void AddAssemblyTriggers_Multiple_AddsRegistrationForAll(ServiceLifetime lifetime)
    {
        var serviceCollection = new ServiceCollection()
            .AddTrigger<SampleTrigger>(lifetime)
            .AddTrigger<SampleTriggerClone>(lifetime)
            .AddAssemblyTriggers(typeof(Trigger<object>).Assembly);

        Assert.Equal(23, serviceCollection.Count);
    }

    [Fact]
    public void AddTriggerInstance_DoesNotDoubleRegister()
    {
        var trigger = new SampleTrigger();

        var serviceCollection = new ServiceCollection()
            .AddTrigger(trigger)
            .AddTrigger(trigger);

        Assert.Single(serviceCollection, x => x.ImplementationInstance == trigger);
    }

    [Theory]
    [MemberData(nameof(Lifetimes))]
    public void AddTrigger_DoesNotDoubleRegister(ServiceLifetime lifetime)
    {
        var serviceCollection = new ServiceCollection()
            .AddTrigger<SampleTrigger>(lifetime)
            .AddTrigger<SampleTrigger>(lifetime);

        Assert.Single(serviceCollection, x => x.ServiceType == typeof(SampleTrigger));
    }

    [Theory]
    [MemberData(nameof(Lifetimes))]
    public void AddTrigger_DoesNotDoubleRegister2(ServiceLifetime lifetime)
    {
        var serviceCollection = new ServiceCollection()
            .AddAssemblyTriggers(lifetime)
            .AddTrigger<SampleTrigger>(lifetime);

        Assert.Single(serviceCollection, x => x.ServiceType == typeof(SampleTrigger));
    }

    [Theory]
    [MemberData(nameof(Lifetimes))]
    public void AddAssemblyTrigger_DoesNotDoubleRegister(ServiceLifetime lifetime)
    {
        var serviceCollection = new ServiceCollection()
            .AddAssemblyTriggers(lifetime)
            .AddAssemblyTriggers(lifetime);

        Assert.Single(serviceCollection, x => x.ServiceType == typeof(SampleTrigger));
    }

    [Theory]
    [MemberData(nameof(Lifetimes))]
    public void AddAssemblyTrigger_DoesNotDoubleRegister2(ServiceLifetime lifetime)
    {
        var serviceCollection = new ServiceCollection()
            .AddTrigger<SampleTrigger>(lifetime)
            .AddAssemblyTriggers(lifetime);

        Assert.Single(serviceCollection, x => x.ServiceType == typeof(SampleTrigger));
    }

    [Fact]
    public void AddTrigger_WhenExplicitlyRegisteredAsAService_OnlyAddsTheTriggerTypeRegistrations()
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton<SampleTrigger>()
            .AddTrigger<SampleTrigger>();

        Assert.Equal(8, serviceCollection.Count);
    }

    [Fact]
    public void AddAssemblyTriggers_WhenExplicitlyRegisteredAsAService_OnlyAddsTheTriggerTypeRegistrations()
    {
        var serviceCollection = new ServiceCollection()
            .AddSingleton<SampleTrigger>()
            .AddSingleton<SampleTriggerClone>()
            .AddAssemblyTriggers();

        Assert.Equal(16, serviceCollection.Count);
    }
}
