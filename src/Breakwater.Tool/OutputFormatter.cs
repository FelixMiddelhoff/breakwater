using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;

namespace Breakwater.Tool;

/// <summary>Renders findings as a console table or as JSON (for CI tooling that parses output).</summary>
internal static class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new SeverityJsonConverter() },
    };

    /// <summary>
    /// Serializes <see cref="DiagnosticSeverity"/> using the same "Suggestion"/"Warning"
    /// vocabulary as the table output and <c>--fail-on</c>, rather than the enum's own member
    /// names (Info/Warning), so CI tooling parsing the JSON sees one consistent severity name.
    /// </summary>
    private sealed class SeverityJsonConverter : JsonConverter<DiagnosticSeverity>
    {
        public override DiagnosticSeverity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetString() switch
            {
                "Suggestion" => DiagnosticSeverity.Info,
                var value => Enum.Parse<DiagnosticSeverity>(value!),
            };

        public override void Write(Utf8JsonWriter writer, DiagnosticSeverity value, JsonSerializerOptions options)
            => writer.WriteStringValue(SeverityLabel(value));
    }

    public static void WriteTable(TextWriter writer, IReadOnlyList<SqlFinding> findings)
    {
        if (findings.Count == 0)
        {
            writer.WriteLine("No issues found.");
            return;
        }

        var ruleWidth = Math.Max("RULE".Length, Max(findings, f => f.RuleId.Length));
        var severityWidth = Math.Max("SEVERITY".Length, Max(findings, f => SeverityLabel(f.Severity).Length));
        var lineWidth = Math.Max("LINE".Length, Max(findings, f => f.Line.ToString().Length));
        var migrationWidth = Math.Max("MIGRATION".Length, Max(findings, f => f.Migration.Length));

        writer.WriteLine($"{Pad("RULE", ruleWidth)}  {Pad("SEVERITY", severityWidth)}  {Pad("LINE", lineWidth)}  {Pad("MIGRATION", migrationWidth)}  MESSAGE");
        foreach (var finding in findings)
        {
            writer.WriteLine($"{Pad(finding.RuleId, ruleWidth)}  {Pad(SeverityLabel(finding.Severity), severityWidth)}  {Pad(finding.Line.ToString(), lineWidth)}  {Pad(finding.Migration, migrationWidth)}  {finding.Message}");
        }

        writer.WriteLine();
        writer.WriteLine($"{findings.Count} issue(s) found.");
    }

    public static void WriteJson(TextWriter writer, IReadOnlyList<SqlFinding> findings)
    {
        var json = JsonSerializer.Serialize(findings, JsonOptions);
        writer.WriteLine(json);
    }

    /// <summary>
    /// "Suggestion"/"Warning" rather than DiagnosticSeverity's own enum member names ("Info"/
    /// "Warning"), so the table and JSON use the same vocabulary as <c>--fail-on</c> and the
    /// analyzer's own documented tiers (RuleDescriptors.Suggestion is DiagnosticSeverity.Info).
    /// </summary>
    private static string SeverityLabel(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Info => "Suggestion",
        _ => severity.ToString(),
    };

    /// <summary>
    /// Renders findings as a minimal, valid SARIF 2.1.0 log so the CLI's output can feed GitHub
    /// code scanning the same way the analyzer's own <c>-p:ErrorLog=...;version=2</c> SARIF output
    /// does (see <c>.github/workflows/pr-review.yml</c> and
    /// <c>.github/scripts/post-sarif-comment.js</c>, which read <c>runs[0].results[].level</c>,
    /// <c>ruleId</c>, <c>message.text</c>, and <c>locations[0].physicalLocation</c>). <see
    /// cref="SqlFinding"/> only carries a flat "Warning"/"Suggestion" tier (no finer severity, see
    /// <see cref="SqlScriptLinter"/>), which maps directly onto the two SARIF levels the comment
    /// script already understands ("warning" and "note"); anything else defaults to "warning"
    /// rather than inventing a tier this codebase doesn't have.
    /// </summary>
    public static void WriteSarif(TextWriter writer, IReadOnlyList<SqlFinding> findings, string scriptPath)
    {
        var uri = BuildArtifactUri(scriptPath);

        var results = new List<object>(findings.Count);
        foreach (var finding in findings)
        {
            results.Add(new
            {
                ruleId = finding.RuleId,
                level = SeverityToSarifLevel(finding.Severity),
                message = new { text = finding.Message },
                locations = new object[]
                {
                    new
                    {
                        physicalLocation = new
                        {
                            artifactLocation = new { uri },
                            region = new { startLine = finding.Line },
                        },
                    },
                },
            });
        }

        var document = new Dictionary<string, object?>
        {
            ["$schema"] = "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new object[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "Breakwater.Tool",
                            informationUri = "https://github.com/FelixMiddelhoff/breakwater",
                            version = typeof(OutputFormatter).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                        },
                    },
                    results,
                },
            },
        };

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        writer.WriteLine(json);
    }

    private static string SeverityToSarifLevel(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Info => "note",
        _ => "warning",
    };

    private static string BuildArtifactUri(string scriptPath)
    {
        if (Uri.TryCreate(scriptPath, UriKind.Absolute, out var absolute) && absolute.Scheme == "file")
        {
            return absolute.AbsoluteUri;
        }

        if (Path.IsPathRooted(scriptPath))
        {
            return new Uri(scriptPath).AbsoluteUri;
        }

        return scriptPath.Replace('\\', '/');
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
