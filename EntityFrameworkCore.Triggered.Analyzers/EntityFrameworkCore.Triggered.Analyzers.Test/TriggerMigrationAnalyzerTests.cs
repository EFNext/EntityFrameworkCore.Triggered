using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace EntityFrameworkCore.Triggered.Analyzers.Test;

public class TriggerMigrationAnalyzerTests
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

    private static CSharpAnalyzerTest<TriggerMigrationAnalyzer, DefaultVerifier> CreateTest(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TriggerMigrationAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        return test;
    }

    [Fact]
    public async Task NoDiagnostic_WhenCorrectSyncSignature()
    {
        var test = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IBeforeSaveTrigger<Student>
{
    public void BeforeSave(EntityFrameworkCore.Triggered.ITriggerContext<Student> context)
    {
    }
}
";

        await CreateTest(test).RunAsync();
    }

    [Fact]
    public async Task NoDiagnostic_WhenCorrectAsyncInterface()
    {
        var test = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IBeforeSaveAsyncTrigger<Student>
{
    public System.Threading.Tasks.Task BeforeSaveAsync(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        await CreateTest(test).RunAsync();
    }

    [Fact]
    public async Task Diagnostic_WhenOldAsyncSignatureOnSyncInterface()
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

        await CreateTest(test,
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyTrigger", "IBeforeSaveTrigger<Student>", "IBeforeSaveAsyncTrigger"))
            .RunAsync();
    }

    [Fact]
    public async Task Diagnostic_WhenOldAsyncSignatureOnAfterSaveTrigger()
    {
        var test = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IAfterSaveTrigger<Student>
{
    public System.Threading.Tasks.Task {|#0:AfterSave|}(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        await CreateTest(test,
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyTrigger", "IAfterSaveTrigger<Student>", "IAfterSaveAsyncTrigger"))
            .RunAsync();
    }

    [Fact]
    public async Task Diagnostic_WhenMultipleInterfacesWithOldSignature()
    {
        var test = TriggerInterfaceStubs + @"
class MyTrigger : EntityFrameworkCore.Triggered.IBeforeSaveTrigger<Student>, EntityFrameworkCore.Triggered.IAfterSaveTrigger<Student>
{
    public System.Threading.Tasks.Task {|#0:BeforeSave|}(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task {|#1:AfterSave|}(EntityFrameworkCore.Triggered.ITriggerContext<Student> context, System.Threading.CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
";

        await CreateTest(test,
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyTrigger", "IBeforeSaveTrigger<Student>", "IBeforeSaveAsyncTrigger"),
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(1)
                .WithArguments("MyTrigger", "IAfterSaveTrigger<Student>", "IAfterSaveAsyncTrigger"))
            .RunAsync();
    }

    [Fact]
    public async Task Diagnostic_WhenLifecycleTriggerWithOldSignature()
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

        await CreateTest(test,
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyTrigger", "IBeforeSaveStartingTrigger", "IBeforeSaveStartingAsyncTrigger"))
            .RunAsync();
    }

    [Fact]
    public async Task Diagnostic_WhenAfterSaveFailedWithOldSignature()
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

        await CreateTest(test,
            new DiagnosticResult("EFCT001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MyTrigger", "IAfterSaveFailedTrigger<Student>", "IAfterSaveFailedAsyncTrigger"))
            .RunAsync();
    }
}
