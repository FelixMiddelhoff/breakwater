using Breakwater.Tool;
using Xunit;

namespace Breakwater.Tool.Tests;

public class MigrationScriptSplitterTests
{
    [Fact]
    public void Script_with_no_history_insert_yields_a_single_fallback_section()
    {
        var sections = MigrationScriptSplitter.Split("CREATE TABLE Foo (Id int);");

        var section = Assert.Single(sections);
        Assert.Equal("(script)", section.Name);
        Assert.Equal(1, section.StartLine);
    }

    [Fact]
    public void Script_with_two_migrations_is_split_on_the_history_insert()
    {
        const string script = """
            CREATE TABLE Foo (Id int);
            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES (N'20240101000000_First', N'8.0.0');
            CREATE TABLE Bar (Id int);
            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES (N'20240102000000_Second', N'8.0.0');
            """;

        var sections = MigrationScriptSplitter.Split(script);

        Assert.Equal(2, sections.Count);
        Assert.Equal("20240101000000_First", sections[0].Name);
        Assert.Equal(1, sections[0].StartLine);
        Assert.Equal("20240102000000_Second", sections[1].Name);
    }

    [Fact]
    public void SectionFor_attributes_a_line_to_the_last_section_that_started_before_it()
    {
        var sections = new[]
        {
            new MigrationSection("First", 1),
            new MigrationSection("Second", 10),
        };

        Assert.Equal("First", MigrationScriptSplitter.SectionFor(sections, 5));
        Assert.Equal("Second", MigrationScriptSplitter.SectionFor(sections, 10));
        Assert.Equal("Second", MigrationScriptSplitter.SectionFor(sections, 999));
    }

    [Fact]
    public void Postgres_quoted_history_table_name_is_also_recognized()
    {
        const string script = """
            CREATE TABLE "Foo" ("Id" integer);
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20240101000000_First', '8.0.0');
            """;

        var sections = MigrationScriptSplitter.Split(script);

        var section = Assert.Single(sections);
        Assert.Equal("20240101000000_First", section.Name);
    }
}
