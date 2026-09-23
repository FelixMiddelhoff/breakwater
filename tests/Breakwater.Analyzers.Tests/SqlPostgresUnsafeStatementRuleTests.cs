using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>BW019 (Postgres-specific raw SQL risk: CREATE INDEX without CONCURRENTLY, LOCK TABLE, VACUUM FULL, ALTER TABLE ... SET DATA TYPE).</summary>
public class SqlPostgresUnsafeStatementRuleTests
{
    [Fact]
    public async Task CreateIndex_without_concurrently_on_postgres_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.Sql("CREATE INDEX ix_users_email ON users (email);");
            }
            """));

        Assert.Equal("BW019", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task CreateIndex_concurrently_on_postgres_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.Sql("CREATE INDEX CONCURRENTLY ix_users_email ON users (email);");
            }
            """));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task LockTable_on_postgres_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.Sql("LOCK TABLE users IN ACCESS EXCLUSIVE MODE;");
            }
            """));

        Assert.Equal("BW019", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task VacuumFull_on_postgres_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.Sql("VACUUM FULL users;");
            }
            """));

        Assert.Equal("BW019", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task AlterTableSetDataType_on_postgres_is_reported()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.Sql("ALTER TABLE users ALTER COLUMN age SET DATA TYPE bigint;");
            }
            """));

        Assert.Equal("BW019", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Without_a_known_postgres_guard_stays_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.Sql("CREATE INDEX ix_users_email ON users (email);");"""));

        Assert.Empty(diagnostics);
    }
}
