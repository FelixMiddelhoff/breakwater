using System;
using System.Collections.Generic;
using System.IO;

namespace Breakwater.Tool;

/// <summary>
/// CLI entry point for <c>breakwater-sql</c>: lints the SQL script <c>dotnet ef migrations
/// script</c> produces against the same risky shapes Breakwater's analyzer flags in inline
/// <c>Sql(...)</c> calls. <see cref="Run"/> holds all the logic and writes only to the
/// <see cref="TextWriter"/>s it is given, so tests can exercise the whole CLI (argument parsing,
/// stdin/file reading, exit code) without touching the real console.
/// </summary>
public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error, Console.In);

    /// <summary>Returns the process exit code: 0 when clean, 1 when findings were reported, 2 on a usage error.</summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, TextReader stdin)
    {
        if (args.Length > 0 && args[0] == "init")
        {
            return InitCommand.Run(args[1..], stdout, stderr);
        }

        if (!CliArguments.TryParse(args, out var parsed, out var usageError))
        {
            stderr.WriteLine(usageError);
            stderr.WriteLine(CliArguments.Usage);
            return 2;
        }

        string script;
        try
        {
            script = parsed.Path is null
                ? stdin.ReadToEnd()
                : File.ReadAllText(parsed.Path);
        }
        catch (IOException ex)
        {
            stderr.WriteLine($"Could not read '{parsed.Path}': {ex.Message}");
            return 2;
        }

        if (!FindingIgnores.TryBuild(parsed.IgnoreFlags, parsed.IgnoreFilePath, out var ignores, out var ignoreError))
        {
            stderr.WriteLine(ignoreError);
            return 2;
        }

        var findings = SqlScriptLinter.Lint(script, parsed.Provider);

        var suppressedCount = 0;
        if (!ignores.IsEmpty)
        {
            var remaining = new List<SqlFinding>(findings.Count);
            foreach (var finding in findings)
            {
                if (ignores.Suppresses(finding))
                {
                    suppressedCount++;
                }
                else
                {
                    remaining.Add(finding);
                }
            }

            findings = remaining;
        }

        if (parsed.Json)
        {
            OutputFormatter.WriteJson(stdout, findings);
        }
        else
        {
            OutputFormatter.WriteTable(stdout, findings);
        }

        if (suppressedCount > 0)
        {
            stdout.WriteLine($"{suppressedCount} finding(s) suppressed via --ignore.");
        }

        return findings.Count > 0 ? 1 : 0;
    }
}
