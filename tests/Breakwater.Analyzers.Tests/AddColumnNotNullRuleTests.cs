using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW006 (AddColumn not-null without default on an existing table).</summary>
public class AddColumnNotNullRuleTests
{
    [Fact]
    public async Task Not_null_without_default_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<int>(name: "Age", table: "Users", nullable: false);
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW006", diagnostic.Id);
        Assert.Contains("'Users.Age'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Nullable_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<int>(name: "Age", table: "Users", nullable: true);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task With_a_constant_default_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<int>(name: "Age", table: "Users", nullable: false, defaultValue: 0);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task With_a_default_value_sql_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<int>(name: "Age", table: "Users", nullable: false, defaultValueSql: "0");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            migrationBuilder.AddColumn<int>(name: "Age", table: "Users", nullable: false);
            """));

        Assert.Empty(diagnostics);
    }
}
