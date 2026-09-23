using System.Linq;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW035 (raw SQL disguising a structural schema change: ALTER TABLE DROP/ADD COLUMN).</summary>
public class SqlStructuralChangeRuleTests
{
    [Fact]
    public async Task Alter_table_drop_column_is_reported()
    {
        // The exact real-world shape from bitwarden-mysql's GrantIdWithIndexes.cs.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE `Grant` DROP COLUMN `Id`");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW035", diagnostic.Id);
        Assert.Contains("DROP COLUMN", diagnostic.GetMessage());
        Assert.Contains("Id", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Alter_table_add_column_not_null_with_no_default_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE `Grant` ADD COLUMN `Id` INT NOT NULL");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW035", diagnostic.Id);
        Assert.Contains("ADD COLUMN", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Add_column_not_null_with_a_default_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE Users ADD COLUMN Active INT NOT NULL DEFAULT 0");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Add_column_nullable_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE Users ADD COLUMN Nickname VARCHAR(50) NULL");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Alter_table_add_column_unique_auto_increment_with_no_not_null_stays_silent()
    {
        // The second half of the real bitwarden-mysql pattern: AUTO_INCREMENT/UNIQUE only, no
        // NOT NULL clause - out of BW035's declared scope (ADD COLUMN ... NOT NULL with no
        // DEFAULT), so this specific statement is not itself flagged (the sibling DROP COLUMN
        // statement in the same call is what fires - see the multi-statement test below).
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE `Grant` ADD COLUMN `Id` INT AUTO_INCREMENT UNIQUE");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Alter_table_add_column_type_change_without_not_null_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE Users ADD COLUMN Score DECIMAL(10,2)");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Alter_table_rename_column_stays_silent()
    {
        // Out of scope by design: BW035 only looks for DROP COLUMN and ADD COLUMN ... NOT NULL,
        // not every structural ALTER TABLE shape.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE Users RENAME COLUMN OldName TO NewName");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Select_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("SELECT * FROM Users");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Non_constant_sql_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            var sql = GetSql();
            migrationBuilder.Sql(sql);
            """, members: "private static string GetSql() => \"ALTER TABLE Users DROP COLUMN Legacy\";"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Bracket_quoted_identifiers_do_not_break_detection()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE [dbo].[Grant] DROP COLUMN [Id]");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW035", diagnostic.Id);
    }

    [Fact]
    public async Task Keyword_inside_a_string_literal_does_not_false_positive()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("UPDATE Users SET Note = 'ALTER TABLE Users DROP COLUMN X' WHERE Id = 1");
            """));

        Assert.DoesNotContain(diagnostics, d => d.Id == "BW035");
    }

    [Fact]
    public async Task Drop_column_reports_only_BW035_and_suppresses_BW010()
    {
        // BW010 also matches this statement (it flags any statement containing a DROP keyword),
        // but BW035 is more specific for this exact shape - most-specific-wins, same as BW023
        // over BW001. Only one diagnostic should be reported, not both.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE Users DROP COLUMN LegacyFlag");
            """));

        Assert.Equal(new[] { "BW035" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task Multiple_statements_each_reporting_BW035_report_only_the_first()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.Sql("ALTER TABLE `Grant` DROP COLUMN `Id`; ALTER TABLE `Grant` ADD COLUMN `Id` INT AUTO_INCREMENT UNIQUE;");
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW035", diagnostic.Id);
        Assert.Contains("DROP COLUMN", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Sql_in_down_is_ignored()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithDown("""
            migrationBuilder.Sql("ALTER TABLE Users DROP COLUMN Legacy");
            """));

        Assert.Empty(diagnostics);
    }
}
