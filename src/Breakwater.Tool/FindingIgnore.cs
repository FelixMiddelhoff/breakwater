using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Breakwater.Tool;

/// <summary>
/// A set of <c>RULE:LINE</c> suppressions collected from <c>--ignore</c> flags and/or an
/// <c>--ignore-file</c>, used to filter findings out of a generated SQL script lint run without
/// having to edit the generated script itself (which would be lost on the next
/// <c>dotnet ef migrations script</c> regeneration).
/// </summary>
internal sealed class FindingIgnores
{
    private readonly HashSet<(string RuleId, int Line)> _entries;

    private FindingIgnores(HashSet<(string RuleId, int Line)> entries)
    {
        _entries = entries;
    }

    public static FindingIgnores Empty { get; } = new(new HashSet<(string, int)>());

    public bool IsEmpty => _entries.Count == 0;

    public int Count => _entries.Count;

    public bool Suppresses(SqlFinding finding) => _entries.Contains((finding.RuleId, finding.Line));

    /// <summary>Parses a single <c>--ignore</c> flag value, expected as <c>RULE:LINE</c> (e.g. <c>BW010:7</c>).</summary>
    public static bool TryParseEntry(string value, out string ruleId, out int line)
    {
        var separator = value.IndexOf(':');
        if (separator > 0 && separator < value.Length - 1)
        {
            var rulePart = value[..separator];
            var linePart = value[(separator + 1)..];
            if (int.TryParse(linePart, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedLine) && parsedLine > 0)
            {
                ruleId = rulePart;
                line = parsedLine;
                return true;
            }
        }

        ruleId = string.Empty;
        line = 0;
        return false;
    }

    /// <summary>Builds the combined ignore set from repeated <c>--ignore</c> values and an optional ignore file.</summary>
    public static bool TryBuild(
        IReadOnlyList<string> ignoreFlags,
        string? ignoreFilePath,
        out FindingIgnores ignores,
        out string error)
    {
        var entries = new HashSet<(string, int)>();

        foreach (var flag in ignoreFlags)
        {
            if (!TryParseEntry(flag, out var ruleId, out var line))
            {
                ignores = Empty;
                error = $"Invalid --ignore value '{flag}'. Expected RULE:LINE, e.g. BW010:7.";
                return false;
            }

            entries.Add((ruleId, line));
        }

        if (ignoreFilePath is not null)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(ignoreFilePath);
            }
            catch (IOException ex)
            {
                ignores = Empty;
                error = $"Could not read ignore file '{ignoreFilePath}': {ex.Message}";
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                ignores = Empty;
                error = $"Could not read ignore file '{ignoreFilePath}': {ex.Message}";
                return false;
            }

            foreach (var rawLine in lines)
            {
                var trimmed = rawLine.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                if (!TryParseEntry(trimmed, out var ruleId, out var line))
                {
                    ignores = Empty;
                    error = $"Invalid entry '{trimmed}' in ignore file '{ignoreFilePath}'. Expected RULE:LINE, e.g. BW010:7.";
                    return false;
                }

                entries.Add((ruleId, line));
            }
        }

        ignores = entries.Count == 0 ? Empty : new FindingIgnores(entries);
        error = string.Empty;
        return true;
    }
}
