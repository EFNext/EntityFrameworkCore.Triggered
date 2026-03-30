using System;
using EntityFrameworkCore.Triggered.Tests.Stubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityFrameworkCore.Triggered.Tests.Infrastructure
{
    public class ServiceCollectionExtensionsTests
    {
        class TestModel { public int Id { get; set; } }
#pragma warning disable CS0618 // Type or member is obsolete
        class TestDbContext : DbContext
#pragma warning restore CS0618 // Type or member is obsolete
        {

            public DbSet<TestModel> TestModels { get; set; }
            public TestDbContext(DbContextOptions options) : base(options)
            {
            }
        }

        class TestDbContextFactory : DbContextFactory<TestDbContext>
        {
            public TestDbContextFactory(IServiceProvider serviceProvider, DbContextOptions<TestDbContext> options, IDbContextFactorySource<TestDbContext> factorySource) : base(serviceProvider, options, factorySource)
            {
            }
        }

        [Fact]
        public void AddTriggeredDbContext_AddsTriggersAndCallsUsersAction()
        {
            var subject = new ServiceCollection();
            var optionsActionsCalled = false;
            subject.AddTriggeredDbContext<TestDbContext>(options => {
                optionsActionsCalled = true;
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            });

            var serviceProvider = subject.BuildServiceProvider();
            var context = serviceProvider.GetRequiredService<TestDbContext>();

            Assert.True(optionsActionsCalled);
            Assert.NotNull(context.GetService<ITriggerService>());
        }

        [Fact]
        public void AddTriggeredDbContextPool_AddsTriggersAndCallsUsersAction()
        {
            var subject = new ServiceCollection();
            var optionsActionsCalled = false;
            subject.AddTriggeredDbContext<TestDbContext>(options => {
                optionsActionsCalled = true;
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            });

            var serviceProvider = subject.BuildServiceProvider();
            var context = serviceProvider.GetRequiredService<TestDbContext>();

            Assert.True(optionsActionsCalled);
            Assert.NotNull(context.GetService<ITriggerService>());
        }


        [Fact]
        public void AddTriggeredDbContext_ReusesScopedServiceProvider()
        {
            var subject = new ServiceCollection();
            subject.AddTriggeredDbContext<TestDbContext>(options => {
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            }).AddScoped<IBeforeSaveTrigger<TestModel>, TriggerStub<TestModel>>();

            var serviceProvider = subject.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            context.TestModels.Add(new TestModel());

            context.SaveChanges();

            var triggerStub = scope.ServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;
            Assert.NotNull(triggerStub);
            Assert.Single(triggerStub.BeforeSaveInvocations);
        }


        [Fact]
        public void AddTriggeredDbContextPool_ReusesScopedServiceProvider()
        {
            var subject = new ServiceCollection();
            subject.AddTriggeredDbContextPool<TestDbContext>(options => {
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            }).AddScoped<IBeforeSaveTrigger<TestModel>, TriggerStub<TestModel>>();

            var serviceProvider = subject.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            context.TestModels.Add(new TestModel());

            context.SaveChanges();

            var triggerStub = scope.ServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;
            Assert.NotNull(triggerStub);
            Assert.Single(triggerStub.BeforeSaveInvocations);
        }

        [Fact]
        public void AddTriggeredDbContextPool_SupportsAScopedLifetime()
        {
            var subject = new ServiceCollection();
            subject.AddTriggeredDbContextPool<TestDbContext>(options => {
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            });

            var serviceProvider = subject.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var context1 = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var context2 = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            Assert.Equal(context1, context1);
        }

        [Fact]
        public void AddTriggeredDbContextPool_SupportAContractType()
        {
            var subject = new ServiceCollection();
            subject.AddTriggeredDbContextPool<DbContext, TestDbContext>(options => {
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            });

            var serviceProvider = subject.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var context1 = scope.ServiceProvider.GetRequiredService<DbContext>();
            var context2 = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            Assert.Equal(context1, context1);
        }

        [Fact]
        public void AddTriggeredDbContextFactory_ReusesScopedServiceProvider()
        {
            var subject = new ServiceCollection();
            subject.AddTriggeredDbContextFactory<TestDbContext>((_, options) => {
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            }).AddScoped<IBeforeSaveTrigger<TestModel>, TriggerStub<TestModel>>();

            var serviceProvider = subject.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TestDbContext>>();
            var context = contextFactory.CreateDbContext();

            context.TestModels.Add(new TestModel());

            context.SaveChanges();

            var triggerStub = scope.ServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;
            Assert.NotNull(triggerStub);
            Assert.Single(triggerStub.BeforeSaveInvocations);
        }

        [Fact]
        public void AddTriggeredDbContextFactory_WithCustomFactory_ReusesScopedServiceProvider()
        {
            var subject = new ServiceCollection();
            subject.AddTriggeredDbContextFactory<TestDbContext, TestDbContextFactory>(options => {
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            }).AddScoped<IBeforeSaveTrigger<TestModel>, TriggerStub<TestModel>>();

            var serviceProvider = subject.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TestDbContext>>();

            var context = contextFactory.CreateDbContext();

            context.TestModels.Add(new TestModel());

            context.SaveChanges();

            var triggerStub = scope.ServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;
            Assert.NotNull(triggerStub);
            Assert.Single(triggerStub.BeforeSaveInvocations);
        }

        [Fact]
        public void AddTriggeredPooledDbContextFactory_ReusesScopedServiceProvider()
        {
            var subject = new ServiceCollection();
            subject.AddTriggeredPooledDbContextFactory<TestDbContext>(options => {
                options.UseInMemoryDatabase("test");
                options.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
            }).AddScoped<IBeforeSaveTrigger<TestModel>, TriggerStub<TestModel>>();

            var serviceProvider = subject.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TestDbContext>>();
            var context = contextFactory.CreateDbContext();

            context.TestModels.Add(new TestModel());

            context.SaveChanges();

            var triggerStub = scope.ServiceProvider.GetRequiredService<IBeforeSaveTrigger<TestModel>>() as TriggerStub<TestModel>;
            Assert.NotNull(triggerStub);
            Assert.Single(triggerStub.BeforeSaveInvocations);
        }
    }
}
