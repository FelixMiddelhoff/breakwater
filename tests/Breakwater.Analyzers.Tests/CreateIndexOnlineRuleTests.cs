using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW007 (CreateIndex on an existing table without an online/concurrent build).</summary>
public class CreateIndexOnlineRuleTests
{
    [Fact]
    public async Task Plain_index_on_existing_table_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" });
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW007", diagnostic.Id);
        Assert.Contains("'IX_Users_Email'", diagnostic.GetMessage());
        Assert.Contains("'Users'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Unique_index_on_existing_table_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" }, unique: true);
            """));

        Assert.Equal("BW007", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Postgres_concurrent_with_suppress_transaction_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.SuppressTransaction = true;
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" })
                .Annotation("Npgsql:CreatedConcurrently", true);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Postgres_concurrent_annotation_without_suppress_transaction_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" })
                .Annotation("Npgsql:CreatedConcurrently", true);
            """));

        Assert.Equal("BW007", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Sql_server_online_annotation_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" })
                .Annotation("SqlServer:Online", true);
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Unrelated_annotation_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" })
                .Annotation("SomeOther:Thing", true);
            """));

        Assert.Equal("BW007", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" });
            """));

        Assert.Empty(diagnostics);
    }
}
