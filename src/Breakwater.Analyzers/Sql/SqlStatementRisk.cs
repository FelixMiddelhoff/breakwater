using System.Collections.Generic;
using System.Linq;

namespace Breakwater.Analyzers.Sql;

/// <summary>Judges one already-tokenized SQL statement for BW010's risk patterns.</summary>
internal static class SqlStatementRisk
{
    /// <summary>
    /// Returns a short, human-readable reason when the statement is one BW010 flags (an
    /// unfiltered UPDATE/DELETE, a TRUNCATE, or a DROP), or null when it is not.
    /// </summary>
    public static string? Evaluate(SqlStatement statement)
    {
        var tokens = statement.Tokens;
        if (tokens.Count == 0)
        {
            return null;
        }

        // TRUNCATE and DROP are unsafe wherever they appear in the statement (there is no safe
        // "filtered" form of either), so these two are checked first and everywhere.
        if (tokens.Any(t => t.IsKeyword("TRUNCATE")))
        {
            return "TRUNCATE empties the whole table with no way back";
        }

        if (tokens.Any(t => t.IsKeyword("DROP")))
        {
            return "DROP removes the object outright with no way back";
        }

        var leading = tokens[0];
        if (leading.IsKeyword("UPDATE"))
        {
            return HasEffectiveWhereClause(tokens)
                ? null
                : "UPDATE without a WHERE clause runs against every row in the table";
        }

        if (leading.IsKeyword("DELETE"))
        {
            return HasEffectiveWhereClause(tokens)
                ? null
                : "DELETE without a WHERE clause removes every row in the table";
        }

        return null;
    }

    /// <summary>
    /// True when the statement has a <c>WHERE</c> clause that actually filters something. A
    /// missing <c>WHERE</c> and a <c>WHERE 1 = 1</c> (or <c>WHERE 1=1</c>) both count as "no
    /// filter", per the documented edge case: the constant condition matches every row just like
    /// no condition at all.
    /// </summary>
    private static bool HasEffectiveWhereClause(IReadOnlyList<SqlToken> tokens)
    {
        var whereIndex = -1;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].IsKeyword("WHERE"))
            {
                whereIndex = i;
                break;
            }
        }

        if (whereIndex < 0)
        {
            return false;
        }

        return !IsTrivialAlwaysTrue(tokens, whereIndex + 1);
    }

    /// <summary>
    /// True when the condition starting at <paramref name="start"/> is exactly <c>1 = 1</c> (or
    /// <c>1=1</c>) and nothing else follows it - i.e. the whole WHERE clause is a no-op filter.
    /// Anything more (an added <c>AND ...</c>, a different constant) is treated as a real
    /// condition and left alone, per "silent when unsure".
    /// </summary>
    private static bool IsTrivialAlwaysTrue(IReadOnlyList<SqlToken> tokens, int start)
    {
        var remaining = tokens.Skip(start).ToList();
        return remaining.Count == 3
            && remaining[0].Kind == SqlTokenKind.Number && remaining[0].Text == "1"
            && remaining[1].Kind == SqlTokenKind.Symbol && remaining[1].Text == "="
            && remaining[2].Kind == SqlTokenKind.Number && remaining[2].Text == "1";
    }
}
