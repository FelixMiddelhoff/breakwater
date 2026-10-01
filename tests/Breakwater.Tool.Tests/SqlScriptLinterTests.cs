using Breakwater.Analyzers.Configuration;
using Breakwater.Tool;
using Xunit;

namespace Breakwater.Tool.Tests;

/// <summary>Covers the rule shapes the tool reuses from the analyzer: BW010, BW019, BW031, BW033, BW034, BW035, BW036, BW037.</summary>
public class SqlScriptLinterTests
{
    [Fact]
    public void Unfiltered_DELETE_is_flagged_as_BW010()
    {
        var findings = SqlScriptLinter.Lint("DELETE FROM Users;", BreakwaterDatabaseProvider.SqlServer);

        var finding = Assert.Single(findings);
        Assert.Equal("BW010", finding.RuleId);
    }

    [Fact]
    public void TRUNCATE_is_flagged_as_BW010()
    {
        var findings = SqlScriptLinter.Lint("TRUNCATE TABLE Orders;", BreakwaterDatabaseProvider.Postgres);

        Assert.Contains(findings, f => f.RuleId == "BW010");
    }

    [Fact]
    public void DROP_TABLE_is_flagged_as_BW010()
    {
        var findings = SqlScriptLinter.Lint("DROP TABLE Orders;", BreakwaterDatabaseProvider.MySql);

        Assert.Contains(findings, f => f.RuleId == "BW010");
    }

    [Fact]
    public void Filtered_DELETE_produces_no_findings()
    {
        var findings = SqlScriptLinter.Lint("DELETE FROM Users WHERE Id = 1;", BreakwaterDatabaseProvider.SqlServer);

        Assert.Empty(findings);
    }

    [Fact]
    public void Safe_script_produces_no_findings()
    {
        const string script = """
            CREATE TABLE IF NOT EXISTS Users (
                Id int NOT NULL PRIMARY KEY,
                Name nvarchar(100) NULL
            );

            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES (N'20240101000000_Initial', N'8.0.0');
            """;

        var findings = SqlScriptLinter.Lint(script, BreakwaterDatabaseProvider.SqlServer);

        Assert.Empty(findings);
    }

    [Fact]
    public void Bare_CREATE_TABLE_is_flagged_as_BW037()
    {
        var findings = SqlScriptLinter.Lint("CREATE TABLE Users (Id int NOT NULL PRIMARY KEY);", BreakwaterDatabaseProvider.Postgres);

        var finding = Assert.Single(findings);
        Assert.Equal("BW037", finding.RuleId);
    }

    [Fact]
    public void CREATE_TABLE_IF_NOT_EXISTS_produces_no_findings()
    {
        var findings = SqlScriptLinter.Lint("CREATE TABLE IF NOT EXISTS Users (Id int NOT NULL PRIMARY KEY);", BreakwaterDatabaseProvider.Postgres);

        Assert.Empty(findings);
    }

    [Fact]
    public void Bare_CREATE_INDEX_on_postgres_reports_both_BW019_and_BW037()
    {
        var findings = SqlScriptLinter.Lint("CREATE INDEX IX_Users_Name ON Users (Name);", BreakwaterDatabaseProvider.Postgres);

        Assert.Contains(findings, f => f.RuleId == "BW019");
        Assert.Contains(findings, f => f.RuleId == "BW037");
    }

    [Fact]
    public void GO_batch_separator_is_flagged_as_BW031_only_for_sql_server()
    {
        const string script = "ALTER TABLE Users ADD COLUMN Age int;\nGO\nSELECT 1;";

        var sqlServerFindings = SqlScriptLinter.Lint(script, BreakwaterDatabaseProvider.SqlServer);
        Assert.Contains(sqlServerFindings, f => f.RuleId == "BW031");

        var postgresFindings = SqlScriptLinter.Lint(script, BreakwaterDatabaseProvider.Postgres);
        Assert.DoesNotContain(postgresFindings, f => f.RuleId == "BW031");
    }

    [Fact]
    public void Password_clause_is_flagged_as_BW033()
    {
        var findings = SqlScriptLinter.Lint("CREATE LOGIN bob WITH PASSWORD = 'hunter2';", BreakwaterDatabaseProvider.SqlServer);

        Assert.Contains(findings, f => f.RuleId == "BW033");
    }

    [Fact]
    public void Disabled_foreign_key_checks_is_flagged_as_BW034()
    {
        var findings = SqlScriptLinter.Lint("SET FOREIGN_KEY_CHECKS = 0;", BreakwaterDatabaseProvider.MySql);

        Assert.Contains(findings, f => f.RuleId == "BW034");
    }

    [Fact]
    public void Postgres_create_index_without_concurrently_is_flagged_as_BW019_only_for_postgres()
    {
        const string script = "CREATE INDEX IX_Users_Name ON Users (Name);";

        var postgresFindings = SqlScriptLinter.Lint(script, BreakwaterDatabaseProvider.Postgres);
        Assert.Contains(postgresFindings, f => f.RuleId == "BW019");

        var sqlServerFindings = SqlScriptLinter.Lint(script, BreakwaterDatabaseProvider.SqlServer);
        Assert.DoesNotContain(sqlServerFindings, f => f.RuleId == "BW019");
    }

    [Fact]
    public void ALTER_TABLE_DROP_COLUMN_is_flagged_as_BW035_not_BW010()
    {
        var findings = SqlScriptLinter.Lint("ALTER TABLE Grant DROP COLUMN Id;", BreakwaterDatabaseProvider.MySql);

        var finding = Assert.Single(findings);
        Assert.Equal("BW035", finding.RuleId);
    }

    [Fact]
    public void DROP_IF_EXISTS_followed_by_matching_CREATE_is_flagged_as_BW036_not_BW010()
    {
        const string script = """
            DROP PROCEDURE IF EXISTS UpsertUser;
            CREATE PROCEDURE UpsertUser AS SELECT 1;
            """;

        var findings = SqlScriptLinter.Lint(script, BreakwaterDatabaseProvider.SqlServer);

        var finding = Assert.Single(findings);
        Assert.Equal("BW036", finding.RuleId);
    }

    [Fact]
    public void Unpaired_DROP_IF_EXISTS_is_still_flagged_as_BW010()
    {
        const string script = "DROP PROCEDURE IF EXISTS UpsertUser;";

        var findings = SqlScriptLinter.Lint(script, BreakwaterDatabaseProvider.SqlServer);

        Assert.Contains(findings, f => f.RuleId == "BW010");
    }

    [Fact]
    public void Findings_are_attributed_to_the_migration_that_contains_them()
    {
        const string script = """
            DELETE FROM Users;
            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES (N'20240101000000_First', N'8.0.0');
            DROP TABLE Orders;
            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES (N'20240102000000_Second', N'8.0.0');
            """;

        var findings = SqlScriptLinter.Lint(script, BreakwaterDatabaseProvider.SqlServer);

        Assert.Equal(2, findings.Count);
        Assert.Equal("20240101000000_First", findings[0].Migration);
        Assert.Equal("20240102000000_Second", findings[1].Migration);
    }
}
