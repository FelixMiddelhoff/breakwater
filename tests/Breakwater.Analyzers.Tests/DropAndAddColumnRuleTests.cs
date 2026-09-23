using System.Linq;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW023 (DropColumn + AddColumn on the same table): suppresses BW001.</summary>
public class DropAndAddColumnRuleTests
{
    [Fact]
    public async Task DropColumn_and_AddColumn_on_the_same_table_reports_BW023_and_suppresses_BW001()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropColumn("OldName", "Users");
            migrationBuilder.AddColumn<int>("NewName", "Users", nullable: true);
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW023", diagnostic.Id);
        Assert.Contains("'Users.OldName'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task DropColumn_alone_still_reports_BW001()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropColumn("OldName", "Users");"""));

        Assert.Equal("BW001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task DropColumn_and_AddColumn_on_different_tables_does_not_report_BW023()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropColumn("OldName", "Users");
            migrationBuilder.AddColumn<int>("NewName", "Orders", nullable: true);
            """));

        Assert.Equal(new[] { "BW001" }, diagnostics.Select(d => d.Id));
    }
}
