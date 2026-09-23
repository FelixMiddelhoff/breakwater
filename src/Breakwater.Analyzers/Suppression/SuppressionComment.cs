using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Breakwater.Analyzers.Suppression;

/// <summary>
/// The <c>// breakwater: allow BWxxx &lt;reason&gt;</c> convention documented in
/// <c>breakwater-rules.md</c>: a single-line comment on the line immediately above the flagged
/// call. A matching comment with a non-empty reason suppresses that rule id for that call; a
/// matching comment with an empty or whitespace-only reason does not suppress - the diagnostic
/// stays and its result says a reason is required.
/// </summary>
internal static class SuppressionComment
{
    private const string Prefix = "breakwater: allow ";

    public enum Result
    {
        /// <summary>No matching comment above the call; the rule's normal severity applies.</summary>
        NotPresent,

        /// <summary>A matching comment with a non-empty reason; the diagnostic is suppressed.</summary>
        SuppressedWithReason,

        /// <summary>A matching comment with no reason; the diagnostic still fires and must say so.</summary>
        ReasonRequired,
    }

    /// <summary>
    /// Looks at the leading trivia of the statement containing <paramref name="invocationSyntax"/>
    /// (the same way <c>MigrationOperationReader</c> reaches syntax around an operation) for a
    /// <c>// breakwater: allow &lt;ruleId&gt; &lt;reason&gt;</c> comment.
    /// </summary>
    public static Result Check(SyntaxNode invocationSyntax, string ruleId)
    {
        var statement = invocationSyntax.FirstAncestorOrSelf<StatementSyntax>() ?? invocationSyntax;

        foreach (var trivia in statement.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                continue;
            }

            var text = trivia.ToString();
            var content = text.StartsWith("//", StringComparison.Ordinal) ? text.Substring(2).Trim() : text.Trim();
            if (!content.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var rest = content.Substring(Prefix.Length);
            var spaceIndex = rest.IndexOf(' ');
            var id = (spaceIndex < 0 ? rest : rest.Substring(0, spaceIndex)).Trim();
            if (!string.Equals(id, ruleId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var reason = spaceIndex < 0 ? string.Empty : rest.Substring(spaceIndex + 1).Trim();
            return string.IsNullOrWhiteSpace(reason) ? Result.ReasonRequired : Result.SuppressedWithReason;
        }

        return Result.NotPresent;
    }
}
