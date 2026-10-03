using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Triggered.Analyzers;

internal readonly struct TriggerMigrationMatch
{
    public TriggerMigrationMatch(IMethodSymbol method, INamedTypeSymbol triggerInterface, TriggerMappingEntry entry)
    {
        Method = method;
        TriggerInterface = triggerInterface;
        Entry = entry;
    }

    public IMethodSymbol Method { get; }
    public INamedTypeSymbol TriggerInterface { get; }
    public TriggerMappingEntry Entry { get; }
}

internal static class TriggerMigrationMatcher
{
    /// <summary>
    /// Matches a method still using the v3 signature (<c>Task Method(..., CancellationToken)</c>) against the sync trigger interface it was meant to implement.
    /// </summary>
    public static bool TryMatch(MethodDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken, out TriggerMigrationMatch match)
    {
        match = default;

        if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not IMethodSymbol method)
            return false;

        if (method.ContainingType is not { TypeKind: TypeKind.Class } containingType)
            return false;

        if (!IsNonGenericTask(method.ReturnType) || method.Parameters.Length == 0 || !IsCancellationToken(method.Parameters[method.Parameters.Length - 1].Type))
            return false;

        INamedTypeSymbol? explicitInterface = null;
        if (declaration.ExplicitInterfaceSpecifier is { } specifier)
        {
            explicitInterface = semanticModel.GetTypeInfo(specifier.Name, cancellationToken).Type as INamedTypeSymbol;
            if (explicitInterface is null)
                return false;
        }

        var methodName = declaration.Identifier.ValueText;

        foreach (var triggerInterface in containingType.AllInterfaces)
        {
            if (explicitInterface is not null && !SymbolEqualityComparer.Default.Equals(triggerInterface, explicitInterface))
                continue;

            if (!TriggerMapping.TryGetBySyncInterface(triggerInterface, out var entry) || entry.SyncMethodName != methodName)
                continue;

            var syncMethod = triggerInterface.GetMembers(entry.SyncMethodName).OfType<IMethodSymbol>().FirstOrDefault();
            if (syncMethod is null || !ParametersMatchIgnoringCancellationToken(syncMethod, method))
                continue;

            match = new TriggerMigrationMatch(method, triggerInterface, entry);
            return true;
        }

        return false;
    }

    public static bool IsCancellationToken(ITypeSymbol type)
        => type is { Name: "CancellationToken", ContainingNamespace: { Name: "Threading", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } };

    private static bool IsNonGenericTask(ITypeSymbol type)
        => type is INamedTypeSymbol { Name: "Task", IsGenericType: false, ContainingNamespace: { Name: "Tasks", ContainingNamespace: { Name: "Threading", ContainingNamespace: { Name: "System" } } } };

    private static bool ParametersMatchIgnoringCancellationToken(IMethodSymbol syncMethod, IMethodSymbol method)
    {
        if (syncMethod.Parameters.Length != method.Parameters.Length - 1)
            return false;

        for (var i = 0; i < syncMethod.Parameters.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(syncMethod.Parameters[i].Type, method.Parameters[i].Type))
                return false;
        }

        return true;
    }
}
