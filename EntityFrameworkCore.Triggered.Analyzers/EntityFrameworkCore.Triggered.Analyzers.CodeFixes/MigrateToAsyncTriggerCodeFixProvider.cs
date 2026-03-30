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
        var properties = diagnostic.Properties;

        if (!properties.TryGetValue("AsyncInterfaceShortName", out var asyncInterfaceName) ||
            !properties.TryGetValue("SyncInterfaceShortName", out var syncInterfaceName) ||
            !properties.TryGetValue("SyncMethodName", out var syncMethodName) ||
            !properties.TryGetValue("AsyncMethodName", out var asyncMethodName))
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var methodDeclaration = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration == null) return;

        var classDeclaration = methodDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Migrate to {asyncInterfaceName}",
                createChangedDocument: ct => MigrateToAsync(context.Document, root, classDeclaration, methodDeclaration, syncInterfaceName!, asyncInterfaceName!, syncMethodName!, asyncMethodName!, ct),
                equivalenceKey: "MigrateToAsyncTrigger"),
            diagnostic);
    }

    private static Task<Document> MigrateToAsync(
        Document document,
        SyntaxNode root,
        ClassDeclarationSyntax classDeclaration,
        MethodDeclarationSyntax methodDeclaration,
        string syncInterfaceName,
        string asyncInterfaceName,
        string syncMethodName,
        string asyncMethodName,
        CancellationToken cancellationToken)
    {
        var newRoot = root;

        // Replace interface name in base list
        if (classDeclaration.BaseList != null)
        {
            var newBaseList = classDeclaration.BaseList;
            foreach (var baseType in classDeclaration.BaseList.Types)
            {
                var typeName = GetBaseTypeName(baseType);
                if (typeName == syncInterfaceName)
                {
                    var newBaseType = ReplaceInterfaceName(baseType, syncInterfaceName, asyncInterfaceName);
                    newBaseList = newBaseList.ReplaceNode(baseType, newBaseType);
                }
            }

            if (newBaseList != classDeclaration.BaseList)
            {
                newRoot = newRoot.ReplaceNode(classDeclaration.BaseList, newBaseList);
            }
        }

        // Find the method again in the updated tree
        var updatedMethod = newRoot.FindNode(methodDeclaration.Identifier.Span)
            .FirstAncestorOrSelf<MethodDeclarationSyntax>();

        if (updatedMethod != null && updatedMethod.Identifier.Text == syncMethodName)
        {
            var newMethod = updatedMethod.WithIdentifier(
                SyntaxFactory.Identifier(asyncMethodName)
                    .WithTriviaFrom(updatedMethod.Identifier));
            newRoot = newRoot.ReplaceNode(updatedMethod, newMethod);
        }

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static string? GetBaseTypeName(BaseTypeSyntax baseType)
    {
        return baseType.Type switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => null
        };
    }

    private static BaseTypeSyntax ReplaceInterfaceName(BaseTypeSyntax baseType, string oldName, string newName)
    {
        switch (baseType.Type)
        {
            case GenericNameSyntax generic when generic.Identifier.Text == oldName:
                var newGeneric = generic.WithIdentifier(
                    SyntaxFactory.Identifier(newName).WithTriviaFrom(generic.Identifier));
                return baseType.WithType(newGeneric);

            case SimpleNameSyntax simple when simple.Identifier.Text == oldName:
                var newSimple = SyntaxFactory.IdentifierName(
                    SyntaxFactory.Identifier(newName).WithTriviaFrom(simple.Identifier));
                return baseType.WithType(newSimple);

            case QualifiedNameSyntax qualified when qualified.Right.Identifier.Text == oldName:
                SimpleNameSyntax newRight;
                if (qualified.Right is GenericNameSyntax rightGeneric)
                {
                    newRight = rightGeneric.WithIdentifier(
                        SyntaxFactory.Identifier(newName).WithTriviaFrom(rightGeneric.Identifier));
                }
                else
                {
                    newRight = SyntaxFactory.IdentifierName(
                        SyntaxFactory.Identifier(newName).WithTriviaFrom(qualified.Right.Identifier));
                }
                return baseType.WithType(qualified.WithRight(newRight));

            default:
                return baseType;
        }
    }
}
