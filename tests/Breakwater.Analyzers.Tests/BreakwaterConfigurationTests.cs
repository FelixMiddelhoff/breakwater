using System.Collections.Generic;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>
/// <c>breakwater_provider = sqlserver | postgres | sqlite | mysql | auto</c> (default <c>auto</c>).
/// An explicit override is trusted outright and skips the <c>IsNpgsql()</c>/<c>IsMySql()</c>
/// guard-detection heuristic; <c>auto</c> keeps the heuristic and adds provider-annotation detection.
/// </summary>
public class BreakwaterProviderTests
{
    [Fact]
    public async Task Auto_without_a_guard_or_annotation_leaves_BW016_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<System.DateTime>("CreatedAt", "Orders", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW016");
    }

    [Fact]
    public async Task Postgres_override_makes_BW016_fire_without_the_IsNpgsql_guard()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""
                migrationBuilder.AddColumn<System.DateTime>("CreatedAt", "Orders", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP");
                """),
            globalOptions: new Dictionary<string, string> { ["breakwater_provider"] = "postgres" });

        Assert.Contains(diagnostics, d => d.Id == "BW016");
    }

    [Fact]
    public async Task Mysql_override_makes_BW029_fire_without_the_IsMySql_guard()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""
                migrationBuilder.DropColumn("Email", "Users");
                migrationBuilder.DropColumn("Phone", "Users");
                """),
            globalOptions: new Dictionary<string, string> { ["breakwater_provider"] = "mysql" });

        Assert.Contains(diagnostics, d => d.Id == "BW029");
    }

    [Fact]
    public async Task Sqlserver_override_still_fires_BW031_for_a_GO_separator()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.Sql("ALTER TABLE Orders ADD Foo int\nGO");"""),
            globalOptions: new Dictionary<string, string> { ["breakwater_provider"] = "sqlserver" });

        Assert.Contains(diagnostics, d => d.Id == "BW031");
    }

    [Fact]
    public async Task Postgres_override_silences_BW031_for_a_GO_separator()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.Sql("ALTER TABLE Orders ADD Foo int\nGO");"""),
            globalOptions: new Dictionary<string, string> { ["breakwater_provider"] = "postgres" });

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW031");
    }

    [Fact]
    public async Task Existing_IsNpgsql_guard_still_works_under_auto()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.AddColumn<System.DateTime>("CreatedAt", "Orders", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP");
            }
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW016");
    }

    [Fact]
    public async Task Unrecognized_provider_value_falls_back_to_auto_with_no_crash()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""
                migrationBuilder.AddColumn<System.DateTime>("CreatedAt", "Orders", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP");
                """),
            globalOptions: new Dictionary<string, string> { ["breakwater_provider"] = "bogus" });

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW999");
        Assert.DoesNotContain(diagnostics, d => d.Id == "BW016");
    }
}

/// <summary>
/// <c>breakwater_deploy_model = rolling | downtime_ok</c> (default <c>rolling</c>). Downgrades
/// BW001/BW002/BW003 to Info when the old app version is not running during the migration.
/// </summary>
public class BreakwaterDeployModelTests
{
    [Fact]
    public async Task Default_rolling_model_reports_BW001_at_warning()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropColumn("Email", "Users");
            """));

        var finding = Assert.Single(diagnostics, d => d.Id == "BW001");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, finding.Severity);
    }

    [Fact]
    public async Task Downtime_ok_downgrades_BW001_to_info()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropColumn("Email", "Users");"""),
            globalOptions: new Dictionary<string, string> { ["breakwater_deploy_model"] = "downtime_ok" });

        var finding = Assert.Single(diagnostics, d => d.Id == "BW001");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Info, finding.Severity);
    }

    [Fact]
    public async Task Downtime_ok_downgrades_BW002_to_info()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropTable("Users");"""),
            globalOptions: new Dictionary<string, string> { ["breakwater_deploy_model"] = "downtime_ok" });

        var finding = Assert.Single(diagnostics, d => d.Id == "BW002");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Info, finding.Severity);
    }

    [Fact]
    public async Task Downtime_ok_does_not_change_an_unrelated_rule()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.AddColumn<string>("Status", "Orders", nullable: false);"""),
            globalOptions: new Dictionary<string, string> { ["breakwater_deploy_model"] = "downtime_ok" });

        var finding = Assert.Single(diagnostics, d => d.Id == "BW006");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, finding.Severity);
    }

    [Fact]
    public async Task Unrecognized_deploy_model_value_falls_back_to_rolling_with_no_crash()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropColumn("Email", "Users");"""),
            globalOptions: new Dictionary<string, string> { ["breakwater_deploy_model"] = "bogus" });

        var finding = Assert.Single(diagnostics, d => d.Id == "BW001");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, finding.Severity);
    }
}

