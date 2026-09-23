using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW014 (Postgres AlterColumn changing between two non-primitive/mapped types, e.g. a mapped enum).</summary>
public class ProviderSpecificTypeChangeRuleTests
{
    [Fact]
    public async Task Postgres_AlterColumn_between_two_custom_types_is_reported()
    {
        var source = MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.AlterColumn<NewStatus>("Status", "Orders", oldClrType: typeof(OldStatus));
            }
            """, "public enum OldStatus { A } public enum NewStatus { B }");

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        // BW004 (any CLR type change) also fires on the same call; the two describe different
        // aspects (general narrowing risk vs. the Postgres-specific rewrite risk) and are not
        // suppressed against each other.
        Assert.Contains(diagnostics, d => d.Id == "BW014");
    }

    [Fact]
    public async Task Postgres_AlterColumn_between_primitive_types_is_not_reported_by_BW014()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.AlterColumn<long>("Amount", "Orders", oldClrType: typeof(int));
            }
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW014");
    }

    [Fact]
    public async Task Without_a_known_postgres_guard_stays_silent_for_BW014()
    {
        var source = MigrationSnippet.WithUp(
            """migrationBuilder.AlterColumn<NewStatus>("Status", "Orders", oldClrType: typeof(OldStatus));""",
            "public enum OldStatus { A } public enum NewStatus { B }");

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW014");
    }
}
