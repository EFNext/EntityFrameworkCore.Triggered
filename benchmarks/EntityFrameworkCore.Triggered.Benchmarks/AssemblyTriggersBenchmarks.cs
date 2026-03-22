using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.Triggered.Benchmarks
{
    /// <summary>
    /// Measures the setup cost (assembly scan + trigger registration) without building the
    /// ServiceProvider, isolating the overhead of AddAssemblyTriggers vs explicit AddTrigger&lt;T&gt;.
    ///
    /// Note: TriggerTypeHelper caches results per type after the first iteration.
    /// BenchmarkDotNet measures steady-state (warm cache).
    /// </summary>
    [MemoryDiagnoser]
    public class AssemblyTriggersSetupBenchmarks
    {
        private static readonly Assembly BenchmarkAssembly = typeof(AssemblyTriggersSetupBenchmarks).Assembly;

        /// <summary>
        /// Baseline: 2 triggers registered explicitly via AddTrigger&lt;T&gt;() inside UseTriggers().
        /// </summary>
        [Benchmark(Baseline = true)]
        public IServiceCollection Explicit_2Triggers_ViaOptions()
        {
            return new ServiceCollection()
                .AddDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("Setup_Explicit_Options").UseTriggers(o =>
                    {
                        o.AddTrigger<Triggers.SetStudentRegistrationDateTrigger>();
                        o.AddTrigger<Triggers.SignStudentUpForMandatoryCourses>();
                    }));
        }

        /// <summary>
        /// Assembly scan via TriggersContextOptionsBuilder.AddAssemblyTriggers().
        /// Discovers 2 Student triggers + 5 StudentCourse triggers = 7 types total.
        /// </summary>
        [Benchmark]
        public IServiceCollection AssemblyTriggers_ViaOptions()
        {
            return new ServiceCollection()
                .AddDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("Setup_Assembly_Options").UseTriggers(o =>
                        o.AddAssemblyTriggers(BenchmarkAssembly)));
        }

        /// <summary>
        /// Baseline: 2 triggers registered explicitly via IServiceCollection.AddTrigger&lt;T&gt;().
        /// </summary>
        [Benchmark]
        public IServiceCollection Explicit_2Triggers_ViaServiceCollection()
        {
            return new ServiceCollection()
                .AddTriggeredDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("Setup_Explicit_SC"))
                .AddTrigger<Triggers.SetStudentRegistrationDateTrigger>()
                .AddTrigger<Triggers.SignStudentUpForMandatoryCourses>();
        }

        /// <summary>
        /// Assembly scan via IServiceCollection.AddAssemblyTriggers().
        /// Registers all 7 triggers (2 Student + 5 StudentCourse) directly in the application DI container.
        /// </summary>
        [Benchmark]
        public IServiceCollection AssemblyTriggers_ViaServiceCollection()
        {
            return new ServiceCollection()
                .AddTriggeredDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("Setup_Assembly_SC"))
                .AddAssemblyTriggers(BenchmarkAssembly);
        }
    }

    /// <summary>
    /// Measures the per-SaveChanges runtime cost when triggers were registered via
    /// AddAssemblyTriggers (7 triggers in the assembly = 2 for Student + 5 for StudentCourse)
    /// vs explicit registration targeting only the 2 Student triggers.
    ///
    /// Answers: "Does registering extra non-applicable triggers (discovered by assembly scan)
    /// slow down the hot path?"
    ///
    /// A new DbContext is created on every iteration to include instantiation cost.
    /// </summary>
    [MemoryDiagnoser]
    public class AssemblyTriggersRuntimeBenchmarks
    {
        private static readonly Assembly BenchmarkAssembly = typeof(AssemblyTriggersRuntimeBenchmarks).Assembly;

        private IServiceProvider _explicit2OptionsProvider;
        private IServiceProvider _assemblyScan7OptionsProvider;
        private IServiceProvider _explicit2ServiceCollectionProvider;
        private IServiceProvider _assemblyScan7ServiceCollectionProvider;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _explicit2OptionsProvider = new ServiceCollection()
                .AddTriggeredDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("RT_Explicit_Options").UseTriggers(o =>
                    {
                        o.AddTrigger<Triggers.SetStudentRegistrationDateTrigger>();
                        o.AddTrigger<Triggers.SignStudentUpForMandatoryCourses>();
                    }))
                .BuildServiceProvider();

            _assemblyScan7OptionsProvider = new ServiceCollection()
                .AddTriggeredDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("RT_Assembly_Options").UseTriggers(o =>
                        o.AddAssemblyTriggers(BenchmarkAssembly)))
                .BuildServiceProvider();

            _explicit2ServiceCollectionProvider = new ServiceCollection()
                .AddTriggeredDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("RT_Explicit_SC"))
                .AddTrigger<Triggers.SetStudentRegistrationDateTrigger>()
                .AddTrigger<Triggers.SignStudentUpForMandatoryCourses>()
                .BuildServiceProvider();

            _assemblyScan7ServiceCollectionProvider = new ServiceCollection()
                .AddTriggeredDbContext<TriggeredApplicationContext>(options =>
                    options.UseInMemoryDatabase("RT_Assembly_SC"))
                .AddAssemblyTriggers(BenchmarkAssembly)
                .BuildServiceProvider();

            SeedCourse(_explicit2OptionsProvider);
            SeedCourse(_assemblyScan7OptionsProvider);
            SeedCourse(_explicit2ServiceCollectionProvider);
            SeedCourse(_assemblyScan7ServiceCollectionProvider);
        }

        private static void SeedCourse(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            using var ctx = scope.ServiceProvider.GetRequiredService<TriggeredApplicationContext>();
            ctx.Database.EnsureCreated();
            ctx.Courses.Add(new Course { Id = Guid.NewGuid(), DisplayName = "Mandatory", IsMandatory = true });
            ctx.SaveChanges();
        }

        /// <summary>
        /// Baseline: 2 Student triggers registered explicitly via UseTriggers options.
        /// </summary>
        [Benchmark(Baseline = true)]
        public void Explicit_2Triggers_ViaOptions()
        {
            using var scope = _explicit2OptionsProvider.CreateScope();
            using var ctx = scope.ServiceProvider.GetRequiredService<TriggeredApplicationContext>();
            ctx.Students.Add(new Student { Id = Guid.NewGuid(), DisplayName = "Test" });
            ctx.SaveChanges();
        }

        /// <summary>
        /// 7 triggers registered via assembly scan (2 Student + 5 StudentCourse) through UseTriggers options.
        /// Only the 2 Student triggers are invoked during this SaveChanges.
        /// </summary>
        [Benchmark]
        public void AssemblyTriggers_7Registered_2Active_ViaOptions()
        {
            using var scope = _assemblyScan7OptionsProvider.CreateScope();
            using var ctx = scope.ServiceProvider.GetRequiredService<TriggeredApplicationContext>();
            ctx.Students.Add(new Student { Id = Guid.NewGuid(), DisplayName = "Test" });
            ctx.SaveChanges();
        }

        /// <summary>
        /// Baseline (application DI path): 2 Student triggers registered explicitly via IServiceCollection.
        /// </summary>
        [Benchmark]
        public void Explicit_2Triggers_ViaServiceCollection()
        {
            using var scope = _explicit2ServiceCollectionProvider.CreateScope();
            using var ctx = scope.ServiceProvider.GetRequiredService<TriggeredApplicationContext>();
            ctx.Students.Add(new Student { Id = Guid.NewGuid(), DisplayName = "Test" });
            ctx.SaveChanges();
        }

        /// <summary>
        /// 7 triggers registered via assembly scan (2 Student + 5 StudentCourse) through IServiceCollection.
        /// Only the 2 Student triggers are invoked during this SaveChanges.
        /// </summary>
        [Benchmark]
        public void AssemblyTriggers_7Registered_2Active_ViaServiceCollection()
        {
            using var scope = _assemblyScan7ServiceCollectionProvider.CreateScope();
            using var ctx = scope.ServiceProvider.GetRequiredService<TriggeredApplicationContext>();
            ctx.Students.Add(new Student { Id = Guid.NewGuid(), DisplayName = "Test" });
            ctx.SaveChanges();
        }
    }
}