/// <summary>
/// <c>breakwater_since_migration = &lt;migration id&gt;</c>: migrations with an id lexicographically
/// less than or equal to the configured value are skipped entirely.
/// </summary>
public class BreakwaterSinceMigrationTests
{
    private const string OldMigration = """
        using Microsoft.EntityFrameworkCore.Migrations;

        [Migration("20200101000000_Old")]
        public class Old : Migration
        {
            protected override void Up(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.DropColumn("Email", "Users");
            }
        }
        """;

    private const string NewMigration = """
        using Microsoft.EntityFrameworkCore.Migrations;

        [Migration("20260101000000_New")]
        public class New : Migration
        {
            protected override void Up(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.DropColumn("Email", "Users");
            }
        }
        """;

    [Fact]
    public async Task Migration_at_or_before_the_configured_id_is_skipped_entirely()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            OldMigration,
            globalOptions: new Dictionary<string, string> { ["breakwater_since_migration"] = "20250101000000_Cutoff" });

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Migration_exactly_at_the_configured_id_is_skipped()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            OldMigration,
            globalOptions: new Dictionary<string, string> { ["breakwater_since_migration"] = "20200101000000_Old" });

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Migration_after_the_configured_id_is_still_analyzed()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            NewMigration,
            globalOptions: new Dictionary<string, string> { ["breakwater_since_migration"] = "20250101000000_Cutoff" });

        Assert.Contains(diagnostics, d => d.Id == "BW001");
    }

    [Fact]
    public async Task No_since_migration_configured_analyzes_everything()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(OldMigration);

        Assert.Contains(diagnostics, d => d.Id == "BW001");
    }
}

/// <summary>
/// <c>breakwater_small_tables = Users, Settings</c>: table-lock rules downgrade to Info for
/// operations on listed tables, matched case-insensitively.
/// </summary>
public class BreakwaterSmallTablesTests
{
    [Fact]
    public async Task Listed_table_downgrades_BW004_to_info()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""
                migrationBuilder.AlterColumn<int>("Amount", "Users", oldClrType: typeof(string));
                """),
            globalOptions: new Dictionary<string, string> { ["breakwater_small_tables"] = "Users, Settings" });

        var finding = Assert.Single(diagnostics, d => d.Id == "BW004");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Info, finding.Severity);
    }

    [Fact]
    public async Task Table_name_matching_is_case_insensitive()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""
                migrationBuilder.AlterColumn<int>("Amount", "USERS", oldClrType: typeof(string));
                """),
            globalOptions: new Dictionary<string, string> { ["breakwater_small_tables"] = "users" });

        var finding = Assert.Single(diagnostics, d => d.Id == "BW004");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Info, finding.Severity);
    }

    [Fact]
    public async Task Unlisted_table_stays_at_warning()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""
                migrationBuilder.AlterColumn<int>("Amount", "Orders", oldClrType: typeof(string));
                """),
            globalOptions: new Dictionary<string, string> { ["breakwater_small_tables"] = "Users, Settings" });

        var finding = Assert.Single(diagnostics, d => d.Id == "BW004");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, finding.Severity);
    }

    [Fact]
    public async Task Small_tables_does_not_change_a_non_lock_rule()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropColumn("Email", "Users");"""),
            globalOptions: new Dictionary<string, string> { ["breakwater_small_tables"] = "Users" });

        var finding = Assert.Single(diagnostics, d => d.Id == "BW001");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, finding.Severity);
    }

    [Fact]
    public async Task Empty_small_tables_value_leaves_everything_at_default_severity_with_no_crash()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""
                migrationBuilder.AlterColumn<int>("Amount", "Users", oldClrType: typeof(string));
                """),
            globalOptions: new Dictionary<string, string> { ["breakwater_small_tables"] = "" });

        var finding = Assert.Single(diagnostics, d => d.Id == "BW004");
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, finding.Severity);
    }
}
