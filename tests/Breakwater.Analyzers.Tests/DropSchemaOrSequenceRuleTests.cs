using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW018 (DropSchema/DropSequence/AlterSequence/RestartSequence) and BW021 (AlterDatabase).</summary>
public class DropSchemaOrSequenceRuleTests
{
    [Fact]
    public async Task DropSchema_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropSchema("legacy");"""));

        Assert.Equal("BW018", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task DropSequence_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropSequence("OrderNumbers");"""));

        Assert.Equal("BW018", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task AlterSequence_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.AlterSequence("OrderNumbers", incrementBy: 5);"""));

        Assert.Equal("BW018", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task RestartSequence_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.RestartSequence("OrderNumbers", startValue: 1000);"""));

        Assert.Equal("BW018", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task CreateTable_is_not_reported_by_BW018()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.CreateTable("Orders");"""));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task AlterDatabase_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.AlterDatabase();"""));

        Assert.Equal("BW021", Assert.Single(diagnostics).Id);
    }
}
