using System.Linq;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>
/// BW037 (raw SQL CREATE TABLE/CREATE INDEX with no IF NOT EXISTS guard: non-idempotent, throws on
/// a re-run).
/// </summary>
public class SqlIdempotentCreateRuleTests
{
    [Fact]
    public async Task Bare_create_table_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("CREATE TABLE Widget (Id INT)");
            """));

        Assert.Equal(new[] { "BW037" }, diagnostics.Select(d => d.Id));
        var diagnostic = diagnostics.Single();
        Assert.Contains("TABLE", diagnostic.GetMessage());
        Assert.Contains("Widget", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Bare_create_index_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("CREATE INDEX IX_Widget_Name ON Widget (Name)");
            """));

        Assert.Equal(new[] { "BW037" }, diagnostics.Select(d => d.Id));
        var diagnostic = diagnostics.Single();
        Assert.Contains("INDEX", diagnostic.GetMessage());
        Assert.Contains("IX_Widget_Name", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Bare_create_unique_index_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("CREATE UNIQUE INDEX IX_Widget_Name ON Widget (Name)");
            """));

        Assert.Equal(new[] { "BW037" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Create_table_if_not_exists_is_not_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("CREATE TABLE IF NOT EXISTS Widget (Id INT)");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Create_index_if_not_exists_is_not_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_Widget_Name ON Widget (Name)");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Create_procedure_is_out_of_scope_handled_by_bw036_instead()
    {
        // TABLE/INDEX are the structural-object scope for BW037; PROCEDURE/FUNCTION/VIEW is
        // BW036's redefinable-object scope, not this rule's job.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("CREATE PROCEDURE Widget_Get() BEGIN SELECT 1; END");
            """));

        Assert.DoesNotContain("BW037", diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Create_view_is_out_of_scope()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("CREATE VIEW ActiveWidgets AS SELECT 1");
            """));

        Assert.DoesNotContain("BW037", diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Keyword_inside_string_literal_does_not_trigger()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("INSERT INTO Log (Message) VALUES ('CREATE TABLE Widget (Id INT)')");
            """));

        Assert.DoesNotContain("BW037", diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Multi_statement_reports_only_the_first_unguarded_create()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"
                CREATE TABLE Widget (Id INT);
                CREATE TABLE Gadget (Id INT)");
            """));

        Assert.Equal(new[] { "BW037" }, diagnostics.Select(d => d.Id));
        Assert.Contains("Widget", diagnostics.Single().GetMessage());
    }

    [Fact]
    public async Task Non_constant_sql_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            var sql = GetSql();
            migrationBuilder.Sql(sql);
            """, members: "private static string GetSql() => \"CREATE TABLE Widget (Id INT)\";"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Sql_in_down_is_ignored()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithDown("""
            migrationBuilder.Sql("CREATE TABLE Widget (Id INT)");
            """));

        Assert.Empty(diagnostics);
    }
}
