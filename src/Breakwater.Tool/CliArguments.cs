using Breakwater.Analyzers.Configuration;

namespace Breakwater.Tool;

/// <summary>Parsed command-line arguments for <c>breakwater-sql</c>.</summary>
internal sealed record ParsedArguments(string? Path, BreakwaterDatabaseProvider Provider, bool Json);

internal static class CliArguments
{
    public const string Usage =
        "Usage: breakwater-sql --provider <sqlserver|postgres|sqlite|mysql> [--format json] [<script-file>]\n" +
        "       breakwater-sql init [<path>] [--force]\n" +
        "Reads the script from <script-file>, or from stdin when no file is given.";

    public static bool TryParse(string[] args, out ParsedArguments parsed, out string error)
    {
        string? path = null;
        BreakwaterDatabaseProvider? provider = null;
        var json = false;

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

        parsed = new ParsedArguments(path, provider.Value, json);
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
