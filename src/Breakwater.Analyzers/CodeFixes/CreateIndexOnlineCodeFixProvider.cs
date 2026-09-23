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

namespace Breakwater.Analyzers.CodeFixes;

/// <summary>
/// BW007: a plain <c>CreateIndex</c> call blocks writes while it builds. The mechanical fix is
/// provider-specific (Postgres needs both an annotation and <c>SuppressTransaction = true</c>; SQL
/// Server only needs an annotation), and the analyzer cannot see which database provider a project
/// targets at compile time unless <c>breakwater_provider</c> is set to a non-<c>auto</c> value in
/// .editorconfig - config a code fix provider does not read here, since <see cref="CodeFixContext"/>
/// gives no supported access to <c>AnalyzerConfigOptions</c>. Rather than guess, both fixes are
/// always offered side by side; the developer picks the one that matches their database.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CreateIndexOnlineCodeFixProvider))]
[Shared]
public sealed class CreateIndexOnlineCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("BW007");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>(IsCreateIndexInvocation);
        if (invocation is null || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add PostgreSQL concurrent index build (CreatedConcurrently + SuppressTransaction)",
                ct => AddPostgresConcurrentAsync(context.Document, root, invocation, memberAccess, ct),
                equivalenceKey: "BW007_Postgres"),
            diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add SQL Server online index build (SqlServer:Online)",
                ct => Task.FromResult(AddAnnotationOnly(context.Document, root, invocation, "SqlServer:Online")),
                equivalenceKey: "BW007_SqlServer"),
            diagnostic);
    }

    private static bool IsCreateIndexInvocation(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "CreateIndex" };

    private static Document AddAnnotationOnly(Document document, SyntaxNode root, InvocationExpressionSyntax invocation, string annotationName)
    {
        var newInvocation = WrapWithAnnotation(invocation, annotationName);
        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }

    private static Task<Document> AddPostgresConcurrentAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var newInvocation = WrapWithAnnotation(invocation, "Npgsql:CreatedConcurrently");
        var statement = invocation.FirstAncestorOrSelf<ExpressionStatementSyntax>();
        var builderName = memberAccess.Expression.ToString();

        if (statement is null || AlreadySuppressesTransaction(statement, builderName))
        {
            var directRoot = root.ReplaceNode(invocation, newInvocation);
            return Task.FromResult(document.WithSyntaxRoot(directRoot));
        }

        var suppressStatement = SyntaxFactory
            .ParseStatement($"{builderName}.SuppressTransaction = true;")
            .WithLeadingTrivia(statement.GetLeadingTrivia())
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        var newStatement = statement.ReplaceNode(invocation, newInvocation);
        var newRoot = root.ReplaceNode(statement, new SyntaxNode[] { suppressStatement, newStatement });
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    /// <summary>
    /// Looks at every assignment in the enclosing method for an existing
    /// <c>&lt;builderName&gt;.SuppressTransaction = true;</c>, so the fix never inserts a second one
    /// when applied more than once (idempotency) or when the developer already set it by hand.
    /// </summary>
    private static bool AlreadySuppressesTransaction(SyntaxNode statement, string builderName)
    {
        var method = statement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return false;
        }

        return method.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment =>
                assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.Text: "SuppressTransaction" } left
                && left.Expression.ToString() == builderName
                && assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression));
    }

    private static InvocationExpressionSyntax WrapWithAnnotation(InvocationExpressionSyntax invocation, string annotationName)
    {
        var annotationAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            invocation,
            SyntaxFactory.IdentifierName("Annotation"));

        var arguments = SyntaxFactory.SeparatedList(new[]
        {
            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(annotationName))),
            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)),
        });

        return SyntaxFactory.InvocationExpression(annotationAccess, SyntaxFactory.ArgumentList(arguments));
    }
}
