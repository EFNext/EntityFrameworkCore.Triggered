using System.Collections.Generic;
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

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
            return;

        var methodDeclaration = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null || !TriggerMigrationMatcher.TryMatch(methodDeclaration, semanticModel, context.CancellationToken, out var match))
            return;

        var completedTaskReturns = new List<ReturnStatementSyntax>();
        if (!CanConvertToSync(methodDeclaration, match.Method, semanticModel, completedTaskReturns, context.CancellationToken))
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Convert to sync {match.Entry.SyncInterfaceShortName}",
                createChangedDocument: _ => Task.FromResult(context.Document.WithSyntaxRoot(
                    root.ReplaceNode(methodDeclaration, ConvertToSync(methodDeclaration, completedTaskReturns, semanticModel)))),
                equivalenceKey: "MigrateToSyncTrigger"),
            diagnostic);
    }

    /// <summary>
    /// A method can only be converted when dropping the <c>Task</c> and the <c>CancellationToken</c> cannot change its behavior:
    /// it doesn't await anything itself, never uses the token, and only ever returns an already-completed task.
    /// </summary>
    private static bool CanConvertToSync(MethodDeclarationSyntax declaration, IMethodSymbol method, SemanticModel semanticModel, List<ReturnStatementSyntax> completedTaskReturns, CancellationToken cancellationToken)
    {
        SyntaxNode? body = (SyntaxNode?)declaration.Body ?? declaration.ExpressionBody;
        if (body is null)
            return false;

        var cancellationTokenParameter = method.Parameters[method.Parameters.Length - 1];
        var usesCancellationToken = body.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => identifier.Identifier.ValueText == cancellationTokenParameter.Name
                && SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol, cancellationTokenParameter));

        if (usesCancellationToken)
            return false;

        var ownNodes = body.DescendantNodesAndSelf(node => node == body || !IsNestedFunction(node)).ToList();

        if (ownNodes.Any(IsAwait))
            return false;

        if (method.IsAsync)
            return true;

        if (declaration.ExpressionBody is { } expressionBody)
            return expressionBody.Expression is ThrowExpressionSyntax || IsCompletedTask(expressionBody.Expression, semanticModel, cancellationToken);

        foreach (var returnStatement in ownNodes.OfType<ReturnStatementSyntax>())
        {
            if (returnStatement.Expression is null || !IsCompletedTask(returnStatement.Expression, semanticModel, cancellationToken))
                return false;

            completedTaskReturns.Add(returnStatement);
        }

        return true;
    }

    private static MethodDeclarationSyntax ConvertToSync(MethodDeclarationSyntax declaration, IReadOnlyCollection<ReturnStatementSyntax> completedTaskReturns, SemanticModel semanticModel)
    {
        var newMethod = declaration.ReplaceNodes(
            completedTaskReturns,
            (original, _) => SyntaxFactory.ReturnStatement().WithTriviaFrom(original));

        newMethod = newMethod
            .WithReturnType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)).WithTriviaFrom(declaration.ReturnType))
            .WithParameterList(newMethod.ParameterList.WithParameters(newMethod.ParameterList.Parameters.RemoveAt(newMethod.ParameterList.Parameters.Count - 1)));

        var asyncModifier = newMethod.Modifiers.FirstOrDefault(modifier => modifier.IsKind(SyntaxKind.AsyncKeyword));
        if (asyncModifier != default)
        {
            newMethod = newMethod.WithModifiers(newMethod.Modifiers.Remove(asyncModifier));
            if (newMethod.Modifiers.Count == 0)
                newMethod = newMethod.WithReturnType(newMethod.ReturnType.WithLeadingTrivia(asyncModifier.LeadingTrivia));
        }

        if (declaration.ExpressionBody is { } expressionBody && IsCompletedTask(expressionBody.Expression, semanticModel, CancellationToken.None))
        {
            var endOfLine = declaration.DescendantTrivia().FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));
            if (endOfLine == default)
                endOfLine = SyntaxFactory.ElasticCarriageReturnLineFeed;

            var indentation = declaration.GetLeadingTrivia().LastOrDefault(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia));
            var braceLeadingTrivia = indentation == default ? SyntaxFactory.TriviaList() : SyntaxFactory.TriviaList(indentation);

            var block = SyntaxFactory.Block(
                SyntaxFactory.Token(braceLeadingTrivia, SyntaxKind.OpenBraceToken, SyntaxFactory.TriviaList(endOfLine)),
                default,
                SyntaxFactory.Token(braceLeadingTrivia, SyntaxKind.CloseBraceToken, newMethod.SemicolonToken.TrailingTrivia));

            newMethod = newMethod
                .WithParameterList(newMethod.ParameterList.WithTrailingTrivia(endOfLine))
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithBody(block);
        }

        if (newMethod.Body is { Statements: { Count: > 0 } statements } body && statements.Last() is ReturnStatementSyntax { Expression: null } trailingReturn)
        {
            newMethod = newMethod.WithBody(body.WithStatements(statements.Remove(trailingReturn)));
        }

        return newMethod;
    }

    private static bool IsNestedFunction(SyntaxNode node)
        => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax;

    private static bool IsAwait(SyntaxNode node) => node switch
    {
        AwaitExpressionSyntax => true,
        CommonForEachStatementSyntax forEach => forEach.AwaitKeyword != default,
        UsingStatementSyntax usingStatement => usingStatement.AwaitKeyword != default,
        LocalDeclarationStatementSyntax localDeclaration => localDeclaration.AwaitKeyword != default,
        _ => false
    };

    private static bool IsCompletedTask(ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var symbol = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol;
        if (symbol?.ContainingType is not { Name: "Task", ContainingNamespace: { Name: "Tasks", ContainingNamespace: { Name: "Threading", ContainingNamespace.Name: "System" } } })
            return false;

        return symbol switch
        {
            IPropertySymbol { Name: "CompletedTask" } => true,
            IMethodSymbol { Name: "FromResult" } when expression is InvocationExpressionSyntax { ArgumentList.Arguments: { Count: 1 } arguments } =>
                arguments[0].Expression is LiteralExpressionSyntax or DefaultExpressionSyntax,
            _ => false
        };
    }
}
