using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW029 (suppressTransaction / MySQL migration with several operations).</summary>
public class PartialFailureRiskRuleTests
{
    [Fact]
    public async Task SuppressTransaction_with_several_operations_is_reported_once()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.SuppressTransaction = true;
            migrationBuilder.DropColumn("A", "T1");
            migrationBuilder.DropColumn("B", "T1");
            """));

        Assert.Single(diagnostics, d => d.Id == "BW029");
    }

    [Fact]
    public async Task SuppressTransaction_with_a_single_operation_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.SuppressTransaction = true;
            migrationBuilder.DropColumn("A", "T1");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW029");
    }

    [Fact]
    public async Task MySql_migration_with_several_operations_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsMySql())
            {
                migrationBuilder.Sql("ALTER TABLE t1 ADD COLUMN a int;");
            }
            migrationBuilder.DropColumn("B", "T1");
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW029");
    }

    [Fact]
    public async Task Several_operations_without_suppress_transaction_or_MySql_stay_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropColumn("A", "T1");
            migrationBuilder.DropColumn("B", "T1");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW029");
    }
}
