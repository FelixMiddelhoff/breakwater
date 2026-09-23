using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW008 (AddForeignKey/AddCheckConstraint on an existing table).</summary>
public class AddValidatingConstraintRuleTests
{
    [Fact]
    public async Task Add_foreign_key_on_existing_table_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddForeignKey(name: "FK_Orders_Users", table: "Orders", columns: new[] { "UserId" }, principalTable: "Users");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW008", diagnostic.Id);
        Assert.Contains("AddForeignKey", diagnostic.GetMessage());
        Assert.Contains("'FK_Orders_Users'", diagnostic.GetMessage());
        Assert.Contains("'Orders'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Add_check_constraint_on_existing_table_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddCheckConstraint(name: "CK_Users_Age", table: "Users", sql: "Age >= 0");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW008", diagnostic.Id);
        Assert.Contains("AddCheckConstraint", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Foreign_key_on_table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Orders");
            migrationBuilder.AddForeignKey(name: "FK_Orders_Users", table: "Orders", columns: new[] { "UserId" }, principalTable: "Users");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Check_constraint_on_table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            migrationBuilder.AddCheckConstraint(name: "CK_Users_Age", table: "Users", sql: "Age >= 0");
            """));

        Assert.Empty(diagnostics);
    }
}
