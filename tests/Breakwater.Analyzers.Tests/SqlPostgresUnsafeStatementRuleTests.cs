using System.Linq;
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
        // Also reports BW037 (no IF NOT EXISTS guard) - a genuinely different hazard of the same
        // statement (locking vs. re-run safety), so both firing is intentional, not duplicate noise.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.Sql("CREATE INDEX ix_users_email ON users (email);");
            }
            """));

        Assert.Equal(new[] { "BW019", "BW037" }, diagnostics.Select(d => d.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task CreateIndex_concurrently_on_postgres_stays_silent_for_bw019_but_bw037_still_fires()
    {
        // CONCURRENTLY satisfies BW019; there is still no IF NOT EXISTS guard, so BW037 fires alone.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.Sql("CREATE INDEX CONCURRENTLY ix_users_email ON users (email);");
            }
            """));

        Assert.Equal(new[] { "BW037" }, diagnostics.Select(d => d.Id));
    }

    [Fact]
    public async Task CreateIndex_concurrently_if_not_exists_on_postgres_stays_fully_silent()
    {
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(MigrationSnippet.WithUp("""
            if (migrationBuilder.IsNpgsql())
            {
                migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_users_email ON users (email);");
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
    public async Task Without_a_known_postgres_guard_bw019_stays_silent_but_bw037_still_fires()
    {
        // BW019 needs a detected Postgres provider ("silent when unsure"); BW037 is
        // provider-agnostic, so it still fires on the missing IF NOT EXISTS guard.
        var diagnostics = await AnalyzerRunner.AnalyzeAsync(
            MigrationSnippet.WithUp("""migrationBuilder.Sql("CREATE INDEX ix_users_email ON users (email);");"""));

        Assert.Equal(new[] { "BW037" }, diagnostics.Select(d => d.Id));
    }
}
