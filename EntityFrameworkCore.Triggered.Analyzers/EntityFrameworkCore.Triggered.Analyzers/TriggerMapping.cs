using System.Collections.Immutable;

namespace EntityFrameworkCore.Triggered.Analyzers;

internal readonly struct TriggerMappingEntry
{
    public string SyncInterfaceMetadataName { get; }
    public string AsyncInterfaceMetadataName { get; }
    public string SyncMethodName { get; }
    public string AsyncMethodName { get; }
    public string SyncInterfaceShortName { get; }
    public string AsyncInterfaceShortName { get; }

    public TriggerMappingEntry(
        string syncInterfaceMetadataName,
        string asyncInterfaceMetadataName,
        string syncMethodName,
        string asyncMethodName,
        string syncInterfaceShortName,
        string asyncInterfaceShortName)
    {
        SyncInterfaceMetadataName = syncInterfaceMetadataName;
        AsyncInterfaceMetadataName = asyncInterfaceMetadataName;
        SyncMethodName = syncMethodName;
        AsyncMethodName = asyncMethodName;
        SyncInterfaceShortName = syncInterfaceShortName;
        AsyncInterfaceShortName = asyncInterfaceShortName;
    }
}

internal static class TriggerMapping
{
    public static readonly ImmutableArray<TriggerMappingEntry> Entries = ImmutableArray.Create(
        // Save triggers (generic)
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.IBeforeSaveTrigger`1",
            "EntityFrameworkCore.Triggered.IBeforeSaveAsyncTrigger`1",
            "BeforeSave", "BeforeSaveAsync",
            "IBeforeSaveTrigger", "IBeforeSaveAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.IAfterSaveTrigger`1",
            "EntityFrameworkCore.Triggered.IAfterSaveAsyncTrigger`1",
            "AfterSave", "AfterSaveAsync",
            "IAfterSaveTrigger", "IAfterSaveAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.IAfterSaveFailedTrigger`1",
            "EntityFrameworkCore.Triggered.IAfterSaveFailedAsyncTrigger`1",
            "AfterSaveFailed", "AfterSaveFailedAsync",
            "IAfterSaveFailedTrigger", "IAfterSaveFailedAsyncTrigger"),

        // Transaction triggers (generic)
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Transactions.IBeforeCommitTrigger`1",
            "EntityFrameworkCore.Triggered.Transactions.IBeforeCommitAsyncTrigger`1",
            "BeforeCommit", "BeforeCommitAsync",
            "IBeforeCommitTrigger", "IBeforeCommitAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Transactions.IAfterCommitTrigger`1",
            "EntityFrameworkCore.Triggered.Transactions.IAfterCommitAsyncTrigger`1",
            "AfterCommit", "AfterCommitAsync",
            "IAfterCommitTrigger", "IAfterCommitAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Transactions.IBeforeRollbackTrigger`1",
            "EntityFrameworkCore.Triggered.Transactions.IBeforeRollbackAsyncTrigger`1",
            "BeforeRollback", "BeforeRollbackAsync",
            "IBeforeRollbackTrigger", "IBeforeRollbackAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Transactions.IAfterRollbackTrigger`1",
            "EntityFrameworkCore.Triggered.Transactions.IAfterRollbackAsyncTrigger`1",
            "AfterRollback", "AfterRollbackAsync",
            "IAfterRollbackTrigger", "IAfterRollbackAsyncTrigger"),

        // Lifecycle triggers — save (non-generic)
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Lifecycles.IBeforeSaveStartingTrigger",
            "EntityFrameworkCore.Triggered.Lifecycles.IBeforeSaveStartingAsyncTrigger",
            "BeforeSaveStarting", "BeforeSaveStartingAsync",
            "IBeforeSaveStartingTrigger", "IBeforeSaveStartingAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Lifecycles.IBeforeSaveCompletedTrigger",
            "EntityFrameworkCore.Triggered.Lifecycles.IBeforeSaveCompletedAsyncTrigger",
            "BeforeSaveCompleted", "BeforeSaveCompletedAsync",
            "IBeforeSaveCompletedTrigger", "IBeforeSaveCompletedAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Lifecycles.IAfterSaveStartingTrigger",
            "EntityFrameworkCore.Triggered.Lifecycles.IAfterSaveStartingAsyncTrigger",
            "AfterSaveStarting", "AfterSaveStartingAsync",
            "IAfterSaveStartingTrigger", "IAfterSaveStartingAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Lifecycles.IAfterSaveCompletedTrigger",
            "EntityFrameworkCore.Triggered.Lifecycles.IAfterSaveCompletedAsyncTrigger",
            "AfterSaveCompleted", "AfterSaveCompletedAsync",
            "IAfterSaveCompletedTrigger", "IAfterSaveCompletedAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Lifecycles.IAfterSaveFailedStartingTrigger",
            "EntityFrameworkCore.Triggered.Lifecycles.IAfterSaveFailedStartingAsyncTrigger",
            "AfterSaveFailedStarting", "AfterSaveFailedStartingAsync",
            "IAfterSaveFailedStartingTrigger", "IAfterSaveFailedStartingAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Lifecycles.IAfterSaveFailedCompletedTrigger",
            "EntityFrameworkCore.Triggered.Lifecycles.IAfterSaveFailedCompletedAsyncTrigger",
            "AfterSaveFailedCompleted", "AfterSaveFailedCompletedAsync",
            "IAfterSaveFailedCompletedTrigger", "IAfterSaveFailedCompletedAsyncTrigger"),

        // Lifecycle triggers — transaction (non-generic)
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Transactions.Lifecycles.IBeforeCommitStartingTrigger",
            "EntityFrameworkCore.Triggered.Transactions.Lifecycles.IBeforeCommitStartingAsyncTrigger",
            "BeforeCommitStarting", "BeforeCommitStartingAsync",
            "IBeforeCommitStartingTrigger", "IBeforeCommitStartingAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Transactions.Lifecycles.IBeforeCommitCompletedTrigger",
            "EntityFrameworkCore.Triggered.Transactions.Lifecycles.IBeforeCommitCompletedAsyncTrigger",
            "BeforeCommitCompleted", "BeforeCommitCompletedAsync",
            "IBeforeCommitCompletedTrigger", "IBeforeCommitCompletedAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Transactions.Lifecycles.IAfterCommitStartingTrigger",
            "EntityFrameworkCore.Triggered.Transactions.Lifecycles.IAfterCommitStartingAsyncTrigger",
            "AfterCommitStarting", "AfterCommitStartingAsync",
            "IAfterCommitStartingTrigger", "IAfterCommitStartingAsyncTrigger"),
        new TriggerMappingEntry(
            "EntityFrameworkCore.Triggered.Transactions.Lifecycles.IAfterCommitCompletedTrigger",
            "EntityFrameworkCore.Triggered.Transactions.Lifecycles.IAfterCommitCompletedAsyncTrigger",
            "AfterCommitCompleted", "AfterCommitCompletedAsync",
            "IAfterCommitCompletedTrigger", "IAfterCommitCompletedAsyncTrigger")
    );
}
