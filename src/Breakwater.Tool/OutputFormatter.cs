using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Breakwater.Tool;

/// <summary>Renders findings as a console table or as JSON (for CI tooling that parses output).</summary>
internal static class OutputFormatter
{
    public static void WriteTable(TextWriter writer, IReadOnlyList<SqlFinding> findings)
    {
        if (findings.Count == 0)
        {
            writer.WriteLine("No issues found.");
            return;
        }

        var ruleWidth = Math.Max("RULE".Length, Max(findings, f => f.RuleId.Length));
        var lineWidth = Math.Max("LINE".Length, Max(findings, f => f.Line.ToString().Length));
        var migrationWidth = Math.Max("MIGRATION".Length, Max(findings, f => f.Migration.Length));

        writer.WriteLine($"{Pad("RULE", ruleWidth)}  {Pad("LINE", lineWidth)}  {Pad("MIGRATION", migrationWidth)}  MESSAGE");
        foreach (var finding in findings)
        {
            writer.WriteLine($"{Pad(finding.RuleId, ruleWidth)}  {Pad(finding.Line.ToString(), lineWidth)}  {Pad(finding.Migration, migrationWidth)}  {finding.Message}");
        }

        writer.WriteLine();
        writer.WriteLine($"{findings.Count} issue(s) found.");
    }

    public static void WriteJson(TextWriter writer, IReadOnlyList<SqlFinding> findings)
    {
        var json = JsonSerializer.Serialize(findings, new JsonSerializerOptions { WriteIndented = true });
        writer.WriteLine(json);
    }

    private static int Max(IReadOnlyList<SqlFinding> findings, Func<SqlFinding, int> selector)
    {
        var max = 0;
        foreach (var finding in findings)
        {
            max = Math.Max(max, selector(finding));
        }

        return max;
    }

    private static string Pad(string value, int width) => value.PadRight(width);
}
