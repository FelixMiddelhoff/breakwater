using System.IO;
using System.Text.Json;
using Breakwater.Tool;
using Xunit;

namespace Breakwater.Tool.Tests;

/// <summary>Covers <see cref="OutputFormatter.WriteSarif"/>: the CLI's SARIF 2.1.0 output, which
/// feeds GitHub code scanning the same way the analyzer's own <c>-p:ErrorLog=...;version=2</c>
/// SARIF output does (see <c>.github/scripts/post-sarif-comment.js</c>).</summary>
public class OutputFormatterTests
{
    [Fact]
    public void Script_with_a_finding_produces_one_result_with_rule_id_and_line()
    {
        var findings = new[] { new SqlFinding("BW010", "Warning", "0001_Init", 7, "Raw SQL contains an unfiltered DELETE") };
        var writer = new StringWriter();

        OutputFormatter.WriteSarif(writer, findings, "script.sql");

        using var document = JsonDocument.Parse(writer.ToString());
        var root = document.RootElement;

        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        Assert.True(root.TryGetProperty("$schema", out var schema));
        Assert.Contains("sarif-schema-2.1.0.json", schema.GetString());

        var runs = root.GetProperty("runs");
        Assert.Equal(1, runs.GetArrayLength());

        var run = runs[0];
        Assert.Equal("Breakwater.Tool", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
        Assert.False(string.IsNullOrEmpty(run.GetProperty("tool").GetProperty("driver").GetProperty("informationUri").GetString()));

        var results = run.GetProperty("results");
        var result = Assert.Single(results.EnumerateArray());

        Assert.Equal("BW010", result.GetProperty("ruleId").GetString());
        Assert.Equal("warning", result.GetProperty("level").GetString());
        Assert.Equal("Raw SQL contains an unfiltered DELETE", result.GetProperty("message").GetProperty("text").GetString());

        var location = result.GetProperty("locations")[0].GetProperty("physicalLocation");
        Assert.Equal(7, location.GetProperty("region").GetProperty("startLine").GetInt32());
        Assert.Contains("script.sql", location.GetProperty("artifactLocation").GetProperty("uri").GetString());
    }

    [Fact]
    public void Suggestion_severity_maps_to_note_level()
    {
        var findings = new[] { new SqlFinding("BW036", "Suggestion", "0001_Init", 3, "Idempotent redefinition") };
        var writer = new StringWriter();

        OutputFormatter.WriteSarif(writer, findings, "script.sql");

        using var document = JsonDocument.Parse(writer.ToString());
        var result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        Assert.Equal("note", result.GetProperty("level").GetString());
    }

    [Fact]
    public void Clean_script_produces_valid_sarif_with_empty_results()
    {
        var findings = System.Array.Empty<SqlFinding>();
        var writer = new StringWriter();

        OutputFormatter.WriteSarif(writer, findings, "clean.sql");

        using var document = JsonDocument.Parse(writer.ToString());
        var root = document.RootElement;

        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        var results = root.GetProperty("runs")[0].GetProperty("results");
        Assert.Equal(JsonValueKind.Array, results.ValueKind);
        Assert.Equal(0, results.GetArrayLength());
    }

    [Fact]
    public void Program_dashdash_format_sarif_produces_parseable_sarif_for_a_finding()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "TRUNCATE TABLE Orders;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exitCode = Program.Run(new[] { "--provider", "postgres", "--format", "sarif", path }, stdout, stderr, new StringReader(string.Empty));

            Assert.Equal(1, exitCode);

            using var document = JsonDocument.Parse(stdout.ToString());
            var results = document.RootElement.GetProperty("runs")[0].GetProperty("results");
            Assert.Equal(1, results.GetArrayLength());
            Assert.Equal("BW010", results[0].GetProperty("ruleId").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
