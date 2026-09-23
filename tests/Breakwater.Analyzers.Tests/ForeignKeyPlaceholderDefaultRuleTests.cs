using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW025 (AddForeignKey on a column added this migration with a placeholder default).</summary>
public class ForeignKeyPlaceholderDefaultRuleTests
{
    [Fact]
    public async Task AddForeignKey_on_a_column_added_with_zero_default_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<int>("OwnerId", "Orders", defaultValue: 0);
            migrationBuilder.AddForeignKey("FK_Orders_Owner", "Orders", new[] { "OwnerId" }, "Owners");
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW025");
    }

    [Fact]
    public async Task AddForeignKey_on_a_column_added_with_empty_string_default_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<string>("OwnerCode", "Orders", defaultValue: "");
            migrationBuilder.AddForeignKey("FK_Orders_Owner", "Orders", new[] { "OwnerCode" }, "Owners");
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW025");
    }

    [Fact]
    public async Task AddForeignKey_on_a_column_with_a_real_default_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<int>("OwnerId", "Orders", defaultValue: 42);
            migrationBuilder.AddForeignKey("FK_Orders_Owner", "Orders", new[] { "OwnerId" }, "Owners");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW025");
    }

    [Fact]
    public async Task AddForeignKey_on_a_pre_existing_column_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddForeignKey("FK_Orders_Owner", "Orders", new[] { "OwnerId" }, "Owners");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW025");
    }
}
