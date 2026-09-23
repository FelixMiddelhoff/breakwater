using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW027 (CreateIndex(unique: true) on an existing table).</summary>
public class CreateUniqueIndexRuleTests
{
    [Fact]
    public async Task Unique_index_on_a_column_added_nullable_this_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<string>("Code", "Users", nullable: true);
            migrationBuilder.CreateIndex("IX_Users_Code", "Users", new[] { "Code" }, unique: true);
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW027");
    }

    [Fact]
    public async Task Unique_index_on_a_table_created_this_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable("Users");
            migrationBuilder.CreateIndex("IX_Users_Code", "Users", new[] { "Code" }, unique: true);
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW027");
    }

    [Fact]
    public async Task Unique_index_on_an_existing_column_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateIndex("IX_Users_Code", "Users", new[] { "Code" }, unique: true);
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW027");
    }
}
