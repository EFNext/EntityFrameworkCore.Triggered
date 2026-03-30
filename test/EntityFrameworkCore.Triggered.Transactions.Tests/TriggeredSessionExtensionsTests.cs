using System;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggered.Transactions.Tests.Stubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace EntityFrameworkCore.Triggered.Transactions.Tests
{
    public class TriggeredSessionExtensionsTests
    {
        class TestModel
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
        }

        class TestDbContext : DbContext
        {
            public TriggerStub<TestModel> TriggerStub { get; } = new TriggerStub<TestModel>();

            public DbSet<TestModel> TestModels { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                base.OnConfiguring(optionsBuilder);

                optionsBuilder.ConfigureWarnings(warningOptions => {
                    warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                });
                optionsBuilder.UseInMemoryDatabase("test");
                optionsBuilder.UseTriggers(triggerOptions => {
                    triggerOptions
                        .UseTransactionTriggers()
                        .AddTrigger(TriggerStub);
                });
            }
        }

        protected static ITriggerSession CreateSession(DbContext context)
            => context.Database.GetService<ITriggerService>().CreateSession(context);


        [Fact]
        public void RaiseBeforeCommitTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.DiscoverChanges();
            session.RaiseBeforeCommitTriggers();

            Assert.Empty(context.TriggerStub.BeforeCommitInvocations);
        }

        [Fact]
        public async Task RaiseBeforeCommitAsyncTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.DiscoverChanges();
            await session.RaiseBeforeCommitAsyncTriggers();

            Assert.Empty(context.TriggerStub.BeforeCommitAsyncInvocations);
        }

        [Fact]
        public void RaiseBeforeCommitTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            context.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            session.DiscoverChanges();
            session.RaiseBeforeCommitTriggers();

            Assert.Single(context.TriggerStub.BeforeCommitInvocations);
        }

        [Fact]
        public async Task RaiseBeforeCommitAsyncTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            context.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            session.DiscoverChanges();
            await session.RaiseBeforeCommitAsyncTriggers();

            Assert.Single(context.TriggerStub.BeforeCommitAsyncInvocations);
        }

        [Fact]
        public void RaiseAfterCommitTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.DiscoverChanges();
            session.RaiseAfterCommitTriggers();

            Assert.Empty(context.TriggerStub.AfterCommitInvocations);
        }

        [Fact]
        public async Task RaiseAfterCommitAsyncTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.DiscoverChanges();
            await session.RaiseAfterCommitAsyncTriggers();

            Assert.Empty(context.TriggerStub.AfterCommitAsyncInvocations);
        }

        [Fact]
        public void RaiseAfterCommitTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            context.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            session.DiscoverChanges();
            session.RaiseAfterCommitTriggers();

            Assert.Single(context.TriggerStub.AfterCommitInvocations);
        }

        [Fact]
        public async Task RaiseAfterCommitAsyncTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            context.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            session.DiscoverChanges();
            await session.RaiseAfterCommitAsyncTriggers();

            Assert.Single(context.TriggerStub.AfterCommitAsyncInvocations);
        }

        [Fact]
        public void RaiseBeforeRollbackTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.DiscoverChanges();
            session.RaiseBeforeRollbackTriggers();

            Assert.Empty(context.TriggerStub.BeforeRollbackInvocations);
        }

        [Fact]
        public async Task RaiseBeforeRollbackAsyncTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.DiscoverChanges();
            await session.RaiseBeforeRollbackAsyncTriggers();

            Assert.Empty(context.TriggerStub.BeforeRollbackAsyncInvocations);
        }

        [Fact]
        public void RaiseBeforeRollbackTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            context.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            session.DiscoverChanges();
            session.RaiseBeforeRollbackTriggers();

            Assert.Single(context.TriggerStub.BeforeRollbackInvocations);
        }

        [Fact]
        public async Task RaiseBeforeRollbackAsyncTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            context.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            session.DiscoverChanges();
            await session.RaiseBeforeRollbackAsyncTriggers();

            Assert.Single(context.TriggerStub.BeforeRollbackAsyncInvocations);
        }

        [Fact]
        public void RaiseAfterRollbackTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.DiscoverChanges();
            session.RaiseAfterRollbackTriggers();

            Assert.Empty(context.TriggerStub.AfterRollbackInvocations);
        }

        [Fact]
        public async Task RaiseAfterRollbackAsyncTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.DiscoverChanges();
            await session.RaiseAfterRollbackAsyncTriggers();

            Assert.Empty(context.TriggerStub.AfterRollbackAsyncInvocations);
        }

        [Fact]
        public void RaiseAfterRollbackTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            context.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            session.DiscoverChanges();
            session.RaiseAfterRollbackTriggers();

            Assert.Single(context.TriggerStub.AfterRollbackInvocations);
        }

        [Fact]
        public async Task RaiseAfterRollbackAsyncTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            context.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            session.DiscoverChanges();
            await session.RaiseAfterRollbackAsyncTriggers();

            Assert.Single(context.TriggerStub.AfterRollbackAsyncInvocations);
        }

        [Fact]
        public void RaiseBeforeCommitStartingTriggers_CallsTriggers()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.RaiseBeforeCommitStartingTriggers();

            Assert.Equal(1, context.TriggerStub.BeforeCommitStartingInvocationsCount);
        }

        [Fact]
        public async Task RaiseBeforeCommitStartingAsyncTriggers_CallsTriggers()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            await session.RaiseBeforeCommitStartingAsyncTriggers();

            Assert.Equal(1, context.TriggerStub.BeforeCommitStartingAsyncInvocationsCount);
        }

        [Fact]
        public void RaiseBeforeCommitCompletedTriggers_CallsTriggers()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.RaiseBeforeCommitCompletedTriggers();

            Assert.Equal(1, context.TriggerStub.BeforeCommitCompletedInvocationsCount);
        }

        [Fact]
        public async Task RaiseBeforeCommitCompletedAsyncTriggers_CallsTriggers()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            await session.RaiseBeforeCommitCompletedAsyncTriggers();

            Assert.Equal(1, context.TriggerStub.BeforeCommitCompletedAsyncInvocationsCount);
        }

        [Fact]
        public void RaiseAfterCommitStartingTriggers_CallsTriggers()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.RaiseAfterCommitStartingTriggers();

            Assert.Equal(1, context.TriggerStub.AfterCommitStartingInvocationsCount);
        }

        [Fact]
        public async Task RaiseAfterCommitStartingAsyncTriggers_CallsTriggers()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            await session.RaiseAfterCommitStartingAsyncTriggers();

            Assert.Equal(1, context.TriggerStub.AfterCommitStartingAsyncInvocationsCount);
        }

        [Fact]
        public void RaiseAfterCommitCompletedTrigger_CallsTriggers()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            session.RaiseAfterCommitCompletedTriggers();

            Assert.Equal(1, context.TriggerStub.AfterCommitCompletedInvocationsCount);
        }

        [Fact]
        public async Task RaiseAfterCommitCompletedAsyncTrigger_CallsTriggers()
        {
            using var context = new TestDbContext();
            var session = CreateSession(context);

            await session.RaiseAfterCommitCompletedAsyncTriggers();

            Assert.Equal(1, context.TriggerStub.AfterCommitCompletedAsyncInvocationsCount);
        }
    }
}
