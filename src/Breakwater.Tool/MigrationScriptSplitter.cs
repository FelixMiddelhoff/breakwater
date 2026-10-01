using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Breakwater.Tool;

/// <summary>One migration's slice of a generated script, named and located for reporting.</summary>
internal sealed record MigrationSection(string Name, int StartLine);

/// <summary>
/// Best-effort splitter for the output of <c>dotnet ef migrations script</c>, used only to label
/// findings with the migration they came from - linting itself always runs against the whole
/// script text so line numbers stay correct regardless of whether splitting succeeds.
/// </summary>
/// <remarks>
/// EF does not emit a per-migration header comment by default, so splitting on one is not
/// reliable. What EF always emits, for every migration it includes, is the bookkeeping insert
/// into the migrations-history table at the end of that migration's statements:
/// <c>INSERT INTO [__EFMigrationsHistory] ("MigrationId", "ProductVersion") VALUES (N'20240101..._Name', ...)</c>
/// (table/column quoting differs per provider; the migration id literal does not). That insert is
/// used as the end-of-section marker instead: everything up to and including it belongs to the
/// migration it names, and the next line starts the following migration's section.
/// </remarks>
internal static class MigrationScriptSplitter
{
    private static readonly Regex HistoryInsert = new(
        @"INSERT\s+INTO\s+[\[`""]?__EFMigrationsHistory[\]`""]?\s*\([^)]*\)\s*VALUES\s*\(\s*N?'(?<id>[^']+)'",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Returns the sections found in source order. When no migration-history insert is found at
    /// all (e.g. a hand-trimmed script, or one generated with history tracking disabled), a
    /// single "(script)" section covering the whole file is returned so callers always have
    /// somewhere to attribute a finding to.
    /// </summary>
    public static IReadOnlyList<MigrationSection> Split(string script)
    {
        var sections = new List<MigrationSection>();
        var line = 1;
        var searchFrom = 0;
        var sectionStartLine = 1;

        while (true)
        {
            var match = HistoryInsert.Match(script, searchFrom);
            if (!match.Success)
            {
                break;
            }

            var migrationId = match.Groups["id"].Value;
            sections.Add(new MigrationSection(migrationId, sectionStartLine));

            var matchEnd = match.Index + match.Length;
            line += CountNewlines(script, searchFrom, matchEnd);
            sectionStartLine = line;
            searchFrom = matchEnd;
        }

        if (sections.Count == 0)
        {
            return new[] { new MigrationSection("(script)", 1) };
        }

        return sections;
    }

    /// <summary>Returns the name of the section that contains <paramref name="line"/>.</summary>
    public static string SectionFor(IReadOnlyList<MigrationSection> sections, int line)
    {
        var name = sections[0].Name;
        foreach (var section in sections)
        {
            if (section.StartLine > line)
            {
                break;
            }

            name = section.Name;
        }

        return name;
    }

    private static int CountNewlines(string text, int from, int to)
    {
        var count = 0;
        for (var i = from; i < to; i++)
        {
            if (text[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }
}
