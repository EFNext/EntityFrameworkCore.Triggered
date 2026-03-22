using System;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.Triggered.Benchmarks
{
    /// <summary>
    /// Measures trigger resolution cost in isolation by creating a new DbContext on every iteration.
    /// This exposes the per-instantiation overhead of the triggered infrastructure compared to a
    /// plain DbContext, and how that overhead scales with the number of registered triggers.
    /// </summary>
    [MemoryDiagnoser]
    public class TriggerResolutionBenchmarks
    {
        private IServiceProvider _plainServiceProvider;
        private IServiceProvider _triggered0ServiceProvider;  // UseTriggers() with no triggers registered
        private IServiceProvider _triggered1ServiceProvider;  // 1 lightweight trigger (no DB query)
        private IServiceProvider _triggered2ServiceProvider;  // 2 triggers, one of which queries the DB

        [GlobalSetup]
        public void GlobalSetup()
        {
            _plainServiceProvider = new ServiceCollection()
                .AddDbContext<ApplicationContext>(options =>
                    options.UseInMemoryDatabase("TriggerResolution_Plain"))
                .BuildServiceProvider();

            _triggered0ServiceProvider = new ServiceCollection()
                .AddDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("TriggerResolution_T0").UseTriggers())
                .BuildServiceProvider();

            _triggered1ServiceProvider = new ServiceCollection()
                .AddTriggeredDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("TriggerResolution_T1").UseTriggers(triggerOptions =>
                        triggerOptions.AddTrigger<Triggers.SetStudentRegistrationDateTrigger>()))
                .BuildServiceProvider();

            _triggered2ServiceProvider = new ServiceCollection()
                .AddTriggeredDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("TriggerResolution_T2").UseTriggers(triggerOptions =>
                    {
                        triggerOptions.AddTrigger<Triggers.SetStudentRegistrationDateTrigger>();
                        triggerOptions.AddTrigger<Triggers.SignStudentUpForMandatoryCourses>();
                    }))
                .BuildServiceProvider();

            // Seed a mandatory course so SignStudentUpForMandatoryCourses has data to work with.
            SeedCourse(_triggered2ServiceProvider);
        }

        private static void SeedCourse(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<TriggeredApplicationContext>();
            context.Database.EnsureCreated();
            context.Courses.Add(new Course { Id = Guid.NewGuid(), DisplayName = "Mandatory Course", IsMandatory = true });
            context.SaveChanges();
        }

        /// <summary>
        /// Baseline: standard DbContext with no triggered infrastructure.
        /// A new scope and DbContext are created on each iteration.
        /// </summary>
        [Benchmark(Baseline = true)]
        public void PlainDbContext()
        {
            using var scope = _plainServiceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
            context.Students.Add(new Student { Id = Guid.NewGuid(), DisplayName = "Test" });
            context.SaveChanges();
        }

        /// <summary>
        /// Triggered infrastructure enabled (UseTriggers), but no triggers registered.
        /// Measures the raw overhead of the trigger resolution engine with an empty registry.
        /// </summary>
        [Benchmark]
        public void TriggeredDbContext_NoTriggers()
        {
            using var scope = _triggered0ServiceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<TriggeredApplicationContext>();
            context.Students.Add(new Student { Id = Guid.NewGuid(), DisplayName = "Test" });
            context.SaveChanges();
        }

        /// <summary>
        /// 1 trigger registered: SetStudentRegistrationDateTrigger (synchronous, no DB query).
        /// Measures resolution + execution cost for a lightweight trigger.
        /// </summary>
        [Benchmark]
        public void TriggeredDbContext_OneTrigger()
        {
            using var scope = _triggered1ServiceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<TriggeredApplicationContext>();
            context.Students.Add(new Student { Id = Guid.NewGuid(), DisplayName = "Test" });
            context.SaveChanges();
        }

        /// <summary>
        /// 2 triggers registered: SetStudentRegistrationDateTrigger + SignStudentUpForMandatoryCourses
        /// (the second performs a DB query and adds new entities).
        /// Measures resolution + execution cost for triggers with side effects.
        /// </summary>
        [Benchmark]
        public void TriggeredDbContext_TwoTriggers()
        {
            using var scope = _triggered2ServiceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<TriggeredApplicationContext>();
            context.Students.Add(new Student { Id = Guid.NewGuid(), DisplayName = "Test" });
            context.SaveChanges();
        }
    }
}
