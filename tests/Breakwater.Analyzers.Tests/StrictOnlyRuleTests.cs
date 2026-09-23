using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>
/// BW011, BW020 and BW030 are "off (strict profile only)" per the quality policy: disabled by
/// default (<c>isEnabledByDefault: false</c>), so Roslyn itself suppresses them under the default
/// analyzer configuration these tests run with, regardless of what their Check() logic would say.
/// These tests confirm that off-by-default behavior for a scenario that would otherwise fire.
/// </summary>
public class StrictOnlyRuleTests
{
    [Fact]
    public async Task BW011_empty_Down_stays_silent_by_default()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithDown(string.Empty));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW011");
    }

    [Fact]
    public async Task BW020_postgres_ddl_without_lock_timeout_stays_silent_by_default()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.DropColumn("Email", "Users");
            }
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW020");
    }

    [Fact]
    public async Task BW030_cascade_delete_stays_silent_by_default()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddForeignKey("FK_Orders_Owner", "Orders", new[] { "OwnerId" }, "Owners", onDelete: 1);
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW030");
    }
}
