using System.Collections.Generic;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>
/// Standard Roslyn suppression (#pragma, [SuppressMessage]) alongside Breakwater's own
/// <c>// breakwater: allow BWxxx &lt;reason&gt;</c> comment convention.
/// </summary>
public class SuppressionTests
{
    [Fact]
    public async Task Pragma_disable_suppresses_the_diagnostic()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            #pragma warning disable BW001
            migrationBuilder.DropColumn("Email", "Users");
            #pragma warning restore BW001
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW001");
    }

    [Fact]
    public async Task Pragma_disable_for_a_different_rule_does_not_suppress_this_one()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            #pragma warning disable BW002
            migrationBuilder.DropColumn("Email", "Users");
            #pragma warning restore BW002
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW001");
    }

    [Fact]
    public async Task SuppressMessage_attribute_suppresses_the_diagnostic()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync("""
            using Microsoft.EntityFrameworkCore.Migrations;

            [Migration("20260101000000_AddThings")]
            public class AddThings : Migration
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("Migration", "BW001")]
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn("Email", "Users");
                }
            }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW001");
    }

    [Fact]
    public async Task Comment_convention_with_a_reason_suppresses_the_diagnostic()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            // breakwater: allow BW001 column unused since v3, safe to drop
            migrationBuilder.DropColumn("Email", "Users");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW001");
    }

    [Fact]
    public async Task Comment_convention_is_case_insensitive_on_the_rule_id()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            // breakwater: allow bw001 column unused since v3, safe to drop
            migrationBuilder.DropColumn("Email", "Users");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW001");
    }

    [Fact]
    public async Task Comment_convention_with_no_reason_still_fires_and_says_a_reason_is_required()
    {
        var source = MigrationSnippet.WithUp("""
            // breakwater: allow BW001
            migrationBuilder.DropColumn("Email", "Users");
            """);
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        var finding = Assert.Single(diagnostics, d => d.Id == "BW001");
        Assert.Contains("non-empty reason", finding.GetMessage());
    }

    [Fact]
    public async Task Comment_convention_with_only_whitespace_reason_still_fires_and_says_a_reason_is_required()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            // breakwater: allow BW001
            migrationBuilder.DropColumn("Email", "Users");
            """));

        var finding = Assert.Single(diagnostics, d => d.Id == "BW001");
        Assert.Contains("non-empty reason", finding.GetMessage());
    }

    [Fact]
    public async Task Comment_convention_for_a_different_rule_does_not_suppress_this_one()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            // breakwater: allow BW002 unrelated reason
            migrationBuilder.DropColumn("Email", "Users");
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW001");
    }

    [Fact]
    public async Task No_comment_reports_normally()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropColumn("Email", "Users");
            """));

        Assert.Contains(diagnostics, d => d.Id == "BW001");
    }
}

/// <summary>
/// <c>breakwater_profile = recommended | strict</c> (default <c>recommended</c>). Strict enables
/// the "Off (strict)" tier: BW011, BW020, BW030.
/// </summary>
public class BreakwaterProfileTests
{
    [Fact]
    public async Task Default_profile_leaves_BW020_off()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.AddColumn<string>("Status", "Orders", nullable: true);
            }
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW020");
    }

    [Fact]
    public async Task Strict_profile_enables_BW020()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""
                if (migrationBuilder.IsNpgsql())
                {
                    migrationBuilder.AddColumn<string>("Status", "Orders", nullable: true);
                }
                """),
            globalOptions: new Dictionary<string, string> { ["breakwater_profile"] = "strict" });

        Assert.Contains(diagnostics, d => d.Id == "BW020");
    }

    [Fact]
    public async Task Strict_profile_enables_BW030()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.AddForeignKey("FK_Orders_Owner", "Orders", new[] { "OwnerId" }, "Owners", onDelete: 1);"""),
            globalOptions: new Dictionary<string, string> { ["breakwater_profile"] = "strict" });

        Assert.Contains(diagnostics, d => d.Id == "BW030");
    }

    [Fact]
    public async Task Strict_profile_enables_BW011()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithDown(string.Empty),
            globalOptions: new Dictionary<string, string> { ["breakwater_profile"] = "strict" });

        Assert.Contains(diagnostics, d => d.Id == "BW011");
    }

    [Fact]
    public async Task Recommended_profile_leaves_BW011_off()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithDown(string.Empty),
            globalOptions: new Dictionary<string, string> { ["breakwater_profile"] = "recommended" });

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW011");
    }

    [Fact]
    public async Task Unrecognized_profile_value_falls_back_to_recommended()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithDown(string.Empty),
            globalOptions: new Dictionary<string, string> { ["breakwater_profile"] = "bogus" });

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW011");
    }

    [Fact]
    public async Task Strict_profile_does_not_change_a_warning_tier_rule()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropColumn("Email", "Users");"""),
            globalOptions: new Dictionary<string, string> { ["breakwater_profile"] = "strict" });

        Assert.Contains(diagnostics, d => d.Id == "BW001");
    }
}
