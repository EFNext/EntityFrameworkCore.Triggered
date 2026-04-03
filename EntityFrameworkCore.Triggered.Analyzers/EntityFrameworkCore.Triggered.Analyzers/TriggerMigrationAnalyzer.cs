using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EntityFrameworkCore.Triggered.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TriggerMigrationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.EFCT001_OldTriggerSignature);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;

        if (namedType.TypeKind != TypeKind.Class)
            return;

        foreach (var iface in namedType.AllInterfaces)
        {
            var metadataName = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

            // Normalize generic display: IBeforeSaveTrigger<T> -> use metadata name with arity
            var originalDef = iface.OriginalDefinition;
            var qualifiedName = originalDef.ContainingNamespace + "." + originalDef.MetadataName;

            foreach (var entry in TriggerMapping.Entries)
            {
                if (qualifiedName != entry.SyncInterfaceMetadataName)
                    continue;

                // Found a sync trigger interface — check if the class has the old async method signature
                var method = namedType.GetMembers()
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m =>
                        m.Name == entry.SyncMethodName &&
                        IsTaskReturnType(m.ReturnType) &&
                        m.Parameters.Any(p => p.Type.ToDisplayString() == "System.Threading.CancellationToken"));

                if (method == null)
                    continue;

                var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add("SyncInterfaceShortName", entry.SyncInterfaceShortName);
                properties.Add("AsyncInterfaceShortName", entry.AsyncInterfaceShortName);
                properties.Add("SyncMethodName", entry.SyncMethodName);
                properties.Add("AsyncMethodName", entry.AsyncMethodName);

                var interfaceDisplayName = iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.EFCT001_OldTriggerSignature,
                    method.Locations.FirstOrDefault() ?? namedType.Locations.FirstOrDefault(),
                    properties.ToImmutable(),
                    namedType.Name,
                    interfaceDisplayName,
                    entry.AsyncInterfaceShortName);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsTaskReturnType(ITypeSymbol type)
    {
        return type.ToDisplayString() == "System.Threading.Tasks.Task" ||
               (type.OriginalDefinition.ToDisplayString() == "System.Threading.Tasks.Task<TResult>");
    }
}
