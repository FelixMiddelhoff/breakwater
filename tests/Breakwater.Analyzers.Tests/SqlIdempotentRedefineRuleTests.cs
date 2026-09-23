using System.Linq;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>
/// BW036 (DROP ... IF EXISTS immediately followed by CREATE of the same name and kind: idempotent
/// redefinition, lower severity than BW010's unpaired-DROP finding).
/// </summary>
public class SqlIdempotentRedefineRuleTests
{
    [Fact]
    public async Task Drop_procedure_if_exists_then_create_same_procedure_reports_only_BW036()
    {
        // The real bitwarden-mysql/platformplatform idiom: redefine, not remove.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"
                DROP PROCEDURE IF EXISTS `Grant_UpdateId`;
                CREATE PROCEDURE `Grant_UpdateId`() BEGIN SELECT 1; END");
            """));

        Assert.Equal(new[] { "BW036" }, diagnostics.Select(d => d.Id));
        var diagnostic = diagnostics.Single();
        Assert.Contains("PROCEDURE", diagnostic.GetMessage());
        Assert.Contains("Grant_UpdateId", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Drop_function_if_exists_then_create_same_function_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS GetActiveUserCount;
                CREATE FUNCTION GetActiveUserCount() RETURNS INT RETURN 1");
            """));

        Assert.Equal(new[] { "BW036" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Drop_view_if_exists_then_create_or_replace_same_view_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"
                DROP VIEW IF EXISTS ActiveUsers;
                CREATE OR REPLACE VIEW ActiveUsers AS SELECT 1");
            """));

        Assert.Equal(new[] { "BW036" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Unpaired_drop_procedure_still_reports_BW010_at_full_severity()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS OrphanProcedure");
            """));

        Assert.Equal(new[] { "BW010" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Drop_without_if_exists_then_matching_create_is_not_treated_as_the_idiom()
    {
        // Only the "IF EXISTS" form is the recognized idempotent-redefinition idiom.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"
                DROP PROCEDURE Grant_UpdateId;
                CREATE PROCEDURE Grant_UpdateId() BEGIN SELECT 1; END");
            """));

        Assert.Equal(new[] { "BW010" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Create_before_drop_does_not_count_order_matters()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"
                CREATE PROCEDURE Grant_UpdateId() BEGIN SELECT 1; END;
                DROP PROCEDURE IF EXISTS Grant_UpdateId");
            """));

        Assert.Equal(new[] { "BW010" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Drop_and_create_of_different_names_is_not_paired()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"
                DROP PROCEDURE IF EXISTS OldProcedure;
                CREATE PROCEDURE NewProcedure() BEGIN SELECT 1; END");
            """));

        Assert.Equal(new[] { "BW010" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Drop_and_create_of_different_kinds_is_not_paired()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"
                DROP PROCEDURE IF EXISTS Widget;
                CREATE FUNCTION Widget() RETURNS INT RETURN 1");
            """));

        Assert.Equal(new[] { "BW010" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Drop_table_if_exists_is_not_the_redefinable_idiom_kind()
    {
        // Only PROCEDURE/FUNCTION/VIEW are redefinable objects for this idiom; a table is a
        // structural object, not covered here (BW002 covers DropTable for the typed API; BW010
        // still flags an unpaired raw-SQL table drop).
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS Widget;
                CREATE TABLE Widget (Id INT)");
            """));

        Assert.Equal(new[] { "BW010" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Non_constant_sql_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            var sql = GetSql();
            migrationBuilder.Sql(sql);
            """, members: "private static string GetSql() => \"DROP PROCEDURE IF EXISTS X; CREATE PROCEDURE X() BEGIN END\";"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Sql_in_down_is_ignored()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithDown("""
            migrationBuilder.Sql(@"
                DROP PROCEDURE IF EXISTS Grant_UpdateId;
                CREATE PROCEDURE Grant_UpdateId() BEGIN SELECT 1; END");
            """));

        Assert.Empty(diagnostics);
    }
}
