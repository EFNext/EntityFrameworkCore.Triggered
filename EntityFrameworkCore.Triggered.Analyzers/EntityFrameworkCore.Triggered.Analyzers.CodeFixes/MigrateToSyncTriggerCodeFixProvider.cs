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
public sealed class MigrateToSyncTriggerCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create("EFCT001");

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var properties = diagnostic.Properties;

        if (!properties.TryGetValue("SyncInterfaceShortName", out var syncInterfaceName) ||
            !properties.TryGetValue("SyncMethodName", out var syncMethodName))
            return;

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var methodDeclaration = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Migrate to sync {syncInterfaceName}",
                createChangedDocument: ct => MigrateToSync(context.Document, root, methodDeclaration, ct),
                equivalenceKey: "MigrateToSyncTrigger"),
            diagnostic);
    }

    private static Task<Document> MigrateToSync(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax methodDeclaration,
        CancellationToken cancellationToken)
    {
        var newMethod = methodDeclaration;

        // Change return type from Task to void
        newMethod = newMethod.WithReturnType(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword))
                .WithTriviaFrom(methodDeclaration.ReturnType));

        // Remove CancellationToken parameter
        var parameters = newMethod.ParameterList.Parameters;
        var filteredParameters = parameters.Where(p =>
        {
            var typeName = p.Type?.ToString();
            return typeName != "CancellationToken" && typeName != "System.Threading.CancellationToken";
        }).ToArray();

        newMethod = newMethod.WithParameterList(
            SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(filteredParameters))
                .WithTriviaFrom(newMethod.ParameterList));

        // Remove async modifier if present
        if (newMethod.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            var newModifiers = SyntaxFactory.TokenList(
                newMethod.Modifiers.Where(m => !m.IsKind(SyntaxKind.AsyncKeyword)));
            newMethod = newMethod.WithModifiers(newModifiers);
        }

        // Replace return Task.CompletedTask / return Task.FromResult(...) with plain return
        if (newMethod.Body != null)
        {
            var newBody = RewriteReturnStatements(newMethod.Body);
            newMethod = newMethod.WithBody(newBody);
        }
        else if (newMethod.ExpressionBody != null)
        {
            // Handle expression body: Task.CompletedTask => remove expression body, add empty body
            var exprText = newMethod.ExpressionBody.Expression.ToString();
            if (exprText.EndsWith("Task.CompletedTask") || exprText.Contains("Task.FromResult"))
            {
                newMethod = newMethod
                    .WithExpressionBody(null)
                    .WithSemicolonToken(SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken))
                    .WithBody(SyntaxFactory.Block());
            }
        }

        var newRoot = root.ReplaceNode(methodDeclaration, newMethod);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static BlockSyntax RewriteReturnStatements(BlockSyntax body)
    {
        var rewriter = new ReturnStatementRewriter();
        return (BlockSyntax)rewriter.Visit(body);
    }

    private sealed class ReturnStatementRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (node.Expression == null)
                return base.VisitReturnStatement(node);

            var exprText = node.Expression.ToString();

            // return Task.CompletedTask; / return System.Threading.Tasks.Task.CompletedTask; => return;
            if (exprText.EndsWith("Task.CompletedTask"))
            {
                return SyntaxFactory.ReturnStatement()
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }

            // return Task.FromResult(...); => return;
            if (node.Expression is InvocationExpressionSyntax invocation &&
                invocation.Expression.ToString().EndsWith("Task.FromResult"))
            {
                return SyntaxFactory.ReturnStatement()
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }

            return base.VisitReturnStatement(node);
        }
    }
}
