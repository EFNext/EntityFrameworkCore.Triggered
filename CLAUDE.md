# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

EntityFrameworkCore.Triggered is a library that adds trigger support (before/after save hooks) to Entity Framework Core. Triggers react to entity changes during `SaveChanges` via `IBeforeSaveTrigger<T>`, `IAfterSaveTrigger<T>`, and related interfaces.

## Build & Test Commands

```bash
# Build (default is V3 / .NET 6.0)
dotnet build EntityFrameworkCore.Triggered.sln

# Run all tests
dotnet test EntityFrameworkCore.Triggered.sln

# Run a single test project
dotnet test test/EntityFrameworkCore.Triggered.Tests

# Run a specific test
dotnet test test/EntityFrameworkCore.Triggered.Tests --filter "FullyQualifiedName~TriggerSessionTests.RaiseBeforeSaveTriggers"

# Build for V2 (.NET Standard 2.1)
dotnet build EntityFrameworkCore.Triggered.sln -c ReleaseV2

# Build for V1 (.NET Standard 2.0)
dotnet build EntityFrameworkCore.Triggered.sln -c ReleaseV1

# Pack NuGet packages
dotnet pack EntityFrameworkCore.Triggered.sln -c Release
```

## Architecture

### Project Dependency Graph

```
Abstractions  <──  Core (EntityFrameworkCore.Triggered)  <──  Extensions
                                                          <──  Transactions
Transactions.Abstractions  <──  Transactions
```

- **Abstractions**: Pure interfaces (`IBeforeSaveTrigger<T>`, `IAfterSaveTrigger<T>`, `ITriggerContext<T>`, `ITriggerSession`, `ChangeType`, lifecycle triggers). No EF Core dependency.
- **Core**: Main implementation — `TriggerSession` (orchestrator, ~400 lines), `TriggerService`, `TriggerContext`, trigger discovery, cascade strategies, and EF Core integration via `UseTriggers()`.
- **Extensions**: Assembly scanning for trigger discovery (`AddAssemblyTriggers`).
- **Transactions**: Transaction-scoped triggers (`IBeforeCommitTrigger`, `IAfterCommitTrigger`).

### Key Internal Components

- **TriggerSession** (`src/EntityFrameworkCore.Triggered/TriggerSession.cs`): Core orchestrator that coordinates trigger discovery, invocation, and cascading during SaveChanges.
- **TriggerContextTracker** (`Internal/`): Wraps EF Core's ChangeTracker to discover pending entity changes.
- **TriggerDiscoveryService** (`Internal/`): Resolves registered triggers for entity types, supporting type inheritance and priority ordering.
- **Descriptor classes** (`Internal/Descriptors/`): Handle invocation of specific trigger types (BeforeSave, AfterSave, etc.) via reflection.
- **CascadeStrategies** (`Internal/CascadeStrategies/`): Control re-discovery of changes after triggers modify entities. Default is `EntityAndTypeCascadeStrategy`.

### Registration Entry Points

- `UseTriggers()` on `DbContextOptionsBuilder` — core configuration
- `AddTriggeredDbContext<T>()` / `AddTriggeredDbContextPool<T>()` / `AddTriggeredDbContextFactory<T>()` — service collection helpers in `Extensions/ServiceCollectionExtensions.cs`

## Multi-Version Strategy

The project supports multiple EF Core versions via build configurations and conditional compilation:

| Config | Target | Preprocessor Symbol | EF Core |
|--------|--------|---------------------|---------|
| Release (V3) | net6.0 | `EFCORETRIGGERED3` | 6.0.0 |
| ReleaseV2 | netstandard2.1 | `EFCORETRIGGERED2` | 3.1.x |
| ReleaseV1 | netstandard2.0 | `EFCORETRIGGERED1` | 3.1.x |

Version-specific code uses `#if EFCORETRIGGERED3` guards. The current branch (`v3`) targets V3.

## Build Settings

- **TreatWarningsAsErrors**: enabled globally (`Directory.Build.props`)
- **Nullable reference types**: enabled
- **LangVersion**: 9.0
- **Strong naming**: enabled (signed with `EntityFrameworkCore.Triggered.snk`)

## Test Projects

- `EntityFrameworkCore.Triggered.Tests` — unit tests (xUnit), uses EF Core InMemory provider
- `EntityFrameworkCore.Triggered.IntegrationTests` — full SaveChanges lifecycle tests
- `EntityFrameworkCore.Triggered.Extensions.Tests` — assembly trigger discovery
- `EntityFrameworkCore.Triggered.Transactions.Tests` — transaction trigger behavior
- `EntityFrameworkCore.Triggered.Analyzers.Tests` — Roslyn analyzer tests
