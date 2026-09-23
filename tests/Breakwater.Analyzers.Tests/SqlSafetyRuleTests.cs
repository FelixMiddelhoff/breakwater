using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW031 (GO/USE batch separators), BW033 (secrets) and BW034 (disables safety checks) in raw SQL.</summary>
public class SqlSafetyRuleTests
{
    [Fact]
    public async Task Sql_with_a_GO_batch_separator_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE Users ADD COLUMN Age int;\nGO\nSELECT 1;");
            """));

        Assert.Equal("BW031", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Sql_with_a_USE_statement_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("USE MyDatabase; SELECT 1;");
            """));

        Assert.Equal("BW031", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Sql_without_a_batch_separator_stays_silent_for_BW031()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("SELECT 1;");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW031");
    }

    [Fact]
    public async Task Sql_creating_a_login_with_a_password_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("CREATE LOGIN app WITH PASSWORD = 'Sup3rSecret!';");
            """));

        Assert.Equal("BW033", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Sql_with_a_connection_string_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("EXEC sp_addlinkedserver @datasrc = 'Server=db;Password=hunter2;';");
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW033");
    }

    [Fact]
    public async Task Sql_without_a_credential_stays_silent_for_BW033()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("CREATE LOGIN app FROM WINDOWS;");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW033");
    }

    [Fact]
    public async Task Sql_disabling_a_constraint_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE Orders NOCHECK CONSTRAINT ALL;");
            """));

        Assert.Equal("BW034", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Sql_disabling_a_trigger_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("DISABLE TRIGGER trg_audit ON Orders;");
            """));

        Assert.Equal("BW034", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Sql_disabling_mysql_foreign_key_checks_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("SET FOREIGN_KEY_CHECKS = 0;");
            """));

        Assert.Equal("BW034", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Sql_dropping_the_database_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("DROP DATABASE Legacy;");
            """));

        // BW010's own DROP trigger also fires on the same statement; both are legitimate findings.
        Assert.Contains(diagnostics, d => d.Id == "BW034");
    }

    [Fact]
    public async Task Sql_that_does_not_disable_anything_stays_silent_for_BW034()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE Orders CHECK CONSTRAINT ALL;");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW034");
    }
}
