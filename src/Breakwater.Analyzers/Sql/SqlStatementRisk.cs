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
    /// Returns a short reason when the statement is one of BW019's Postgres-specific risk
    /// patterns hidden in raw SQL (a <c>CREATE INDEX</c> without <c>CONCURRENTLY</c>, a
    /// <c>LOCK TABLE</c>, a <c>VACUUM FULL</c>, or an <c>ALTER TABLE ... SET DATA TYPE</c>), or
    /// null when it is not. Deliberately does not repeat BW010's own triggers (TRUNCATE/DROP/
    /// unfiltered UPDATE-DELETE): the two rules look for different keywords, so a statement never
    /// needs suppressing between them.
    /// </summary>
    public static string? EvaluatePostgresRisk(SqlStatement statement)
    {
        var tokens = statement.Tokens;
        if (tokens.Count == 0)
        {
            return null;
        }

        if (tokens[0].IsKeyword("CREATE") && tokens.Any(t => t.IsKeyword("INDEX")) && !tokens.Any(t => t.IsKeyword("CONCURRENTLY")))
        {
            return "CREATE INDEX without CONCURRENTLY blocks writes while the index builds";
        }

        if (tokens[0].IsKeyword("LOCK"))
        {
            return "LOCK TABLE blocks other sessions for as long as the lock is held";
        }

        if (tokens[0].IsKeyword("VACUUM") && tokens.Any(t => t.IsKeyword("FULL")))
        {
            return "VACUUM FULL takes an exclusive lock and rewrites the whole table";
        }

        if (tokens[0].IsKeyword("ALTER") && tokens.Any(t => t.IsKeyword("TYPE")) && tokens.Any(t => t.Kind == SqlTokenKind.Word && t.Text.Equals("SET", System.StringComparison.OrdinalIgnoreCase)))
        {
            return "ALTER TABLE ... SET DATA TYPE can rewrite the whole table under lock";
        }

        return null;
    }

    /// <summary>
    /// BW035: returns a short reason when the statement is an <c>ALTER TABLE</c> disguising a
    /// structural schema change - a <c>DROP COLUMN</c> (same risk class as BW001) or an
    /// <c>ADD COLUMN ... NOT NULL</c> with no <c>DEFAULT</c> (same risk class as BW006) - hidden
    /// in raw SQL text instead of the typed API. Only <c>ALTER TABLE</c> statements are examined;
    /// anything else returns null. DROP COLUMN is checked first (matching the real corpus example,
    /// bitwarden-mysql's <c>GrantIdWithIndexes.cs</c>, which drops before it re-adds).
    /// </summary>
    public static string? EvaluateStructuralChange(SqlStatement statement)
    {
        var tokens = statement.Tokens;
        if (tokens.Count < 2 || !tokens[0].IsKeyword("ALTER") || !tokens[1].IsKeyword("TABLE"))
        {
            return null;
        }

        var table = NextIdentifier(tokens, 2) ?? "<unknown table>";

        for (var i = 2; i < tokens.Count; i++)
        {
            if (tokens[i].IsKeyword("DROP") && i + 1 < tokens.Count && tokens[i + 1].IsKeyword("COLUMN"))
            {
                var column = NextIdentifier(tokens, i + 2);
                if (column is not null)
                {
                    return $"ALTER TABLE {table} DROP COLUMN {column} drops a column exactly like a typed " +
                        $"DropColumn(name: \"{column}\", table: \"{table}\") call would (same data-loss risk as BW001), " +
                        "hidden in raw SQL where that rule cannot see it";
                }
            }
        }

        for (var i = 2; i < tokens.Count; i++)
        {
            if (tokens[i].IsKeyword("ADD") && i + 1 < tokens.Count && tokens[i + 1].IsKeyword("COLUMN"))
            {
                var column = NextIdentifier(tokens, i + 2);
                if (column is null)
                {
                    continue;
                }

                // The clause for this column runs until the next top-level comma (the start of a
                // sibling ADD/DROP clause) or the end of the statement.
                var clauseEnd = i + 2;
                while (clauseEnd < tokens.Count && !(tokens[clauseEnd].Kind == SqlTokenKind.Symbol && tokens[clauseEnd].Text == ","))
                {
                    clauseEnd++;
                }

                var hasNotNull = false;
                var hasDefault = false;
                for (var j = i + 2; j < clauseEnd; j++)
                {
                    if (tokens[j].IsKeyword("NOT") && j + 1 < clauseEnd && tokens[j + 1].IsKeyword("NULL"))
                    {
                        hasNotNull = true;
                    }

                    if (tokens[j].IsKeyword("DEFAULT"))
                    {
                        hasDefault = true;
                    }
                }

                if (hasNotNull && !hasDefault)
                {
                    return $"ALTER TABLE {table} ADD COLUMN {column} NOT NULL with no DEFAULT fails on a non-empty " +
                        $"table exactly like a typed AddColumn(name: \"{column}\", table: \"{table}\", nullable: false) " +
                        "call with no default would (same risk as BW006), hidden in raw SQL where that rule cannot see it";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// BW036: true when <paramref name="statement"/> is a <c>DROP PROCEDURE/FUNCTION/VIEW IF
    /// EXISTS &lt;name&gt;</c> statement, with the dropped object's kind and name returned.
    /// </summary>
    public static bool TryGetDropIfExists(SqlStatement statement, out string kind, out string name)
    {
        kind = string.Empty;
        name = string.Empty;
        var tokens = statement.Tokens;
        if (tokens.Count < 2 || !tokens[0].IsKeyword("DROP"))
        {
            return false;
        }

        var kindIndex = -1;
        foreach (var candidate in RedefinableKinds)
        {
            if (tokens[1].IsKeyword(candidate))
            {
                kindIndex = 1;
                kind = candidate;
                break;
            }
        }

        if (kindIndex < 0)
        {
            return false;
        }

        var next = kindIndex + 1;
        if (next < tokens.Count && tokens[next].IsKeyword("IF") && next + 1 < tokens.Count && tokens[next + 1].IsKeyword("EXISTS"))
        {
            next += 2;
        }
        else
        {
            // Only the "IF EXISTS" form is the recognized idempotent-redefinition idiom; an
            // unconditional DROP is not treated as paired even if a matching CREATE follows.
            return false;
        }

        var identifier = NextIdentifier(tokens, next);
        if (identifier is null)
        {
            return false;
        }

        name = identifier;
        return true;
    }

    /// <summary>
    /// BW036: true when <paramref name="statement"/> is a <c>CREATE PROCEDURE/FUNCTION/VIEW
    /// &lt;name&gt;</c> statement (an optional <c>OR REPLACE</c> between <c>CREATE</c> and the
    /// kind keyword is allowed), with the created object's kind and name returned.
    /// </summary>
    public static bool TryGetCreate(SqlStatement statement, out string kind, out string name)
    {
        kind = string.Empty;
        name = string.Empty;
        var tokens = statement.Tokens;
        if (tokens.Count < 2 || !tokens[0].IsKeyword("CREATE"))
        {
            return false;
        }

        var i = 1;
        if (i + 1 < tokens.Count && tokens[i].IsKeyword("OR") && tokens[i + 1].IsKeyword("REPLACE"))
        {
            i += 2;
        }

        if (i >= tokens.Count)
        {
            return false;
        }

        var kindIndex = -1;
        foreach (var candidate in RedefinableKinds)
        {
            if (tokens[i].IsKeyword(candidate))
            {
                kindIndex = i;
                kind = candidate;
                break;
            }
        }

        if (kindIndex < 0)
        {
            return false;
        }

        var identifier = NextIdentifier(tokens, kindIndex + 1);
        if (identifier is null)
        {
            return false;
        }

        name = identifier;
        return true;
    }

    private static readonly string[] RedefinableKinds = { "PROCEDURE", "FUNCTION", "VIEW" };

    /// <summary>
    /// Returns the text of the first <see cref="SqlTokenKind.Word"/> token found scanning forward
    /// from <paramref name="fromIndex"/>, skipping over quote/bracket punctuation (backtick,
    /// square bracket, double quote) so a quoted identifier is still recognized. Stops (returns
    /// null) at anything else - a string literal or number is never an identifier here.
    /// </summary>
    private static string? NextIdentifier(IReadOnlyList<SqlToken> tokens, int fromIndex)
    {
        for (var i = fromIndex; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == SqlTokenKind.Word)
            {
                return token.Text;
            }

            if (token.Kind == SqlTokenKind.Symbol)
            {
                continue;
            }

            return null;
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
