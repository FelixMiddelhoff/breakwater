using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Breakwater.Analyzers.Configuration;
using Breakwater.Analyzers.Sql;

namespace Breakwater.Tool;

/// <summary>
/// Runs the same risky-SQL checks the analyzer's raw-SQL rules (BW010/BW019/BW031/BW033/BW034/
/// BW035/BW036) apply to an inline <c>migrationBuilder.Sql(...)</c> call, but against the whole
/// text of a generated migration script instead. The tokenizer-driven checks (BW010, BW035,
/// BW036, and BW019) call straight into <see cref="SqlTokenizer"/>/<see cref="SqlStatementRisk"/>,
/// reusing the exact logic the analyzer uses. BW031/BW033/BW034 are plain regex matches over raw
/// SQL text in the analyzer too (they never tokenize), so their patterns are restated here rather
/// than reused through a shared internal type - duplicating four small, stable regexes was judged
/// lower risk than changing analyzer rule files (out of scope for this tool) to expose them.
/// </summary>
internal static class SqlScriptLinter
{
    private static readonly Regex GoSeparator = new(@"^[ \t]*GO[ \t]*(\d+)?[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex UseStatement = new(@"(?:^|;)\s*USE\s+[\[\w]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PasswordClause = new(@"PASSWORD\s*=|IDENTIFIED\s+BY", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ConnectionString = new(@"(?:Server|Data\s*Source)\s*=.*?(?:Password|Pwd)\s*=", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex NoCheckConstraint = new(@"NOCHECK\s+CONSTRAINT", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DisableTrigger = new(@"DISABLE\s+TRIGGER", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ForeignKeyChecksOff = new(@"FOREIGN_KEY_CHECKS\s*=\s*0", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DropDatabase = new(@"DROP\s+DATABASE", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<SqlFinding> Lint(string script, BreakwaterDatabaseProvider provider)
    {
        var sections = MigrationScriptSplitter.Split(script);
        var findings = new List<SqlFinding>();
        var locator = new LineLocator(script);

        LintStatements(script, provider, sections, locator, findings);
        LintRawText(script, provider, sections, findings);

        findings.Sort((a, b) => a.Line != b.Line ? a.Line.CompareTo(b.Line) : string.CompareOrdinal(a.RuleId, b.RuleId));
        return findings;
    }

    /// <summary>BW010, BW035, BW036 (tokenizer-driven), and BW019 (Postgres only).</summary>
    private static void LintStatements(
        string script,
        BreakwaterDatabaseProvider provider,
        IReadOnlyList<MigrationSection> sections,
        LineLocator locator,
        List<SqlFinding> findings)
    {
        var statements = SqlTokenizer.Tokenize(script);

        // BW036 (idempotent DROP IF EXISTS ... CREATE pairs) is resolved first so the DROP
        // statement it covers can be excluded from BW010, exactly as RuleCatalog.Suppresses does
        // for the analyzer (BW036/BW035 both take precedence over BW010 on the same statement -
        // most-specific-wins, see SqlIdempotentRedefineRule's and SqlStructuralChangeRule's doc
        // comments in Breakwater.Analyzers).
        var redefinedDropIndexes = new HashSet<int>();
        for (var i = 0; i < statements.Count; i++)
        {
            if (!SqlStatementRisk.TryGetDropIfExists(statements[i], out var dropKind, out var dropName))
            {
                continue;
            }

            for (var j = i + 1; j < statements.Count; j++)
            {
                if (SqlStatementRisk.TryGetCreate(statements[j], out var createKind, out var createName)
                    && string.Equals(createKind, dropKind, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(createName, dropName, StringComparison.OrdinalIgnoreCase))
                {
                    redefinedDropIndexes.Add(i);
                    var line = locator.LineOf(statements[i].Text);
                    findings.Add(new SqlFinding("BW036", "Suggestion", MigrationScriptSplitter.SectionFor(sections, line), line,
                        $"DROP {dropKind} IF EXISTS '{dropName}' is followed by a CREATE {dropKind} of the same name - a recognized idempotent-redefinition idiom, lower risk than an unpaired DROP"));
                    break;
                }
            }
        }

        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];

            // BW035 takes precedence over BW010 for the same statement (same reasoning as above).
            var structuralReason = SqlStatementRisk.EvaluateStructuralChange(statement);
            if (structuralReason is not null)
            {
                var line = locator.LineOf(statement.Text);
                findings.Add(new SqlFinding("BW035", "Warning", MigrationScriptSplitter.SectionFor(sections, line), line, structuralReason));
                continue;
            }

            if (!redefinedDropIndexes.Contains(i))
            {
                var reason = SqlStatementRisk.Evaluate(statement);
                if (reason is not null)
                {
                    var line = locator.LineOf(statement.Text);
                    findings.Add(new SqlFinding("BW010", "Warning", MigrationScriptSplitter.SectionFor(sections, line), line, reason));
                }
            }

            if (provider == BreakwaterDatabaseProvider.Postgres)
            {
                var postgresReason = SqlStatementRisk.EvaluatePostgresRisk(statement);
                if (postgresReason is not null)
                {
                    var line = locator.LineOf(statement.Text);
                    findings.Add(new SqlFinding("BW019", "Warning", MigrationScriptSplitter.SectionFor(sections, line), line, postgresReason));
                }
            }
        }
    }

    /// <summary>BW031 (SQL Server batch separators), BW033 (secrets), BW034 (disabled safety).</summary>
    private static void LintRawText(
        string script,
        BreakwaterDatabaseProvider provider,
        IReadOnlyList<MigrationSection> sections,
        List<SqlFinding> findings)
    {
        // GO/USE are SQL Server-only syntax, same gate SqlBatchSeparatorRule applies.
        if (provider == BreakwaterDatabaseProvider.SqlServer)
        {
            AddIfMatch(findings, sections, script, GoSeparator, "BW031", "Warning",
                m => "Raw SQL contains 'GO', a client-side batch separator that cannot be sent as one command");
            AddIfMatch(findings, sections, script, UseStatement, "BW031", "Warning",
                m => "Raw SQL contains 'USE', a client-side batch separator that cannot be sent as one command");
        }

        if (PasswordClause.IsMatch(script) || ConnectionString.IsMatch(script))
        {
            var match = PasswordClause.IsMatch(script) ? PasswordClause.Match(script) : ConnectionString.Match(script);
            AddSingle(findings, sections, script, match, "BW033", "Warning",
                "Raw SQL contains a credential (a PASSWORD/IDENTIFIED BY clause or a connection string)");
        }

        AddIfMatch(findings, sections, script, DropDatabase, "BW034", "Warning", m => "Raw SQL contains 'DROP DATABASE', which destroys data without anyone noticing");
        AddIfMatch(findings, sections, script, NoCheckConstraint, "BW034", "Warning", m => "Raw SQL contains 'NOCHECK CONSTRAINT', which lets data become invalid without anyone noticing");
        AddIfMatch(findings, sections, script, DisableTrigger, "BW034", "Warning", m => "Raw SQL contains 'DISABLE TRIGGER', which lets data become invalid without anyone noticing");
        AddIfMatch(findings, sections, script, ForeignKeyChecksOff, "BW034", "Warning", m => "Raw SQL contains 'SET FOREIGN_KEY_CHECKS = 0', which lets data become invalid without anyone noticing");
    }

    private static void AddIfMatch(List<SqlFinding> findings, IReadOnlyList<MigrationSection> sections, string script, Regex pattern, string ruleId, string severity, Func<Match, string> message)
    {
        var match = pattern.Match(script);
        if (match.Success)
        {
            AddSingle(findings, sections, script, match, ruleId, severity, message(match));
        }
    }

    private static void AddSingle(List<SqlFinding> findings, IReadOnlyList<MigrationSection> sections, string script, Match match, string ruleId, string severity, string message)
    {
        var line = LineLocator.LineOfOffset(script, match.Index);
        findings.Add(new SqlFinding(ruleId, severity, MigrationScriptSplitter.SectionFor(sections, line), line, message));
    }
}
