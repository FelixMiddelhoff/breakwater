using System.Linq;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW024 (DeleteData) and BW012/BW013 (data-volume and schema+data mixing).</summary>
public class DeleteDataRuleTests
{
    [Fact]
    public async Task DeleteData_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DeleteData("Roles", "Id", new object[] { 1 });
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW024", diagnostic.Id);
        Assert.Contains("'Roles'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task InsertData_is_not_reported_by_BW024()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.InsertData("Roles", new[] { "Id" }, new object[,] { { 1 } });
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task InsertData_with_many_rows_is_reported_by_BW012()
    {
        var values = string.Join(", ", System.Linq.Enumerable.Range(0, 51).Select(i => "{ " + i + " }"));
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp($$"""
            migrationBuilder.InsertData("Roles", new[] { "Id" }, new object[,] { {{values}} });
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW012", diagnostic.Id);
    }

    [Fact]
    public async Task InsertData_with_few_rows_stays_silent_for_BW012()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.InsertData("Roles", new[] { "Id" }, new object[,] { { 1 }, { 2 } });
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task DeleteData_alongside_a_schema_change_is_reported_by_BW013()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<int>("NewCol", "Roles", nullable: true);
            migrationBuilder.DeleteData("Roles", "Id", new object[] { 1 });
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW013");
    }

    [Fact]
    public async Task DeleteData_alone_is_not_reported_by_BW013()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DeleteData("Roles", "Id", new object[] { 1 });
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW013");
    }
}
