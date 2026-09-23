using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW010 (raw <c>Sql(...)</c>: unfiltered UPDATE/DELETE, TRUNCATE, DROP).</summary>
public class SqlUnsafeStatementRuleTests
{
    [Fact]
    public async Task Update_without_where_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("UPDATE Users SET Active = 1");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
        Assert.Contains("UPDATE without a WHERE clause", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Delete_without_where_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("DELETE FROM Users");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
        Assert.Contains("DELETE without a WHERE clause", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Truncate_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("TRUNCATE TABLE Users");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
        Assert.Contains("TRUNCATE", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Drop_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("DROP TABLE Users");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
        Assert.Contains("DROP", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Where_1_equals_1_is_reported_as_unfiltered()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("UPDATE Users SET Active = 1 WHERE 1 = 1");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
    }

    [Fact]
    public async Task Update_with_a_real_where_clause_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("UPDATE Users SET Active = 1 WHERE Id = 42");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Select_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("SELECT * FROM Users");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Non_constant_sql_stays_silent()
    {
        // The content is not statically knowable (a variable), so BW010 must not guess.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            var sql = GetSql();
            migrationBuilder.Sql(sql);
            """, members: "private static string GetSql() => \"UPDATE Users SET Active = 1\";"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task File_read_all_text_sql_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(System.IO.File.ReadAllText("backfill.sql"));
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Interpolated_sql_with_a_non_constant_hole_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            var tableName = "Users";
            migrationBuilder.Sql($"DELETE FROM {tableName}");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Constant_interpolated_sql_is_still_analyzed()
    {
        // All interpolation holes are themselves compile-time constants, so the whole
        // interpolated string folds to a constant and BW010 can still analyze it.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            const string table = "Users";
            migrationBuilder.Sql($"DELETE FROM {table}");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
    }

    [Fact]
    public async Task Verbatim_string_sql_is_analyzed()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"DELETE FROM Users");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
    }

    [Fact]
    public async Task Raw_string_literal_sql_is_analyzed()
    {
        // Built without a raw string literal in this test file itself (to avoid nesting a raw
        // string inside a raw string): the migration source under test contains
        // migrationBuilder.Sql("""DELETE FROM Users""");
        var upBody = "migrationBuilder.Sql(\"\"\"DELETE FROM Users\"\"\");";
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp(upBody));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
    }

    [Fact]
    public async Task Concatenated_string_literals_across_lines_are_analyzed()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(
                "DELETE " +
                "FROM Users");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
    }

    [Fact]
    public async Task Multiple_statements_in_one_call_are_each_checked_and_the_first_unsafe_one_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("UPDATE Users SET Active = 1 WHERE Id = 1; DELETE FROM Orders; SELECT 1;");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW010", diagnostic.Id);
        Assert.Contains("DELETE without a WHERE clause", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Multiple_safe_statements_in_one_call_stay_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("UPDATE Users SET Active = 1 WHERE Id = 1; DELETE FROM Orders WHERE Id = 2;");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Keyword_inside_a_string_literal_does_not_false_positive()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("UPDATE Users SET Note = 'please DROP TABLE nothing' WHERE Id = 1");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Keyword_inside_a_comment_does_not_false_positive()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("UPDATE Users SET Active = 1 -- TRUNCATE TABLE Users\nWHERE Id = 1");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Sql_in_down_is_ignored()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithDown("""
            migrationBuilder.Sql("DELETE FROM Users");
            """));

        Assert.Empty(diagnostics);
    }
}
