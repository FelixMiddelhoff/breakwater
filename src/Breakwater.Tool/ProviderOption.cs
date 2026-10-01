using Breakwater.Analyzers.Configuration;

namespace Breakwater.Tool;

/// <summary>
/// Parses the <c>--provider</c> flag into the analyzer's own
/// <see cref="BreakwaterDatabaseProvider"/> enum, so the same type that gates BW019/BW031 in the
/// analyzer gates them here. Unlike the analyzer (which defaults to <c>Auto</c> and infers the
/// provider from a code guard), the tool has no such guard to inspect - the generated script is
/// already provider-specific SQL - so <c>--provider</c> is required and <c>Auto</c> is never a
/// valid value for it.
/// </summary>
internal static class ProviderOption
{
    public static bool TryParse(string value, out BreakwaterDatabaseProvider provider)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "sqlserver":
                provider = BreakwaterDatabaseProvider.SqlServer;
                return true;
            case "postgres":
            case "postgresql":
            case "npgsql":
                provider = BreakwaterDatabaseProvider.Postgres;
                return true;
            case "sqlite":
                provider = BreakwaterDatabaseProvider.Sqlite;
                return true;
            case "mysql":
                provider = BreakwaterDatabaseProvider.MySql;
                return true;
            default:
                provider = BreakwaterDatabaseProvider.Auto;
                return false;
        }
    }
}
