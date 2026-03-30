using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace EntityFrameworkCore.Triggered.Analyzers.Test;

public class MigrateToAsyncCodeFixTests
{
    private const string TriggerInterfaceStubs = @"
namespace EntityFrameworkCore.Triggered
{
    public enum ChangeType { Added, Modified, Deleted }

    public interface ITriggerContext<out TEntity>
    {
        ChangeType ChangeType { get; }
        TEntity Entity { get; }
    }

    public interface IBeforeSaveTrigger<in TEntity>
    {
        void BeforeSave(ITriggerContext<TEntity> context);
    }

    public interface IBeforeSaveAsyncTrigger<in TEntity>
    {
        System.Threading.Tasks.Task BeforeSaveAsync(ITriggerContext<TEntity> context, System.Threading.CancellationToken cancellationToken);
    }

    public interface IAfterSaveTrigger<TEntity>
    {
        void AfterSave(ITriggerContext<TEntity> context);
    }

    public interface IAfterSaveAsyncTrigger<TEntity>
    {
        System.Threading.Tasks.Task AfterSaveAsync(ITriggerContext<TEntity> context, System.Threading.CancellationToken cancellationToken);
    }

    public interface IAfterSaveFailedTrigger<TEntity>
    {
        void AfterSaveFailed(ITriggerContext<TEntity> context, System.Exception exception);
    }

    public interface IAfterSaveFailedAsyncTrigger<TEntity>
    {
        System.Threading.Tasks.Task AfterSaveFailedAsync(ITriggerContext<TEntity> context, System.Exception exception, System.Threading.CancellationToken cancellationToken);
    }
}

namespace EntityFrameworkCore.Triggered.Lifecycles
{
    public interface IBeforeSaveStartingTrigger
    {
        void BeforeSaveStarting();
    }

    public interface IBeforeSaveStartingAsyncTrigger
    {
        System.Threading.Tasks.Task BeforeSaveStartingAsync(System.Threading.CancellationToken cancellationToken);
    }
}

public class Student { }
";

    private static CSharpCodeFixTest<TriggerMigrationAnalyzer, CodeFixes.MigrateToAsyncTriggerCodeFixProvider, DefaultVerifier> CreateTest(
        string testCode, string fixedCode, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<TriggerMigrationAnalyzer, CodeFixes.MigrateToAsyncTriggerCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            CompilerDiagnostics = CompilerDiagnostics.None,
            CodeFixTestBehaviors = CodeFixTestBehaviors.FixOne,
            CodeActionEquivalenceKey = "MigrateToAsyncTrigger",
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    [Fact]
    public async Task CodeFix_MigratesToAsyncInterface()
    {
        var test = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IBeforeSaveTrigger<Student>
{
    public System.Threading.Tasks.Task {|#0:BeforeSave|}(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        var fixedCode = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IBeforeSaveAsyncTrigger<Student>
{
    public System.Threading.Tasks.Task BeforeSaveAsync(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        await CreateTest(test, fixedCode,
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyTrigger", "IBeforeSaveTrigger<Student>", "IBeforeSaveAsyncTrigger"))
            .RunAsync();
    }

    [Fact]
    public async Task CodeFix_MigratesLifecycleTriggerToAsync()
    {
        var test = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.Lifecycles.IBeforeSaveStartingTrigger
{
    public System.Threading.Tasks.Task {|#0:BeforeSaveStarting|}(System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        var fixedCode = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.Lifecycles.IBeforeSaveStartingAsyncTrigger
{
    public System.Threading.Tasks.Task BeforeSaveStartingAsync(System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        await CreateTest(test, fixedCode,
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyTrigger", "IBeforeSaveStartingTrigger", "IBeforeSaveStartingAsyncTrigger"))
            .RunAsync();
    }

    [Fact]
    public async Task CodeFix_MigratesAfterSaveFailedToAsync()
    {
        var test = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IAfterSaveFailedTrigger<Student>
{
    public System.Threading.Tasks.Task {|#0:AfterSaveFailed|}(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Exception exception, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        var fixedCode = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IAfterSaveFailedAsyncTrigger<Student>
{
    public System.Threading.Tasks.Task AfterSaveFailedAsync(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Exception exception, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        await CreateTest(test, fixedCode,
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyTrigger", "IAfterSaveFailedTrigger<Student>", "IAfterSaveFailedAsyncTrigger"))
            .RunAsync();
    }
}
