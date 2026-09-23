using System.Collections.Generic;

namespace Breakwater.Analyzers.Sql;

/// <summary>
/// A small, dependency-free SQL lexer used by BW010 (and available to later rules such as
/// BW019). It is not a parser: it only classifies characters into tokens well enough to split
/// statements on top-level <c>;</c> and to find risky keywords, while correctly skipping over
/// string literals and comments so text inside them can never look like a keyword.
/// </summary>
internal static class SqlTokenizer
{
    /// <summary>
    /// Splits <paramref name="sql"/> into statements on top-level <c>;</c> characters and
    /// tokenizes each one. Empty statements (blank text, or only comments/whitespace between two
    /// semicolons) are omitted.
    /// </summary>
    public static IReadOnlyList<SqlStatement> Tokenize(string sql)
    {
        var statements = new List<SqlStatement>();
        var current = new List<SqlToken>();
        var textStart = 0;
        var i = 0;
        var length = sql.Length;

        while (i < length)
        {
            var c = sql[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Line comment: -- ... end of line.
            if (c == '-' && i + 1 < length && sql[i + 1] == '-')
            {
                i += 2;
                while (i < length && sql[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            // Block comment: /* ... */ (no nesting, matching every SQL dialect Breakwater targets).
            if (c == '/' && i + 1 < length && sql[i + 1] == '*')
            {
                i += 2;
                while (i < length && !(sql[i] == '*' && i + 1 < length && sql[i + 1] == '/'))
                {
                    i++;
                }

                i = i < length ? i + 2 : length; // step past the closing "*/", if it was found.
                continue;
            }

            // String literal: '...' with '' as an escaped single quote.
            if (c == '\'')
            {
                var start = i + 1;
                i++;
                while (i < length)
                {
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < length && sql[i + 1] == '\'')
                        {
                            i += 2;
                            continue;
                        }

                        break;
                    }

                    i++;
                }

                var text = sql.Substring(start, System.Math.Min(i, length) - start);
                current.Add(new SqlToken(SqlTokenKind.StringLiteral, text));
                i = i < length ? i + 1 : length; // step past the closing quote, if any.
                continue;
            }

            // Statement separator: end the current statement here.
            if (c == ';')
            {
                AddStatement(statements, current, sql, textStart, i);
                current = new List<SqlToken>();
                i++;
                textStart = i;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
                {
                    i++;
                }

                current.Add(new SqlToken(SqlTokenKind.Word, sql.Substring(start, i - start)));
                continue;
            }

            if (char.IsDigit(c))
            {
                var start = i;
                while (i < length && (char.IsDigit(sql[i]) || sql[i] == '.'))
                {
                    i++;
                }

                current.Add(new SqlToken(SqlTokenKind.Number, sql.Substring(start, i - start)));
                continue;
            }

            current.Add(new SqlToken(SqlTokenKind.Symbol, c.ToString()));
            i++;
        }

        AddStatement(statements, current, sql, textStart, length);
        return statements;
    }

    private static void AddStatement(List<SqlStatement> statements, List<SqlToken> tokens, string sql, int textStart, int textEnd)
    {
        if (tokens.Count == 0)
        {
            return;
        }

        var text = sql.Substring(textStart, System.Math.Max(0, textEnd - textStart)).Trim();
        statements.Add(new SqlStatement(tokens, text));
    }
}
