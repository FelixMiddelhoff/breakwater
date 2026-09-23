using System.Linq;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW022 (several risky operations on the same table) and BW029 (no transaction to roll back to).</summary>
public class MixedRiskyOperationsRuleTests
{
    [Fact]
    public async Task Three_risky_operations_where_the_last_has_no_more_specific_finding_adds_BW022()
    {
        // The first two are risky and each has its own (more specific) finding (BW017); the
        // third is risky (counts toward the table's total) but nullable with no default, so no
        // other rule has anything to say about it - exactly where BW022's "nothing more specific
        // fired" condition lets it surface.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropCheckConstraint("CK_Old", "Orders");
            migrationBuilder.DropUniqueConstraint("UQ_Old", "Orders");
            migrationBuilder.AddColumn<int>("Note", "Orders", nullable: true);
            """));

        var last = diagnostics.Last();
        Assert.Equal("BW022", last.Id);
    }

    [Fact]
    public async Task Two_risky_operations_on_the_same_table_do_not_report_BW022()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropCheckConstraint("CK_Old", "Orders");
            migrationBuilder.DropUniqueConstraint("UQ_Old", "Orders");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW022");
    }

    [Fact]
    public async Task Risky_operations_on_a_table_created_in_this_migration_do_not_count_toward_BW022()
    {
        // Corpus-scan finding (M10): a table created and then indexed/keyed several times in the
        // same Initial migration - the single most common real-world shape - was miscounted as
        // "risky" even though the table has no existing data to lock or violate, per
        // breakwater-quality-policy.md principle 4. CreateIndex/AddForeignKey/AddUniqueConstraint
        // on "Orders" here must not add up toward the Threshold since "Orders" was created a few
        // lines above in the same Up method.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Orders");
            migrationBuilder.CreateIndex(name: "IX_Orders_CustomerId", table: "Orders", columns: new[] { "CustomerId" });
            migrationBuilder.AddForeignKey(name: "FK_Orders_Customers", table: "Orders", columns: new[] { "CustomerId" }, principalTable: "Customers");
            migrationBuilder.AddUniqueConstraint(name: "UQ_Orders_Number", table: "Orders", columns: new[] { "Number" });
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW022");
    }

    [Fact]
    public async Task BW022_does_not_appear_when_a_more_specific_rule_already_fired_on_that_operation()
    {
        // DropColumn is itself risky (counts toward the table's total) but always already has
        // BW001 fire on it, so BW022 must never additionally fire on that same DropColumn call.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropCheckConstraint("CK_Old", "Orders");
            migrationBuilder.DropUniqueConstraint("UQ_Old", "Orders");
            migrationBuilder.DropColumn("Notes", "Orders");
            """));

        var lastDiagnostics = diagnostics.Where(d => d.Location.SourceSpan == diagnostics.Last().Location.SourceSpan).ToList();
        Assert.DoesNotContain(lastDiagnostics, d => d.Id == "BW022");
    }
}
