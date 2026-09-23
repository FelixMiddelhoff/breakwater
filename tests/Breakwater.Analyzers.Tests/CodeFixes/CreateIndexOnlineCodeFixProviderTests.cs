using System.Threading.Tasks;
using Breakwater.Analyzers.CodeFixes;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests.CodeFixes;

/// <summary>BW007's two code fixes: PostgreSQL concurrent build, SQL Server online build.</summary>
public class CreateIndexOnlineCodeFixProviderTests
{
    [Fact]
    public async Task Postgres_fix_adds_annotation_and_suppress_transaction()
    {
        var source = MigrationSnippet.WithUp("""
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" });
            """);

        var fixedSource = await CodeFixRunner.ApplyFixAsync(
            source, new CreateIndexOnlineCodeFixProvider(), "BW007", "BW007_Postgres");

        Assert.Contains("migrationBuilder.SuppressTransaction = true;", fixedSource);
        Assert.Contains(".Annotation(\"Npgsql:CreatedConcurrently\", true)", fixedSource);
    }

    [Fact]
    public async Task Postgres_fix_does_not_duplicate_an_existing_suppress_transaction()
    {
        var source = MigrationSnippet.WithUp("""
            migrationBuilder.SuppressTransaction = true;
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" });
            """);

        var fixedSource = await CodeFixRunner.ApplyFixAsync(
            source, new CreateIndexOnlineCodeFixProvider(), "BW007", "BW007_Postgres");

        var occurrences = CountOccurrences(fixedSource, "SuppressTransaction = true");
        Assert.Equal(1, occurrences);
        Assert.Contains(".Annotation(\"Npgsql:CreatedConcurrently\", true)", fixedSource);
    }

    [Fact]
    public async Task Postgres_fix_is_idempotent_and_the_result_no_longer_triggers_BW007()
    {
        var source = MigrationSnippet.WithUp("""
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" });
            """);

        var fixedSource = await CodeFixRunner.ApplyFixAsync(
            source, new CreateIndexOnlineCodeFixProvider(), "BW007", "BW007_Postgres");

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(fixedSource);
        Assert.DoesNotContain(diagnostics, d => d.Id == "BW007");
    }

    [Fact]
    public async Task Sql_server_fix_adds_only_the_online_annotation()
    {
        var source = MigrationSnippet.WithUp("""
            migrationBuilder.CreateIndex(name: "IX_Users_Email", table: "Users", columns: new[] { "Email" });
            """);

        var fixedSource = await CodeFixRunner.ApplyFixAsync(
            source, new CreateIndexOnlineCodeFixProvider(), "BW007", "BW007_SqlServer");

        Assert.Contains(".Annotation(\"SqlServer:Online\", true)", fixedSource);
        Assert.DoesNotContain("SuppressTransaction", fixedSource);

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(fixedSource);
        Assert.DoesNotContain(diagnostics, d => d.Id == "BW007");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
