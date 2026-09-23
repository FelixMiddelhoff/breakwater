using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW004 (AlterColumn type change / length-precision narrowing).</summary>
public class AlterColumnTypeChangeRuleTests
{
    [Fact]
    public async Task Type_change_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<int>(name: "Age", table: "Users", oldClrType: typeof(string));
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW004", diagnostic.Id);
        Assert.Contains("'Users.Age'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Length_narrowing_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", maxLength: 50, oldMaxLength: 100);
            """));

        Assert.Equal("BW004", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Precision_narrowing_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<decimal>(name: "Price", table: "Orders", precision: 8, oldPrecision: 12);
            """));

        Assert.Equal("BW004", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Scale_narrowing_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<decimal>(name: "Price", table: "Orders", scale: 2, oldScale: 4);
            """));

        Assert.Equal("BW004", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Length_widening_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", maxLength: 200, oldMaxLength: 100);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Same_type_and_length_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", maxLength: 100, oldMaxLength: 100, oldClrType: typeof(string));
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Missing_old_values_stays_silent_because_narrowing_cannot_be_told()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", maxLength: 50);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            migrationBuilder.AlterColumn<int>(name: "Age", table: "Users", oldClrType: typeof(string));
            """));

        Assert.Empty(diagnostics);
    }
}
