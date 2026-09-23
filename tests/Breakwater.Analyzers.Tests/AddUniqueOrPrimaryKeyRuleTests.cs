using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW009 (AddUniqueConstraint/AddPrimaryKey on an existing table).</summary>
public class AddUniqueOrPrimaryKeyRuleTests
{
    [Fact]
    public async Task Add_unique_constraint_on_existing_table_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddUniqueConstraint(name: "AK_Users_Email", table: "Users", columns: new[] { "Email" });
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW009", diagnostic.Id);
        Assert.Contains("AddUniqueConstraint", diagnostic.GetMessage());
        Assert.Contains("'AK_Users_Email'", diagnostic.GetMessage());
        Assert.Contains("'Users'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Add_primary_key_on_existing_table_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddPrimaryKey(name: "PK_Users", table: "Users", columns: new[] { "Id" });
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW009", diagnostic.Id);
        Assert.Contains("AddPrimaryKey", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Unique_constraint_on_table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            migrationBuilder.AddUniqueConstraint(name: "AK_Users_Email", table: "Users", columns: new[] { "Email" });
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Primary_key_on_table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            migrationBuilder.AddPrimaryKey(name: "PK_Users", table: "Users", columns: new[] { "Id" });
            """));

        Assert.Empty(diagnostics);
    }
}
