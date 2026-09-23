using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW016 (Postgres AddColumn with a volatile default).</summary>
public class AddColumnVolatileDefaultRuleTests
{
    [Fact]
    public async Task Now_default_on_postgres_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.AddColumn<System.DateTime>(name: "CreatedAt", table: "Users", defaultValueSql: "now()");
            }
            """));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("BW016", diagnostic.Id);
        Assert.Contains("'Users.CreatedAt'", diagnostic.GetMessage());
        Assert.Contains("now()", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Gen_random_uuid_default_on_postgres_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.AddColumn<System.Guid>(name: "PublicId", table: "Users", defaultValueSql: "gen_random_uuid()");
            }
            """));

        Assert.Equal("BW016", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Constant_default_on_postgres_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.AddColumn<int>(name: "Age", table: "Users", defaultValue: 0);
            }
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Volatile_default_without_a_known_postgres_provider_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.AddColumn<System.DateTime>(name: "CreatedAt", table: "Users", defaultValueSql: "now()");
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Unknown_function_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.AddColumn<int>(name: "Rank", table: "Users", defaultValueSql: "some_custom_function()");
            }
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Table_created_in_the_same_migration_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            migrationBuilder.CreateTable(name: "Users");
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.AddColumn<System.DateTime>(name: "CreatedAt", table: "Users", defaultValueSql: "now()");
            }
            """));

        Assert.Empty(diagnostics);
    }
}
