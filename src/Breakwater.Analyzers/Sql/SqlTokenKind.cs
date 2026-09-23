namespace Breakwater.Analyzers.Sql;

/// <summary>The kind of one token produced by <see cref="SqlTokenizer"/>.</summary>
internal enum SqlTokenKind
{
    /// <summary>A run of letters/digits/underscores: a keyword, identifier, or table/column name.</summary>
    Word,

    /// <summary>A run of digits (and at most one decimal point): a numeric literal.</summary>
    Number,

    /// <summary>A single-quoted string literal, with its quotes stripped and <c>''</c> escapes intact.</summary>
    StringLiteral,

    /// <summary>Any other single character: operators, punctuation, parentheses.</summary>
    Symbol,
}
