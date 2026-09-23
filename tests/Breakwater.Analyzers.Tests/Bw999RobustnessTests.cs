using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>
/// BW999: the analyzer must never crash a build. AnalyzerRunner itself already turns an AD0001
/// ("analyzer crashed") diagnostic into a thrown exception, so every other test in this suite
/// already proves the analyzer did not crash on its input; these add a few pathological shapes
/// (empty/very unusual operand shapes) most likely to trip up a rule that assumes a value is
/// present.
/// </summary>
public class Bw999RobustnessTests
{
    [Fact]
    public async Task Every_recognized_operation_with_only_its_required_arguments_does_not_crash()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropColumn("A", "T");
            migrationBuilder.DropTable("T");
            migrationBuilder.RenameColumn("A", "T", "B");
            migrationBuilder.RenameTable("T");
            migrationBuilder.CreateTable("T");
            migrationBuilder.AddColumn<int>("A", "T");
            migrationBuilder.AlterColumn<int>("A", "T");
            migrationBuilder.CreateIndex("IX", "T", new[] { "A" });
            migrationBuilder.AddForeignKey("FK", "T", new[] { "A" }, "P");
            migrationBuilder.AddCheckConstraint("CK", "T", "A > 0");
            migrationBuilder.AddUniqueConstraint("UQ", "T", new[] { "A" });
            migrationBuilder.AddPrimaryKey("PK", "T", new[] { "A" });
            migrationBuilder.DropPrimaryKey("PK", "T");
            migrationBuilder.DropUniqueConstraint("UQ", "T");
            migrationBuilder.DropCheckConstraint("CK", "T");
            migrationBuilder.DropForeignKey("FK", "T");
            migrationBuilder.Sql("SELECT 1;");
            migrationBuilder.InsertData("T", "A", new object[] { 1 });
            migrationBuilder.UpdateData("T", "A", new object[] { 1 }, "A", new object[] { 2 });
            migrationBuilder.DeleteData("T", "A", new object[] { 1 });
            migrationBuilder.DropSchema("S");
            migrationBuilder.DropSequence("Seq");
            migrationBuilder.AlterSequence("Seq");
            migrationBuilder.AlterDatabase();
            """));

        // Reaching this line without AnalyzerRunner throwing already proves no AD0001/crash; also
        // confirm no rule reported BW999 for itself.
        Assert.DoesNotContain(diagnostics, d => d.Id == "BW999");
    }

    [Fact]
    public async Task Sql_with_empty_text_does_not_crash()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""migrationBuilder.Sql("");"""));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW999");
    }

    [Fact]
    public async Task Non_constant_names_everywhere_do_not_crash()
    {
        var source = MigrationSnippet.WithUp(
            """migrationBuilder.AddForeignKey(Name(), Table(), Columns(), Table());""",
            "private static string Name() => \"FK\"; private static string Table() => \"T\"; private static string[] Columns() => new[] { \"A\" };");

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW999");
    }
}
