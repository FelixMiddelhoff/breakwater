using System.Linq;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW028 (missing [Migration] attribute / duplicate ids) and BW032 (EnsureCreated with migrations present).</summary>
public class DiscoverabilityRuleTests
{
    [Fact]
    public async Task Migration_class_without_the_attribute_is_reported()
    {
        var source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public class AddThings : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW028", diagnostic.Id);
        Assert.Contains("AddThings", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Migration_class_with_the_attribute_stays_silent()
    {
        var source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            [Migration("20260101000000_AddThings")]
            public class AddThings : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task An_abstract_base_migration_class_stays_silent()
    {
        var source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            public abstract class BaseMigration : Migration { }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Two_migration_classes_with_the_same_id_are_both_reported()
    {
        var source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            [Migration("dup")]
            public class First : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }
            }

            [Migration("dup")]
            public class Second : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("BW028", d.Id));
        Assert.All(diagnostics, d => Assert.Contains("'dup'", d.GetMessage()));
    }

    [Fact]
    public async Task Distinct_ids_are_not_reported_as_duplicates()
    {
        var source = """
            using Microsoft.EntityFrameworkCore.Migrations;

            [Migration("one")]
            public class First : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }
            }

            [Migration("two")]
            public class Second : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }
            }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task EnsureCreated_in_a_project_with_migrations_is_reported()
    {
        var source = """
            using Microsoft.EntityFrameworkCore.Migrations;
            using Microsoft.EntityFrameworkCore.Infrastructure;

            [Migration("20260101000000_AddThings")]
            public class AddThings : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }
            }

            public class Startup
            {
                public void Configure(DatabaseFacade database) => database.EnsureCreated();
            }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Equal("BW032", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task EnsureCreated_in_a_project_without_any_migration_stays_silent()
    {
        var source = """
            using Microsoft.EntityFrameworkCore.Infrastructure;

            public class Startup
            {
                public void Configure(DatabaseFacade database) => database.EnsureCreated();
            }
            """;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source);

        Assert.Empty(diagnostics);
    }
}
