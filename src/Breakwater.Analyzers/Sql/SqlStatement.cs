using System.Collections.Generic;

namespace Breakwater.Analyzers.Sql;

/// <summary>One statement out of a (possibly multi-statement) raw SQL string, already tokenized.</summary>
internal sealed class SqlStatement
{
    public SqlStatement(IReadOnlyList<SqlToken> tokens, string text)
    {
        Tokens = tokens;
        Text = text;
    }

    /// <summary>The statement's tokens, in source order. Comments carry no token.</summary>
    public IReadOnlyList<SqlToken> Tokens { get; }

    /// <summary>The statement's original source text (trimmed), for diagnostic messages.</summary>
    public string Text { get; }
}
