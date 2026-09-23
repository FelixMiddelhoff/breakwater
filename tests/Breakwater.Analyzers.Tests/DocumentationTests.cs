using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests;

/// <summary>
/// Compiles every C# code block in docs/rules/*.md that is marked
/// <c>// breakwater: expect BW0xx</c> and asserts that rule fires, so the documentation
/// cannot silently drift away from what the analyzer actually does. See
/// breakwater-architecture.md's "Documentation tests" note.
/// </summary>
public class DocumentationTests
{
    // Rules that only report under breakwater_profile = strict (see BreakwaterProfileReader
    // in the main project); their doc examples need that option set to observe the diagnostic.
    private static readonly HashSet<string> StrictOnlyRuleIds = new() { "BW011", "BW020", "BW030" };

    private static readonly Regex CodeBlock = new(@"```csharp\r?\n(?<code>.*?)```", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ExpectComment = new(@"//\s*breakwater:\s*expect\s+(?<id>BW\d{3})", RegexOptions.Compiled);
    private static readonly Regex TopLevelClass = new(@"\b(public|internal)\s+class\s+\w+", RegexOptions.Compiled);

    public static IEnumerable<object[]> DocExamples()
    {
        foreach (var path in Directory.EnumerateFiles(DocsRulesDirectory(), "BW*.md").OrderBy(p => p))
        {
            var text = File.ReadAllText(path);
            var fileName = Path.GetFileName(path);
            var blockIndex = 0;

            foreach (Match block in CodeBlock.Matches(text))
            {
                blockIndex++;
                var code = block.Groups["code"].Value;
                var expectMatch = ExpectComment.Match(code);
                if (!expectMatch.Success)
                {
                    continue;
                }

                yield return new object[] { fileName, blockIndex, expectMatch.Groups["id"].Value, code };
            }
        }
    }

    [Theory]
    [MemberData(nameof(DocExamples))]
    public async Task Doc_example_compiles_and_reports_its_expected_rule(string fileName, int blockIndex, string expectedRuleId, string code)
    {
        var source = TopLevelClass.IsMatch(code) ? code : WrapAsMigration(code);

        var globalOptions = StrictOnlyRuleIds.Contains(expectedRuleId)
            ? new Dictionary<string, string> { ["breakwater_profile"] = "strict" }
            : null;

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(source, globalOptions: globalOptions);

        Assert.True(
            diagnostics.Any(d => d.Id == expectedRuleId),
            $"{fileName} block #{blockIndex}: expected {expectedRuleId} but got [{string.Join(", ", diagnostics.Select(d => d.Id))}]." +
            Environment.NewLine + source);
    }

    /// <summary>
    /// A doc example that is only a method body (or a couple of methods, like BW011's Up/Down
    /// pair) is wrapped in a complete migration class, the same shape
    /// <see cref="MigrationSnippet"/> produces for hand-written tests.
    /// </summary>
    private static string WrapAsMigration(string code)
    {
        return $$"""
            using Microsoft.EntityFrameworkCore.Migrations;

            [Migration("20260101000000_DocExample")]
            public class DocExample : Migration
            {
                {{code}}
            }
            """;
    }

    private static string DocsRulesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "rules");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(directory.FullName, "Breakwater.sln")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find docs/rules next to Breakwater.sln by walking up from " + AppContext.BaseDirectory);
    }
}
