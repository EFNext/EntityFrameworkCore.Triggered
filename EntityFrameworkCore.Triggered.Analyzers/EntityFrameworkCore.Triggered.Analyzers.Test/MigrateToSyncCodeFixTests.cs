using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace EntityFrameworkCore.Triggered.Analyzers.Test;

public class MigrateToSyncCodeFixTests
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

    private static CSharpCodeFixTest<TriggerMigrationAnalyzer, CodeFixes.MigrateToSyncTriggerCodeFixProvider, DefaultVerifier> CreateTest(
        string testCode, string fixedCode, params DiagnosticResult[] expected)
    {
        var test = new CSharpCodeFixTest<TriggerMigrationAnalyzer, CodeFixes.MigrateToSyncTriggerCodeFixProvider, DefaultVerifier>
        {
            TestCode = testCode,
            FixedCode = fixedCode,
            CompilerDiagnostics = CompilerDiagnostics.None,
            CodeFixTestBehaviors = CodeFixTestBehaviors.FixOne,
            CodeActionEquivalenceKey = "MigrateToSyncTrigger",
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    [Fact]
    public async Task CodeFix_ConvertsToSyncSignature()
    {
        var test = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IBeforeSaveTrigger<Student>
{
    public System.Threading.Tasks.Task {|#0:BeforeSave|}(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Threading.CancellationToken cancellationToken)
    {
        context.Entity.ToString();
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        var fixedCode = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IBeforeSaveTrigger<Student>
{
    public void BeforeSave(EntityFrameworkCore.Triggered.ITriggerContext<Student> context)
    {
        context.Entity.ToString();
        return;
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
    public async Task CodeFix_ConvertsLifecycleTriggerToSync()
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
class MyTrigger : EntityFrameworkCore.Triggered.Lifecycles.IBeforeSaveStartingTrigger
{
    public void BeforeSaveStarting()
    {
        return;
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
    public async Task CodeFix_RemovesAsyncModifier()
    {
        var test = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IBeforeSaveTrigger<Student>
{
    public async System.Threading.Tasks.Task {|#0:BeforeSave|}(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Threading.CancellationToken cancellationToken)
    {
        context.Entity.ToString();
    }
}
";

        var fixedCode = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IBeforeSaveTrigger<Student>
{
    public void BeforeSave(EntityFrameworkCore.Triggered.ITriggerContext<Student> context)
    {
        context.Entity.ToString();
    }
}
";

        await CreateTest(test, fixedCode,
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyTrigger", "IBeforeSaveTrigger<Student>", "IBeforeSaveAsyncTrigger"))
            .RunAsync();
    }
}
