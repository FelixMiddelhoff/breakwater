namespace Breakwater.Analyzers.Configuration;

/// <summary>
/// The database provider a migration targets. <c>Auto</c> (the default) keeps the existing
/// heuristics (an <c>if (migrationBuilder.IsNpgsql())</c>-style guard, or a provider-specific
/// chained annotation such as <c>Npgsql:...</c>/<c>SqlServer:...</c>); any other value is an
/// explicit <c>breakwater_provider</c> override that is trusted outright, without requiring the
/// guard pattern.
/// </summary>
internal enum BreakwaterDatabaseProvider
{
    Auto,
    SqlServer,
    Postgres,
    Sqlite,
    MySql,
}
