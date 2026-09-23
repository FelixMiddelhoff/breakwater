using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW015 (AlterColumn collation change).</summary>
public class AlterColumnCollationRuleTests
{
    [Fact]
    public async Task Collation_change_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Name", table: "Users", collation: "Latin1_General_CI_AS", oldCollation: "SQL_Latin1_General_CP1_CI_AS");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW015", diagnostic.Id);
        Assert.Contains("'Users.Name'", diagnostic.GetMessage());
        Assert.Contains("SQL_Latin1_General_CP1_CI_AS", diagnostic.GetMessage());
        Assert.Contains("Latin1_General_CI_AS", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Same_collation_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Name", table: "Users", collation: "Latin1_General_CI_AS", oldCollation: "Latin1_General_CI_AS");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task No_collation_arguments_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Name", table: "Users", maxLength: 100, oldMaxLength: 100);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Only_the_new_collation_known_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Name", table: "Users", collation: "Latin1_General_CI_AS");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            migrationBuilder.AlterColumn<string>(name: "Name", table: "Users", collation: "Latin1_General_CI_AS", oldCollation: "SQL_Latin1_General_CP1_CI_AS");
            """));

        Assert.Empty(diagnostics);
    }
}
