using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggered.Tests.Stubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityFrameworkCore.Triggered.Tests
{
    public class TriggerSessionTests
    {
        public class TestModel
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class TestDbContext : DbContext
        {
            public TestDbContext(DbContextOptions options) : base(options)
            {
            }

            public TestDbContext()
            {
            }

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
                    triggerOptions.AddTrigger(TriggerStub);
                });
            }
        }

        protected static ITriggerSession CreateSubject(DbContext context)
            => context.Database.GetService<ITriggerService>().CreateSession(context);

        [Fact]
        public void RaiseBeforeSaveTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            subject.RaiseBeforeSaveTriggers();

            Assert.Empty(context.TriggerStub.BeforeSaveInvocations);
        }

        [Fact]
        public async Task RaiseBeforeSaveAsyncTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            await subject.RaiseBeforeSaveAsyncTriggers();

            Assert.Empty(context.TriggerStub.BeforeSaveAsyncInvocations);
        }

        [Fact]
        public void RaiseAfterSaveTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            subject.DiscoverChanges();
            subject.RaiseAfterSaveTriggers();

            Assert.Empty(context.TriggerStub.AfterSaveInvocations);
        }


        [Fact]
        public async Task RaiseAfterSaveAsyncTriggers_RaisesNothingOnNoChanges()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            subject.DiscoverChanges();
            await subject.RaiseAfterSaveAsyncTriggers();

            Assert.Empty(context.TriggerStub.AfterSaveAsyncInvocations);
        }

        [Fact]
        public void RaiseBeforeSaveTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            subject.RaiseBeforeSaveTriggers();

            Assert.Single(context.TriggerStub.BeforeSaveInvocations);
        }

        [Fact]
        public async Task RaiseBeforeSaveAsyncTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            await subject.RaiseBeforeSaveAsyncTriggers();

            Assert.Single(context.TriggerStub.BeforeSaveAsyncInvocations);
        }

        [Fact]
        public void RaiseAfterSaveTriggers_WithoutCallToRaiseBeforeSaveTriggers_Throws()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            Assert.Throws<InvalidOperationException>(() => subject.RaiseAfterSaveTriggers());
        }

        [Fact]
        public async Task RaiseAfterSaveAsyncTriggers_WithoutCallToRaiseBeforeSaveTriggers_Throws()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await subject.RaiseAfterSaveAsyncTriggers());
        }

        [Fact]
        public void RaiseAfterSaveTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            subject.DiscoverChanges();
            subject.RaiseAfterSaveTriggers();

            Assert.Single(context.TriggerStub.AfterSaveInvocations);
        }

        [Fact]
        public async Task RaiseAfterSaveAsyncTriggers_RaisesOnceOnSimpleAddition()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            subject.DiscoverChanges();
            await subject.RaiseAfterSaveAsyncTriggers();

            Assert.Single(context.TriggerStub.AfterSaveAsyncInvocations);
        }

        [Fact]
        public async Task RaiseBeforeSaveAsyncTriggers_ThrowsOnCancelledException()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await subject.RaiseBeforeSaveAsyncTriggers(cancellationTokenSource.Token));
        }

        [Fact]
        public async Task RaiseAfterSaveAsyncTriggers_ThrowsOnCancelledException()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            subject.DiscoverChanges();

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await subject.RaiseAfterSaveAsyncTriggers(cancellationTokenSource.Token));
        }

        [Fact]
        public void RaiseBeforeSaveTriggers_CascadingCall_SkipsDiscoveredChanges()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TriggerStub.BeforeSaveHandler = _ => {
                if (context.TriggerStub.BeforeSaveInvocations.Count <= 1)
                {
                    subject.RaiseBeforeSaveTriggers(default);
                }
            };

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            subject.DiscoverChanges();
            subject.RaiseBeforeSaveTriggers();

            Assert.NotEmpty(context.TriggerStub.BeforeSaveInvocations);
        }

        [Fact]
        public void RaiseBeforeSaveTriggers_SkipDetectedChangesAsTrue_ExcludesDetectedChanges()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            subject.DiscoverChanges();
            subject.RaiseBeforeSaveTriggers(true);

            Assert.Empty(context.TriggerStub.BeforeSaveInvocations);
        }

        [Fact]
        public void RaiseBeforeSaveTriggers_SkipDetectedChangesAsFalse_IncludesDetectedChanges()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            subject.DiscoverChanges();
            subject.RaiseBeforeSaveTriggers();

            Assert.NotEmpty(context.TriggerStub.BeforeSaveInvocations);
        }

        [Fact]
        public void RaiseBeforeSaveTriggers_MultipleEntities_SortByPriorities()
        {
            var capturedInvocations = new List<(string, ITriggerContext<TestModel>)>();

            var earlyTrigger = new TriggerStub<TestModel> {
                Priority = CommonTriggerPriority.Early,
                BeforeSaveHandler = context => {
                    capturedInvocations.Add(("Early", context));
                }
            };

            var lateTrigger = new TriggerStub<TestModel> {
                Priority = CommonTriggerPriority.Late,
                BeforeSaveHandler = context => {
                    capturedInvocations.Add(("Late", context));
                }
            };

            var serviceProvider = new ServiceCollection()
                .AddSingleton<IBeforeSaveTrigger<TestModel>>(lateTrigger)
                .AddSingleton<IBeforeSaveTrigger<TestModel>>(earlyTrigger)
                .AddTriggeredDbContext<TestDbContext>(options => {
                    options.UseInMemoryDatabase("Test");
                    options.ConfigureWarnings(warningOptions => {
                        warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    });
                })
                .BuildServiceProvider();

            var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var subject = CreateSubject(dbContext);

            dbContext.TestModels.Add(new TestModel { Id = 1 });
            dbContext.TestModels.Add(new TestModel { Id = 2 });

            subject.RaiseBeforeSaveTriggers();

            Assert.Equal(4, capturedInvocations.Count);
            Assert.Equal("Early", capturedInvocations[0].Item1);
            Assert.Equal("Early", capturedInvocations[1].Item1);
            Assert.Equal("Late", capturedInvocations[2].Item1);
            Assert.Equal("Late", capturedInvocations[3].Item1);
        }


        [Fact]
        public void RaiseBeforeSaveTriggers_CascadingAdd_RaisesSubsequentTriggers()
        {
            TestDbContext dbContext = null;

            var trigger = new TriggerStub<TestModel> {
                Priority = CommonTriggerPriority.Early,
                BeforeSaveHandler = context => {
                    if (context.Entity.Id == 1)
                    {
                        dbContext.TestModels.Add(new TestModel { Id = 2 });
                    }
                }
            };

            var serviceProvider = new ServiceCollection()
                .AddSingleton<IBeforeSaveTrigger<TestModel>>(trigger)
                .AddTriggeredDbContext<TestDbContext>(options => {
                    options.UseInMemoryDatabase("Test");
                    options.ConfigureWarnings(warningOptions => {
                        warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    });
                })
                .BuildServiceProvider();

            var scope = serviceProvider.CreateScope();
            dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var subject = CreateSubject(dbContext);

            dbContext.TestModels.Add(new TestModel { Id = 1 });

            subject.RaiseBeforeSaveTriggers();

            Assert.Equal(2, trigger.BeforeSaveInvocations.Count);
        }

        [Fact]
        public void RaiseAfterSaveFailedTriggers_OnException_RaisesSubsequentTriggers()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            subject.DiscoverChanges();
            subject.RaiseAfterSaveFailedTriggers(new Exception());

            Assert.Single(context.TriggerStub.AfterSaveFailedInvocations);
        }


        [Fact]
        public async Task RaiseAfterSaveFailedAsyncTriggers_OnException_RaisesSubsequentTriggers()
        {
            using var context = new TestDbContext();
            var subject = CreateSubject(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            subject.DiscoverChanges();
            await subject.RaiseAfterSaveFailedAsyncTriggers(new Exception());

            Assert.Single(context.TriggerStub.AfterSaveFailedAsyncInvocations);
        }

        [Fact]
        public void RaiseBeforeSaveTriggers_OnExceptionAndRecall_SkipsPreviousTriggers()
        {
            var firstTrigger = new TriggerStub<TestModel> { };
            var secondTrigger = new TriggerStub<TestModel> { };
            var lastTrigger = new TriggerStub<TestModel> { };

            secondTrigger.BeforeSaveHandler = ctx => {
                if (secondTrigger.BeforeSaveInvocations.Count == 0)
                {
                    throw new Exception("oh oh!");
                }
            };

            var serviceProvider = new ServiceCollection()
                .AddSingleton<IBeforeSaveTrigger<TestModel>>(firstTrigger)
                .AddSingleton<IBeforeSaveTrigger<TestModel>>(secondTrigger)
                .AddSingleton<IBeforeSaveTrigger<TestModel>>(lastTrigger)
                .AddTriggeredDbContext<TestDbContext>(options => {
                    options.UseInMemoryDatabase("Test");
                    options.ConfigureWarnings(warningOptions => {
                        warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    });
                })
                .BuildServiceProvider();

            var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var subject = CreateSubject(dbContext);

            dbContext.TestModels.Add(new TestModel { Id = 1 });

            try
            {
                subject.RaiseBeforeSaveTriggers();
            }
            catch
            {
                subject.RaiseBeforeSaveTriggers();
            }

            Assert.Single(firstTrigger.BeforeSaveInvocations);
            Assert.Single(secondTrigger.BeforeSaveInvocations);
            Assert.Single(lastTrigger.BeforeSaveInvocations);
        }

        [Fact]
        public void RaiseAfterSaveTriggers_ModifiedEntity_HasAccessToUnmodifiedEntity()
        {
            ITriggerContext<TestModel> _capturedTriggerContext = null;

            var trigger = new TriggerStub<TestModel> {
                AfterSaveHandler = context => {
                    _capturedTriggerContext = context;
                }
            };

            var serviceProvider = new ServiceCollection()
                .AddSingleton<IAfterSaveTrigger<TestModel>>(trigger)
                .AddTriggeredDbContext<TestDbContext>(options => {
                    options.UseInMemoryDatabase("Test");
                    options.ConfigureWarnings(warningOptions => {
                        warningOptions.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
                    });
                })
                .BuildServiceProvider();

            var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var subject = CreateSubject(dbContext);
            var testModel = new TestModel { Id = 1, Name = "test1" };

            dbContext.TestModels.Add(testModel);
            dbContext.SaveChanges();

            // act

            testModel.Name = "test2";

            subject.DiscoverChanges();
            dbContext.SaveChanges();
            subject.RaiseAfterSaveTriggers();

            // assert

            Assert.NotNull(_capturedTriggerContext);
            Assert.Equal(ChangeType.Modified, _capturedTriggerContext.ChangeType);
            Assert.Equal("test1", _capturedTriggerContext.UnmodifiedEntity.Name);
            Assert.Equal("test2", _capturedTriggerContext.Entity.Name);
        }

        [Fact]
        public void RaiseTriggers_DisabledConfiguration_Noop()
        {
            // arrange
            using var context = new TestDbContext();

            var triggerService = context.GetService<ITriggerService>();
            triggerService.Configuration = triggerService.Configuration with {
                Disabled = true
            };

            var subject = triggerService.CreateSession(context);

            context.TestModels.Add(new TestModel {
                Id = 1,
                Name = "test1"
            });

            // act
            subject.RaiseBeforeSaveTriggers();

            // assert
            Assert.Empty(context.TriggerStub.BeforeSaveInvocations);
        }
    }
}
