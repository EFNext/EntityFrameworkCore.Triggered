using System;
using EntityFrameworkCore.Triggered.Extensions;
using EntityFrameworkCore.Triggered.Transactions.Tests.Stubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EntityFrameworkCore.Triggered.Transactions.Tests
{
    public class TriggeredDbContextTests
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

        [Fact]
        public void RaiseBeforeCommitTriggers_DiscoveredChangesFromTriggeredDbContext_CallsTriggers()
        {
            var dbContext = new TestDbContext();
            using var subject = dbContext.CreateTriggerSession();

            dbContext.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            dbContext.SaveChanges();

            subject.RaiseBeforeCommitTriggers();

            Assert.Single(dbContext.TriggerStub.BeforeCommitInvocations);
        }


        [Fact]
        public void RaiseAfterCommitTriggers_DiscoveredChangesFromTriggeredDbContext_CallsTriggers()
        {
            var dbContext = new TestDbContext();
            using var subject = dbContext.CreateTriggerSession();

            dbContext.TestModels.Add(new TestModel {
                Id = Guid.NewGuid(),
                Name = "test1"
            });

            dbContext.SaveChanges();

            subject.RaiseAfterCommitTriggers();

            Assert.Single(dbContext.TriggerStub.AfterCommitInvocations);
        }
    }
}
