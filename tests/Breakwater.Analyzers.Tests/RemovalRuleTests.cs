using System.Linq;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW001 (DropColumn) and BW002 (DropTable).</summary>
public class RemovalRuleTests
{
    [Fact]
    public async Task DropColumn_is_reported_with_table_and_column()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropColumn(name: "Email", table: "Users");"""));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW001", diagnostic.Id);
        Assert.Contains("'Users.Email'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task DropColumn_with_schema_names_the_schema_in_the_message()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropColumn("Email", "Users", "auth");"""));

        Assert.Contains("'auth.Users.Email'", Assert.Single(diagnostics).GetMessage());
    }

    [Fact]
    public async Task DropColumn_with_reordered_named_arguments_is_read_correctly()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropColumn(table: "Users", schema: "auth", name: "Email");"""));

        Assert.Contains("'auth.Users.Email'", Assert.Single(diagnostics).GetMessage());
    }

    [Fact]
    public async Task DropColumn_with_constant_expressions_is_resolved()
    {
        var source = MigrationSnippet.WithUp(
            "migrationBuilder.DropColumn(nameof(Legacy), TableName + \"s\");",
            "private const string TableName = \"User\"; private const string Legacy = \"x\";");

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Contains("'Users.Legacy'", Assert.Single(diagnostics).GetMessage());
    }

    [Fact]
    public async Task DropColumn_with_a_name_that_is_not_constant_is_still_reported()
    {
        var source = MigrationSnippet.WithUp(
            "migrationBuilder.DropColumn(ColumnName(), \"Users\");",
            "private static string ColumnName() => \"Email\";");

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Contains("'Users.?'", Assert.Single(diagnostics).GetMessage());
    }

    [Fact]
    public async Task DropTable_is_reported_with_the_table_name()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.DropTable(name: "Orders", schema: "shop");"""));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW002", diagnostic.Id);
        Assert.Contains("'shop.Orders'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Several_operations_are_all_reported_in_source_order()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.DropColumn("A", "T1");
            migrationBuilder.DropTable("T2");
            migrationBuilder.DropColumn("B", "T3");
            """));

        Assert.Equal(new[] { "BW001", "BW002", "BW001" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Operations_inside_control_flow_and_local_functions_are_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.GetType() != null)
            {
                foreach (var table in new[] { "A", "B" })
                {
                    migrationBuilder.DropColumn("Old", table);
                }
            }

            void Cleanup() => migrationBuilder.DropTable("Temp");
            Cleanup();
            """));

        Assert.Equal(new[] { "BW001", "BW002" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task The_migration_builder_parameter_may_have_any_name()
    {
        var source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            [Migration("20260101000000_Renamed")]
            public class Renamed : Migration
            {
                protected override void Up(MigrationBuilder mb) { mb.DropTable("Orders"); }
            }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Equal("BW002", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Operations_in_Down_are_not_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithDown("""migrationBuilder.DropColumn("Email", "Users"); migrationBuilder.DropTable("Orders");"""));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Methods_with_the_same_name_on_other_types_are_ignored()
    {
        var source = """
            public class Schema { public void DropColumn(string name, string table) { } public void DropTable(string name) { } }

            public class NotAMigration
            {
                public void Run(Schema schema) { schema.DropColumn("Email", "Users"); schema.DropTable("Orders"); }
            }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Projects_without_EF_Core_are_ignored()
    {
        var source = """
            public class Plain { public void Run() { } }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source, includeEfCoreStubs: false);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task An_empty_migration_is_not_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp(string.Empty));

        Assert.Empty(diagnostics);
    }
}
