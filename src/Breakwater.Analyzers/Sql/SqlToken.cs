namespace Breakwater.Analyzers.Sql;

/// <summary>
/// One lexical token from a SQL statement. Comments and whitespace are dropped by the tokenizer
/// and never produce tokens, so rules never have to filter them out.
/// </summary>
internal readonly struct SqlToken
{
    public SqlToken(SqlTokenKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    public SqlTokenKind Kind { get; }

    /// <summary>The token's exact source text (a string literal's quotes are stripped).</summary>
    public string Text { get; }

    /// <summary>
    /// True when this is a <see cref="SqlTokenKind.Word"/> that case-insensitively equals
    /// <paramref name="keyword"/>. Only <see cref="SqlTokenKind.Word"/> tokens can ever match a
    /// keyword, so text sitting inside a string literal or comment never does.
    /// </summary>
    public bool IsKeyword(string keyword) => Kind == SqlTokenKind.Word && Text.Equals(keyword, System.StringComparison.OrdinalIgnoreCase);
}
