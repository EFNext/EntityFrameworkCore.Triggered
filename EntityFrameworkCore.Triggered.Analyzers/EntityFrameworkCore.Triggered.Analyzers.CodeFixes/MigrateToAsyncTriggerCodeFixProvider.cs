using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace EntityFrameworkCore.Triggered.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class MigrateToAsyncTriggerCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create("EFCT001");

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var document = context.Document;

        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
            return;

        var methodDeclaration = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null || !TriggerMigrationMatcher.TryMatch(methodDeclaration, semanticModel, context.CancellationToken, out var match))
            return;

        if (methodDeclaration.ExplicitInterfaceSpecifier is { } specifier && GetRightmostIdentifier(specifier.Name) != match.Entry.SyncInterfaceShortName)
            return;

        var baseType = await FindDeclaringBaseTypeAsync(document.Project.Solution, match, context.CancellationToken).ConfigureAwait(false);

        // The interface is inherited from a base class or written through an alias; renaming it here is not safe
        if (baseType is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Migrate to {match.Entry.AsyncInterfaceShortName}",
                createChangedSolution: ct => MigrateToAsyncAsync(document, methodDeclaration, baseType.Value.Document, baseType.Value.Node, match.Entry, ct),
                equivalenceKey: "MigrateToAsyncTrigger"),
            diagnostic);
    }

    private static async Task<(Document Document, BaseTypeSyntax Node)?> FindDeclaringBaseTypeAsync(Solution solution, TriggerMigrationMatch match, CancellationToken cancellationToken)
    {
        foreach (var syntaxReference in match.Method.ContainingType.DeclaringSyntaxReferences)
        {
            if (await syntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is not TypeDeclarationSyntax { BaseList: { } baseList })
                continue;

            var document = solution.GetDocument(baseList.SyntaxTree);
            var semanticModel = document is null ? null : await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
                continue;

            foreach (var baseType in baseList.Types)
            {
                if (SymbolEqualityComparer.Default.Equals(semanticModel.GetTypeInfo(baseType.Type, cancellationToken).Type, match.TriggerInterface)
                    && GetRightmostIdentifier(baseType.Type) == match.Entry.SyncInterfaceShortName)
                {
                    return (document!, baseType);
                }
            }
        }

        return null;
    }

    private static async Task<Solution> MigrateToAsyncAsync(
        Document methodDocument,
        MethodDeclarationSyntax methodDeclaration,
        Document baseTypeDocument,
        BaseTypeSyntax baseType,
        TriggerMappingEntry entry,
        CancellationToken cancellationToken)
    {
        var editor = new SolutionEditor(methodDocument.Project.Solution);

        var baseTypeEditor = await editor.GetDocumentEditorAsync(baseTypeDocument.Id, cancellationToken).ConfigureAwait(false);
        baseTypeEditor.ReplaceNode(baseType.Type, RenameRightmostIdentifier(baseType.Type, entry.AsyncInterfaceShortName));

        var newMethod = methodDeclaration.WithIdentifier(
            SyntaxFactory.Identifier(entry.AsyncMethodName).WithTriviaFrom(methodDeclaration.Identifier));

        if (newMethod.ExplicitInterfaceSpecifier is { } specifier)
        {
            newMethod = newMethod.WithExplicitInterfaceSpecifier(
                specifier.WithName((NameSyntax)RenameRightmostIdentifier(specifier.Name, entry.AsyncInterfaceShortName)));
        }

        var methodEditor = await editor.GetDocumentEditorAsync(methodDocument.Id, cancellationToken).ConfigureAwait(false);
        methodEditor.ReplaceNode(methodDeclaration, newMethod);

        return editor.GetChangedSolution();
    }

    private static string? GetRightmostIdentifier(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
        _ => null
    };

    private static TypeSyntax RenameRightmostIdentifier(TypeSyntax type, string newName) => type switch
    {
        SimpleNameSyntax simple => RenameSimpleName(simple, newName),
        QualifiedNameSyntax qualified => qualified.WithRight(RenameSimpleName(qualified.Right, newName)),
        AliasQualifiedNameSyntax aliasQualified => aliasQualified.WithName(RenameSimpleName(aliasQualified.Name, newName)),
        _ => type
    };

    private static SimpleNameSyntax RenameSimpleName(SimpleNameSyntax name, string newName)
        => name.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(name.Identifier));
}
