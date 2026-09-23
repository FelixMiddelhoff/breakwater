using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW005 (AlterColumn nullable to not-null without default/backfill).</summary>
public class AlterColumnNotNullRuleTests
{
    [Fact]
    public async Task Nullable_to_not_null_without_default_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", nullable: false, oldNullable: true);
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW005", diagnostic.Id);
        Assert.Contains("'Users.Email'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task With_a_default_value_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", nullable: false, oldNullable: true, defaultValue: "");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task With_a_default_value_sql_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", nullable: false, oldNullable: true, defaultValueSql: "''");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Already_not_null_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", nullable: false, oldNullable: false);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Staying_nullable_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", nullable: true, oldNullable: true);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", nullable: false, oldNullable: true);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Type_change_and_not_null_tightening_on_the_same_call_report_only_the_more_specific_rule()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<int>(name: "Age", table: "Users", oldClrType: typeof(string), nullable: false, oldNullable: true);
            """));

        Assert.Equal("BW004", Assert.Single(diagnostics).Id);
    }
}
