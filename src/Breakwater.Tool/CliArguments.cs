using Breakwater.Analyzers.Configuration;
using Microsoft.CodeAnalysis;

namespace Breakwater.Tool;

/// <summary>Parsed command-line arguments for <c>breakwater-sql</c>.</summary>
internal sealed record ParsedArguments(string? Path, BreakwaterDatabaseProvider Provider, bool Json, DiagnosticSeverity FailOn);

internal static class CliArguments
{
    /// <summary>
    /// Backward-compatible default: every rule this CLI currently implements (BW010/BW019/BW031/
    /// BW033/BW034/BW035) is Warning tier, and BW036 is the only Suggestion-tier finding it can
    /// report, so failing at Warning-and-above reproduces the "any finding at all exits 1"
    /// behavior this tool originally shipped, for every script that was already exit-1 before.
    /// </summary>
    public const DiagnosticSeverity DefaultFailOn = DiagnosticSeverity.Warning;

    public const string Usage =
        "Usage: breakwater-sql --provider <sqlserver|postgres|sqlite|mysql> [--format json] [--fail-on <suggestion|warning>] [<script-file>]\n" +
        "       breakwater-sql init [<path>] [--force]\n" +
        "Reads the script from <script-file>, or from stdin when no file is given.\n" +
        "--fail-on sets the minimum severity that causes a nonzero exit code (default: warning).\n" +
        "  suggestion  exit 1 if any finding (including Suggestion-tier, e.g. BW036) is reported.\n" +
        "  warning     exit 1 only for Warning-tier findings (BW010/BW019/BW031/BW033/BW034/BW035); this is the default.\n" +
        "Lower-severity findings are still printed either way; only the exit code changes.";

    public static bool TryParse(string[] args, out ParsedArguments parsed, out string error)
    {
        string? path = null;
        BreakwaterDatabaseProvider? provider = null;
        var json = false;
        var failOn = DefaultFailOn;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--provider":
                    if (!TryTakeValue(args, ref i, out var providerValue))
                    {
                        parsed = null!;
                        error = "--provider requires a value.";
                        return false;
                    }

                    if (!ProviderOption.TryParse(providerValue, out var parsedProvider))
                    {
                        parsed = null!;
                        error = $"Unrecognized provider '{providerValue}'. Expected sqlserver, postgres, sqlite, or mysql.";
                        return false;
                    }

                    provider = parsedProvider;
                    break;

                case "--format":
                    if (!TryTakeValue(args, ref i, out var formatValue))
                    {
                        parsed = null!;
                        error = "--format requires a value.";
                        return false;
                    }

                    if (formatValue != "json" && formatValue != "table")
                    {
                        parsed = null!;
                        error = $"Unrecognized format '{formatValue}'. Expected json or table.";
                        return false;
                    }

                    json = formatValue == "json";
                    break;

                case "--fail-on":
                    if (!TryTakeValue(args, ref i, out var failOnValue))
                    {
                        parsed = null!;
                        error = "--fail-on requires a value.";
                        return false;
                    }

                    switch (failOnValue)
                    {
                        case "suggestion":
                            failOn = DiagnosticSeverity.Info;
                            break;
                        case "warning":
                            failOn = DiagnosticSeverity.Warning;
                            break;
                        default:
                            parsed = null!;
                            error = $"Unrecognized --fail-on value '{failOnValue}'. Expected suggestion or warning.";
                            return false;
                    }

                    break;

                case "-h":
                case "--help":
                    parsed = null!;
                    error = Usage;
                    return false;

                default:
                    if (path is not null)
                    {
                        parsed = null!;
                        error = $"Unexpected extra argument '{arg}'.";
                        return false;
                    }

                    path = arg;
                    break;
            }
        }

        if (provider is null)
        {
            parsed = null!;
            error = "--provider is required: the generated script is already provider-specific SQL.";
            return false;
        }

        parsed = new ParsedArguments(path, provider.Value, json, failOn);
        error = string.Empty;
        return true;
    }

    private static bool TryTakeValue(string[] args, ref int i, out string value)
    {
        if (i + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        i++;
        value = args[i];
        return true;
    }
}
