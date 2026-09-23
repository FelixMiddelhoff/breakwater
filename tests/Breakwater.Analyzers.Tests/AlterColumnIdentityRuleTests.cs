using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW026 (AlterColumn adds/removes identity or value generation).</summary>
public class AlterColumnIdentityRuleTests
{
    [Fact]
    public async Task AlterColumn_adding_an_identity_annotation_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<int>("Id", "Users", oldClrType: typeof(int))
                .Annotation("SqlServer:Identity", "1, 1");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW026", diagnostic.Id);
        Assert.Contains("'Users.Id'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task AlterColumn_removing_an_identity_annotation_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<int>("Id", "Users", oldClrType: typeof(int))
                .OldAnnotation("SqlServer:Identity", "1, 1");
            """));

        Assert.Equal("BW026", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task AlterColumn_with_the_same_identity_annotation_on_both_sides_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<int>("Id", "Users", maxLength: 10, oldClrType: typeof(int), oldMaxLength: 5)
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW026");
    }

    [Fact]
    public async Task AlterColumn_without_any_identity_annotation_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AlterColumn<int>("Age", "Users", oldClrType: typeof(int));
            """));

        Assert.Empty(diagnostics);
    }
}
