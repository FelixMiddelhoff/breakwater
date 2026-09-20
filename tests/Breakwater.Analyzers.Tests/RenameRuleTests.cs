using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW003 (RenameColumn and RenameTable).</summary>
public class RenameRuleTests
{
    [Fact]
    public async Task RenameColumn_is_reported_with_old_and_new_name()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.RenameColumn(name: "Mail", table: "Users", newName: "Email");"""));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW003", diagnostic.Id);
        Assert.Equal("RenameColumn 'Users.Mail' to 'Email' breaks application versions that still use the old name", diagnostic.GetMessage());
    }

    [Fact]
    public async Task RenameTable_is_reported_with_old_and_new_name()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.RenameTable(name: "Order", newName: "Orders", schema: "shop");"""));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW003", diagnostic.Id);
        Assert.Equal("RenameTable 'shop.Order' to 'Orders' breaks application versions that still use the old name", diagnostic.GetMessage());
    }

    [Fact]
    public async Task RenameTable_that_only_moves_to_another_schema_has_an_unknown_new_name()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.RenameTable(name: "Order", newSchema: "archive");"""));

        Assert.Contains("to '?'", Assert.Single(diagnostics).GetMessage());
    }

    [Fact]
    public async Task Renames_in_Down_are_not_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithDown("""migrationBuilder.RenameColumn("Email", "Users", "Mail");"""));

        Assert.Empty(diagnostics);
    }
}
