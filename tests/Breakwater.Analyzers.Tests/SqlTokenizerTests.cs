using System.Linq;
using Breakwater.Analyzers.Sql;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>Unit tests for <see cref="SqlTokenizer"/> and <see cref="SqlStatementRisk"/> directly, without going through the analyzer.</summary>
public class SqlTokenizerTests
{
    [Fact]
    public void Single_statement_produces_one_statement()
    {
        var statements = SqlTokenizer.Tokenize("UPDATE Users SET Active = 1");

        Assert.Single(statements);
        Assert.Contains(statements[0].Tokens, t => t.IsKeyword("UPDATE"));
    }

    [Fact]
    public void Multiple_statements_are_split_on_semicolons()
    {
        var statements = SqlTokenizer.Tokenize("UPDATE A SET X = 1; DELETE FROM B; TRUNCATE TABLE C;");

        Assert.Equal(3, statements.Count);
        Assert.True(statements[0].Tokens[0].IsKeyword("UPDATE"));
        Assert.True(statements[1].Tokens[0].IsKeyword("DELETE"));
        Assert.True(statements[2].Tokens[0].IsKeyword("TRUNCATE"));
    }

    [Fact]
    public void Empty_statements_between_semicolons_are_omitted()
    {
        var statements = SqlTokenizer.Tokenize("UPDATE A SET X = 1;;  ;DELETE FROM B;");

        Assert.Equal(2, statements.Count);
    }

    [Fact]
    public void String_literals_are_tokenized_as_a_single_token_with_quotes_stripped()
    {
        var statements = SqlTokenizer.Tokenize("SELECT * FROM Users WHERE Name = 'DROP TABLE Users'");

        var stringToken = statements[0].Tokens.Single(t => t.Kind == SqlTokenKind.StringLiteral);
        Assert.Equal("DROP TABLE Users", stringToken.Text);
    }

    [Fact]
    public void Keywords_inside_string_literals_are_not_keyword_tokens()
    {
        var statements = SqlTokenizer.Tokenize("SELECT * FROM Users WHERE Name = 'please DROP TABLE Users'");

        Assert.DoesNotContain(statements[0].Tokens, t => t.IsKeyword("DROP"));
    }

    [Fact]
    public void Escaped_single_quotes_inside_a_string_literal_do_not_end_it()
    {
        var statements = SqlTokenizer.Tokenize("UPDATE Users SET Name = 'O''Brien''s' WHERE Id = 1");

        var stringToken = statements[0].Tokens.Single(t => t.Kind == SqlTokenKind.StringLiteral);
        Assert.Equal("O''Brien''s", stringToken.Text);
        // The tokenizer did not stop early, so WHERE is still found after the string.
        Assert.Contains(statements[0].Tokens, t => t.IsKeyword("WHERE"));
    }

    [Fact]
    public void Line_comments_are_skipped_and_their_keywords_ignored()
    {
        var statements = SqlTokenizer.Tokenize("SELECT 1 -- DROP TABLE Users\nFROM Users");

        Assert.DoesNotContain(statements[0].Tokens, t => t.IsKeyword("DROP"));
        Assert.Contains(statements[0].Tokens, t => t.IsKeyword("FROM"));
    }

    [Fact]
    public void Block_comments_are_skipped_and_their_keywords_ignored()
    {
        var statements = SqlTokenizer.Tokenize("UPDATE Users /* TRUNCATE everything, just kidding */ SET Active = 1 WHERE Id = 1");

        Assert.DoesNotContain(statements[0].Tokens, t => t.IsKeyword("TRUNCATE"));
        Assert.Contains(statements[0].Tokens, t => t.IsKeyword("WHERE"));
    }

    [Fact]
    public void Semicolons_inside_string_literals_do_not_split_the_statement()
    {
        var statements = SqlTokenizer.Tokenize("UPDATE Users SET Note = 'a; b; c' WHERE Id = 1");

        Assert.Single(statements);
    }

    [Fact]
    public void Keywords_are_recognized_case_insensitively()
    {
        var upper = SqlTokenizer.Tokenize("update Users set Active = 1")[0];
        var mixed = SqlTokenizer.Tokenize("UpDaTe Users SeT Active = 1")[0];

        Assert.True(upper.Tokens[0].IsKeyword("UPDATE"));
        Assert.True(mixed.Tokens[0].IsKeyword("UPDATE"));
    }

    [Theory]
    [InlineData("UPDATE Users SET Active = 1")]
    [InlineData("DELETE FROM Users")]
    [InlineData("TRUNCATE TABLE Users")]
    [InlineData("DROP TABLE Users")]
    public void Risky_statements_without_a_filter_are_flagged(string sql)
    {
        var statement = SqlTokenizer.Tokenize(sql)[0];

        Assert.NotNull(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Update_with_a_real_where_clause_is_not_flagged()
    {
        var statement = SqlTokenizer.Tokenize("UPDATE Users SET Active = 1 WHERE Id = 42")[0];

        Assert.Null(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Delete_with_a_real_where_clause_is_not_flagged()
    {
        var statement = SqlTokenizer.Tokenize("DELETE FROM Users WHERE Id = 42")[0];

        Assert.Null(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Where_1_equals_1_is_still_flagged_as_unfiltered()
    {
        var statement = SqlTokenizer.Tokenize("UPDATE Users SET Active = 1 WHERE 1 = 1")[0];

        Assert.NotNull(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Where_1_equals_1_without_spaces_is_still_flagged_as_unfiltered()
    {
        var statement = SqlTokenizer.Tokenize("DELETE FROM Users WHERE 1=1")[0];

        Assert.NotNull(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Where_1_equals_1_combined_with_a_real_condition_is_not_flagged()
    {
        // "still effectively no filter" only applies to the trivial 1=1 clause on its own; once a
        // real condition is added the statement does filter something and BW010 stays silent.
        var statement = SqlTokenizer.Tokenize("UPDATE Users SET Active = 1 WHERE 1 = 1 AND Id = 42")[0];

        Assert.Null(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Update_from_with_where_is_not_flagged()
    {
        var statement = SqlTokenizer.Tokenize("UPDATE A SET X = B.Y FROM B WHERE A.Id = B.Id")[0];

        Assert.Null(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Update_from_without_where_is_flagged()
    {
        var statement = SqlTokenizer.Tokenize("UPDATE A SET X = B.Y FROM B")[0];

        Assert.NotNull(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Delete_using_with_where_is_not_flagged()
    {
        var statement = SqlTokenizer.Tokenize("DELETE FROM A USING B WHERE A.Id = B.Id")[0];

        Assert.Null(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Delete_using_without_where_is_flagged()
    {
        var statement = SqlTokenizer.Tokenize("DELETE FROM A USING B")[0];

        Assert.NotNull(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Select_statement_is_never_flagged()
    {
        var statement = SqlTokenizer.Tokenize("SELECT * FROM Users")[0];

        Assert.Null(SqlStatementRisk.Evaluate(statement));
    }

    [Fact]
    public void Multiple_statements_in_one_string_are_each_evaluated_independently()
    {
        var statements = SqlTokenizer.Tokenize("UPDATE A SET X = 1 WHERE Id = 1; DELETE FROM B;");

        Assert.Null(SqlStatementRisk.Evaluate(statements[0]));
        Assert.NotNull(SqlStatementRisk.Evaluate(statements[1]));
    }
}
