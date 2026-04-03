using System;
using EntityFrameworkCore.Triggered.Tests.Stubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityFrameworkCore.Triggered.Tests
{
    public class TriggerServiceApplicationDependenciesTests
    {
        class TestModel { public int Id { get; set; } }

#pragma warning disable CS0618 // Type or member is obsolete
        class TestDbContext : DbContext
#pragma warning restore CS0618 // Type or member is obsolete
        {
            public TestDbContext(DbContextOptions options)
                : base(options)
            {

            }

            public DbSet<TestModel> TestModels { get; set; }
        }

        [Fact]
        public void ScopedTriggers_NoScopedApplicationServiceProvider_ForDbContext_DoesNotLeak()
        {
            var applicationServiceProvider = new ServiceCollection()
                .AddDbContext<TestDbContext>(options => {
                    options.UseInMemoryDatabase("Test");
                    options.UseTriggers();
                    options.ConfigureWarnings(warningOptions => {
                        warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    });
                })
                .AddScoped<IBeforeSaveTrigger<TestModel>, Stubs.TriggerStub<TestModel>>()
                .BuildServiceProvider();

            void SimulateRequest()
            {
                using var serviceScope = applicationServiceProvider.CreateScope();
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<TestDbContext>();
                var triggerStub = serviceScope.ServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;
                Assert.Empty(triggerStub.BeforeSaveInvocations);

                dbContext.Add(new TestModel { });
                dbContext.SaveChanges();
            }

            SimulateRequest();
            SimulateRequest();
        }

        [Fact]
        public void ScopedTriggers_NoScopedApplicationServiceProvider_ForPooledDbContext()
        {
            var applicationServiceProvider = new ServiceCollection()
                .AddDbContextPool<TestDbContext>(options => {
                    options.UseInMemoryDatabase("Test");
                    options.UseTriggers();
                    options.ConfigureWarnings(warningOptions => {
                        warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    });
                })
                .AddScoped<IBeforeSaveTrigger<TestModel>, TriggerStub<TestModel>>()
                .BuildServiceProvider();

            using var serviceScope = applicationServiceProvider.CreateScope();
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<TestDbContext>();

            dbContext.Add(new TestModel { });
            dbContext.SaveChanges();

            var triggerStub = serviceScope.ServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;

            Assert.Empty(triggerStub.BeforeSaveInvocations);

        }

        [Fact]
        public void ScopedTriggers_WithScopedApplicationServiceProvider_ForDbContext()
        {
            IServiceProvider scopedServiceProvider = null;

            var applicationServiceProvider = new ServiceCollection()
                .AddDbContext<TestDbContext>(options => {
                    options.UseInMemoryDatabase("Test");
                    options.UseTriggers(triggerOptions => {
                        triggerOptions.UseApplicationScopedServiceProviderAccessor(_ => scopedServiceProvider);
                    });
                    options.ConfigureWarnings(warningOptions => {
                        warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    });
                })
                .AddScoped<IBeforeSaveTrigger<TestModel>, Stubs.TriggerStub<TestModel>>()
                .BuildServiceProvider();

            using var serviceScope = applicationServiceProvider.CreateScope();
            scopedServiceProvider = serviceScope.ServiceProvider;
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<TestDbContext>();

            dbContext.Add(new TestModel { });
            dbContext.SaveChanges();

            var triggerStub = scopedServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;

            Assert.Single(triggerStub.BeforeSaveInvocations);
        }


        [Fact]
        public void ScopedTriggers_WithScopedApplicationServiceProvider_ForPooledDbContext()
        {
            IServiceProvider scopedServiceProvider = null;

            var applicationServiceProvider = new ServiceCollection()
                .AddDbContextPool<TestDbContext>(options => {
                    options.UseInMemoryDatabase("Test");
                    options.UseTriggers(triggerOptions => {
                        triggerOptions.UseApplicationScopedServiceProviderAccessor(_ => scopedServiceProvider);
                    });
                    options.ConfigureWarnings(warningOptions => {
                        warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    });
                })
                .AddScoped<IBeforeSaveTrigger<TestModel>, TriggerStub<TestModel>>()
                .BuildServiceProvider();

            using var serviceScope = applicationServiceProvider.CreateScope();
            scopedServiceProvider = serviceScope.ServiceProvider;
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<TestDbContext>();

            dbContext.Add(new TestModel { });
            dbContext.SaveChanges();

            var triggerStub = scopedServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;

            Assert.Single(triggerStub.BeforeSaveInvocations);
        }

        [Fact]
        public void ScopedTriggers_ForPooledDbContext_DoNotShareServiceProvider()
        {
            IServiceProvider scopedServiceProvider = null;

            var applicationServiceProvider = new ServiceCollection()
                .AddDbContextPool<TestDbContext>(options => {
                    options.UseInMemoryDatabase("Test");
                    options.UseTriggers(triggerOptions => {
                        triggerOptions.UseApplicationScopedServiceProviderAccessor(_ => scopedServiceProvider);
                    });
                    options.ConfigureWarnings(warningOptions => {
                        warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    });
                })
                .AddScoped<IBeforeSaveTrigger<TestModel>, TriggerStub<TestModel>>()
                .BuildServiceProvider();

            void SimulateRequest()
            {
                using var serviceScope = applicationServiceProvider.CreateScope();
                scopedServiceProvider = serviceScope.ServiceProvider;
                using var dbContext = serviceScope.ServiceProvider.GetRequiredService<TestDbContext>();

                dbContext.Add(new TestModel { });
                dbContext.SaveChanges();

                var triggerStub = scopedServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;

                Assert.Single(triggerStub.BeforeSaveInvocations);
            }

            SimulateRequest();
            SimulateRequest();
        }
    }
}
