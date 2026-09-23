using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>
/// BW017 (DropPrimaryKey, DropUniqueConstraint, DropCheckConstraint, DropForeignKey).
/// </summary>
public class DropConstraintRuleTests
{
    [Fact]
    public async Task Drop_primary_key_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropPrimaryKey(name: "PK_Users", table: "Users");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW017", diagnostic.Id);
        Assert.Contains("DropPrimaryKey", diagnostic.GetMessage());
        Assert.Contains("'PK_Users'", diagnostic.GetMessage());
        Assert.Contains("'Users'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Drop_unique_constraint_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropUniqueConstraint(name: "AK_Users_Email", table: "Users");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW017", diagnostic.Id);
        Assert.Contains("DropUniqueConstraint", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Drop_check_constraint_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropCheckConstraint(name: "CK_Users_Age", table: "Users");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW017", diagnostic.Id);
        Assert.Contains("DropCheckConstraint", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Drop_foreign_key_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropForeignKey(name: "FK_Orders_Users", table: "Orders");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW017", diagnostic.Id);
        Assert.Contains("DropForeignKey", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Drop_on_a_table_created_in_the_same_migration_still_fires()
    {
        // Dropping a constraint from a table created moments ago in the same migration is
        // still worth flagging (it usually means the migration is undoing itself), so BW017
        // has no new-table silence unlike BW004-BW009.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            migrationBuilder.DropPrimaryKey(name: "PK_Users", table: "Users");
            """));

        Assert.Equal("BW017", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Other_operations_stay_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            """));

        Assert.Empty(diagnostics);
    }
}
