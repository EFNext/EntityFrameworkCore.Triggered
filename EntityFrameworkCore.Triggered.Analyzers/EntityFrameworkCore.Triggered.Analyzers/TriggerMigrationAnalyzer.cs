using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;

        if (!TriggerMigrationMatcher.TryMatch(declaration, context.SemanticModel, context.CancellationToken, out var match))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.EFCT001_OldTriggerSignature,
            declaration.Identifier.GetLocation(),
            match.Method.ContainingType.Name,
            match.TriggerInterface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            match.Entry.AsyncInterfaceShortName));
    }
}
