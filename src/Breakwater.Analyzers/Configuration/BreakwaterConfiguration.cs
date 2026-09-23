using System;
using System.Collections.Generic;
using System.Linq;
using Breakwater.Analyzers.Suppression;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Breakwater.Analyzers.Configuration;

/// <summary>
/// Every <c>.editorconfig</c>-driven Breakwater setting, read once per compilation start (from
/// <see cref="AnalyzerConfigOptionsProvider.GlobalOptions"/>, the same source
/// <see cref="BreakwaterProfileReader"/> already reads <c>breakwater_profile</c> from) and threaded
/// down instead of re-read per operation - none of these can change mid-compilation. A malformed
/// value for any key falls back to that key's default; the analyzer never throws on bad config.
/// </summary>
internal sealed class BreakwaterConfiguration
{
    private const string ProviderKey = "breakwater_provider";
    private const string DeployModelKey = "breakwater_deploy_model";
    private const string SinceMigrationKey = "breakwater_since_migration";
    private const string SmallTablesKey = "breakwater_small_tables";

    public BreakwaterProfile Profile { get; }

    public BreakwaterDatabaseProvider Provider { get; }

    public BreakwaterDeployModel DeployModel { get; }

    /// <summary>Migrations with an id lexicographically &lt;= this are skipped entirely. Null means "not set".</summary>
    public string? SinceMigrationId { get; }

    /// <summary>Table names (case-insensitive) that soften table-lock rules to Info.</summary>
    public HashSet<string> SmallTables { get; }

    /// <summary>Table-lock/lock-duration rules that <see cref="SmallTables"/> softens, per breakwater-rules.md.</summary>
    public static readonly HashSet<string> SmallTableSensitiveRuleIds = new HashSet<string>
    {
        "BW004", "BW005", "BW006", "BW007", "BW008", "BW009",
        "BW015", "BW016", "BW019", "BW023", "BW025", "BW026", "BW027",
    };

    /// <summary>Removal rules that <see cref="DeployModel"/> <c>downtime_ok</c> downgrades to Info.</summary>
    public static readonly HashSet<string> DeployModelSensitiveRuleIds = new HashSet<string> { "BW001", "BW002", "BW003" };

    private BreakwaterConfiguration(
        BreakwaterProfile profile,
        BreakwaterDatabaseProvider provider,
        BreakwaterDeployModel deployModel,
        string? sinceMigrationId,
        HashSet<string> smallTables)
    {
        Profile = profile;
        Provider = provider;
        DeployModel = deployModel;
        SinceMigrationId = sinceMigrationId;
        SmallTables = smallTables;
    }

    public static BreakwaterConfiguration Read(AnalyzerConfigOptionsProvider optionsProvider)
    {
        var options = optionsProvider.GlobalOptions;

        var profile = BreakwaterProfileReader.Read(optionsProvider);
        var provider = ReadProvider(options);
        var deployModel = ReadDeployModel(options);
        var sinceMigrationId = ReadSinceMigration(options);
        var smallTables = ReadSmallTables(options);

        return new BreakwaterConfiguration(profile, provider, deployModel, sinceMigrationId, smallTables);
    }

    private static BreakwaterDatabaseProvider ReadProvider(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(ProviderKey, out var value))
        {
            return BreakwaterDatabaseProvider.Auto;
        }

        return value?.Trim().ToLowerInvariant() switch
        {
            "sqlserver" => BreakwaterDatabaseProvider.SqlServer,
            "postgres" => BreakwaterDatabaseProvider.Postgres,
            "sqlite" => BreakwaterDatabaseProvider.Sqlite,
            "mysql" => BreakwaterDatabaseProvider.MySql,
            "auto" => BreakwaterDatabaseProvider.Auto,
            _ => BreakwaterDatabaseProvider.Auto,
        };
    }

    private static BreakwaterDeployModel ReadDeployModel(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(DeployModelKey, out var value)
            && string.Equals(value?.Trim(), "downtime_ok", StringComparison.OrdinalIgnoreCase))
        {
            return BreakwaterDeployModel.DowntimeOk;
        }

        return BreakwaterDeployModel.Rolling;
    }

    private static string? ReadSinceMigration(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(SinceMigrationKey, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return null;
    }

    private static HashSet<string> ReadSmallTables(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(SmallTablesKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var names = value
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .Where(name => name.Length > 0);
        return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }
}
