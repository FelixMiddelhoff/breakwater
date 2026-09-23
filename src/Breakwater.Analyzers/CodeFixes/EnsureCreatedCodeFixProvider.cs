using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Breakwater.Analyzers.CodeFixes;

/// <summary>
/// BW032: <c>Database.EnsureCreated()</c> in a project that also has migrations skips migration
/// history. The mechanical fix is to remove the call - but only when its result is discarded as a
/// bare statement (<c>x.EnsureCreated();</c>). When the return value is used (for example
/// <c>if (!context.Database.EnsureCreated()) { ... }</c>), removing the call would also remove a
/// condition a human needs to redesign, so no fix is offered there.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EnsureCreatedCodeFixProvider))]
[Shared]
public sealed class EnsureCreatedCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("BW032");

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
        var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>(IsEnsureCreatedInvocation);
        if (invocation is null || invocation.Parent is not ExpressionStatementSyntax statement)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove EnsureCreated() call",
                ct => RemoveStatementAsync(context.Document, root, statement, ct),
                equivalenceKey: "BW032_Remove"),
            diagnostic);
    }

    private static bool IsEnsureCreatedInvocation(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "EnsureCreated" };

    private static Task<Document> RemoveStatementAsync(
        Document document,
        SyntaxNode root,
        ExpressionStatementSyntax statement,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia) ?? root;
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
