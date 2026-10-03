using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggered.Tests.Stubs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityFrameworkCore.Triggered.Tests.Internal
{
    /// <summary>
    /// Every way a SaveChanges call can end has to release the trigger session, or the next save on the same context reuses a stale one (#219).
    /// </summary>
    public class TriggerSessionSaveChangesInterceptorReleaseTests : IDisposable
    {
        public class Item
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public int Version { get; set; }
        }

        public class ItemDbContext : DbContext
        {
            public ItemDbContext(DbContextOptions<ItemDbContext> options) : base(options) { }

            public DbSet<Item> Items { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Item>().Property(x => x.Version).IsConcurrencyToken();
            }
        }

        class CancelingCommandInterceptor : DbCommandInterceptor
        {
            public bool Enabled { get; set; }

            public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
                => Enabled ? throw new OperationCanceledException() : result;

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
                => Enabled ? throw new OperationCanceledException() : new ValueTask<InterceptionResult<DbDataReader>>(result);
        }

        readonly SqliteConnection _connection = new("DataSource=:memory:");
        readonly CancelingCommandInterceptor _cancelingInterceptor = new();
        readonly TriggerStub<Item> _trigger = new();

        public TriggerSessionSaveChangesInterceptorReleaseTests()
        {
            _connection.Open();
        }

        public void Dispose() => _connection.Dispose();

        void ConfigureOptions(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseSqlite(_connection)
                .AddInterceptors(_cancelingInterceptor)
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .UseTriggers(triggerOptions => triggerOptions.AddTrigger(_trigger));

        ItemDbContext CreateContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<ItemDbContext>();
            ConfigureOptions(optionsBuilder);

            var context = new ItemDbContext(optionsBuilder.Options);
            context.Database.EnsureCreated();
            return context;
        }

        static Task Save(DbContext context, bool async)
        {
            if (async)
            {
                return context.SaveChangesAsync();
            }

            context.SaveChanges();
            return Task.CompletedTask;
        }

        static Item SeedItem(ItemDbContext context)
        {
            var item = new Item { Id = Guid.NewGuid(), Name = "seed" };
            context.Items.Add(item);
            context.SaveChanges();
            return item;
        }

        static void CauseConcurrencyConflict(ItemDbContext context, Item item)
        {
            context.Database.ExecuteSqlRaw("UPDATE Items SET Version = Version + 1");
            item.Name = "conflicting";
        }

        async Task AssertNextSaveRaisesBeforeSaveTriggers(ItemDbContext context, bool async)
        {
            Assert.Null(context.GetService<ITriggerService>().Current);

            foreach (var entry in context.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }

            _trigger.BeforeSaveInvocations.Clear();
            _trigger.BeforeSaveAsyncInvocations.Clear();

            var item = new Item { Id = Guid.NewGuid(), Name = "next" };
            context.Items.Add(item);
            await Save(context, async);

            Assert.Contains(_trigger.BeforeSaveInvocations, invocation => invocation.Entity == item);
            if (async)
            {
                Assert.Contains(_trigger.BeforeSaveAsyncInvocations, invocation => invocation.Entity == item);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ConcurrencyException_ReleasesTriggerSession(bool async)
        {
            using var context = CreateContext();
            CauseConcurrencyConflict(context, SeedItem(context));

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Save(context, async));

            await AssertNextSaveRaisesBeforeSaveTriggers(context, async);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ConcurrencyException_RaisesAfterSaveFailedTriggers(bool async)
        {
            using var context = CreateContext();
            var item = SeedItem(context);
            CauseConcurrencyConflict(context, item);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Save(context, async));

            var invocation = Assert.Single(_trigger.AfterSaveFailedInvocations);
            Assert.Same(item, invocation.context.Entity);
            Assert.IsType<DbUpdateConcurrencyException>(invocation.exception);
            Assert.Equal(async ? 1 : 0, _trigger.AfterSaveFailedAsyncInvocations.Count);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Cancellation_ReleasesTriggerSession(bool async)
        {
            using var context = CreateContext();
            SeedItem(context).Name = "changed";
            _cancelingInterceptor.Enabled = true;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Save(context, async));

            _cancelingInterceptor.Enabled = false;
            await AssertNextSaveRaisesBeforeSaveTriggers(context, async);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ThrowingAfterSaveFailedTrigger_ReleasesTriggerSession(bool async)
        {
            using var context = CreateContext();
            var item = SeedItem(context);
            context.Entry(item).State = EntityState.Detached;
            context.Items.Add(new Item { Id = item.Id, Name = "duplicate" });
            _trigger.AfterSaveFailedHandler = (_, _) => throw new InvalidOperationException("trigger failed");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Save(context, async));
            Assert.Equal("trigger failed", exception.Message);

            _trigger.AfterSaveFailedHandler = null;
            await AssertNextSaveRaisesBeforeSaveTriggers(context, async);
        }

        [Fact]
        public async Task PooledContext_AfterConcurrencyException_NextLeaseRaisesAsyncBeforeSaveTriggers()
        {
            using var serviceProvider = new ServiceCollection()
                .AddTriggeredDbContextPool<ItemDbContext>(ConfigureOptions, poolSize: 1)
                .BuildServiceProvider();

            Guid seededId;
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ItemDbContext>();
                context.Database.EnsureCreated();
                seededId = SeedItem(context).Id;
            }

            ItemDbContext pooledInstance;
            using (var scope = serviceProvider.CreateScope())
            {
                pooledInstance = scope.ServiceProvider.GetRequiredService<ItemDbContext>();
                CauseConcurrencyConflict(pooledInstance, await pooledInstance.Items.SingleAsync(x => x.Id == seededId));

                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => pooledInstance.SaveChangesAsync());
            }

            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ItemDbContext>();
                Assert.Same(pooledInstance, context);
                _trigger.BeforeSaveAsyncInvocations.Clear();

                var item = new Item { Id = Guid.NewGuid(), Name = "next lease" };
                context.Items.Add(item);
                await context.SaveChangesAsync();

                Assert.Contains(_trigger.BeforeSaveAsyncInvocations, invocation => invocation.Entity == item);
            }
        }
    }
}
