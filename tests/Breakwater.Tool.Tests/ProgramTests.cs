using System.IO;
using Breakwater.Tool;
using Xunit;

namespace Breakwater.Tool.Tests;

/// <summary>End-to-end coverage of the CLI: argument parsing, stdin/file reading, and exit codes.</summary>
public class ProgramTests
{
    [Fact]
    public void Clean_script_from_a_file_exits_zero()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "DELETE FROM Users WHERE Id = 1;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exitCode = Program.Run(new[] { "--provider", "sqlserver", path }, stdout, stderr, new StringReader(string.Empty));

            Assert.Equal(0, exitCode);
            Assert.Contains("No issues found.", stdout.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Unsafe_script_from_a_file_exits_nonzero_and_lists_the_finding()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "TRUNCATE TABLE Orders;");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exitCode = Program.Run(new[] { "--provider", "postgres", path }, stdout, stderr, new StringReader(string.Empty));

            Assert.Equal(1, exitCode);
            Assert.Contains("BW010", stdout.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Script_is_read_from_stdin_when_no_file_is_given()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "--provider", "mysql" }, stdout, stderr, new StringReader("DROP TABLE Orders;"));

        Assert.Equal(1, exitCode);
        Assert.Contains("BW010", stdout.ToString());
    }

    [Fact]
    public void Missing_provider_is_a_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(System.Array.Empty<string>(), stdout, stderr, new StringReader(string.Empty));

        Assert.Equal(2, exitCode);
        Assert.Contains("--provider is required", stderr.ToString());
    }

    [Fact]
    public void Unrecognized_provider_is_a_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "--provider", "oracle" }, stdout, stderr, new StringReader(string.Empty));

        Assert.Equal(2, exitCode);
        Assert.Contains("Unrecognized provider", stderr.ToString());
    }

    [Fact]
    public void Json_format_emits_parseable_json_with_the_rule_id()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "--provider", "sqlserver", "--format", "json" }, stdout, stderr, new StringReader("TRUNCATE TABLE Orders;"));

        Assert.Equal(1, exitCode);
        Assert.Contains("\"ruleId\": \"BW010\"", stdout.ToString(), System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Suggestion_only_script_exits_zero_under_the_default_fail_on()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // DROP PROCEDURE IF EXISTS ... immediately followed by CREATE PROCEDURE of the same name
        // only triggers BW036 (Suggestion tier) - the idempotent-redefinition idiom suppresses
        // BW010's Warning for the same DROP (see SqlScriptLinter's doc comment).
        var exitCode = Program.Run(
            new[] { "--provider", "sqlserver" },
            stdout,
            stderr,
            new StringReader("DROP PROCEDURE IF EXISTS dbo.GetOrders; CREATE PROCEDURE dbo.GetOrders AS SELECT 1;"));

        Assert.Equal(0, exitCode);
        Assert.Contains("BW036", stdout.ToString());
    }

    [Fact]
    public void Suggestion_only_script_exits_zero_with_explicit_fail_on_warning()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(
            new[] { "--provider", "sqlserver", "--fail-on", "warning" },
            stdout,
            stderr,
            new StringReader("DROP PROCEDURE IF EXISTS dbo.GetOrders; CREATE PROCEDURE dbo.GetOrders AS SELECT 1;"));

        Assert.Equal(0, exitCode);
        Assert.Contains("BW036", stdout.ToString());
    }

    [Fact]
    public void Suggestion_only_script_exits_nonzero_with_fail_on_suggestion()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(
            new[] { "--provider", "sqlserver", "--fail-on", "suggestion" },
            stdout,
            stderr,
            new StringReader("DROP PROCEDURE IF EXISTS dbo.GetOrders; CREATE PROCEDURE dbo.GetOrders AS SELECT 1;"));

        Assert.Equal(1, exitCode);
        Assert.Contains("BW036", stdout.ToString());
    }

    [Fact]
    public void Warning_tier_script_exits_nonzero_under_the_default_fail_on_matching_prior_behavior()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Same scenario as Unsafe_script_from_a_file_exits_nonzero_and_lists_the_finding, pinned
        // here explicitly against the pre-existing "any finding exits 1" contract now that
        // --fail-on exists: a Warning-tier finding (BW010) must still fail by default.
        var exitCode = Program.Run(new[] { "--provider", "postgres" }, stdout, stderr, new StringReader("TRUNCATE TABLE Orders;"));

        Assert.Equal(1, exitCode);
        Assert.Contains("BW010", stdout.ToString());
    }

    [Fact]
    public void Unrecognized_fail_on_value_is_a_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "--provider", "sqlserver", "--fail-on", "error" }, stdout, stderr, new StringReader(string.Empty));

        Assert.Equal(2, exitCode);
        Assert.Contains("Unrecognized --fail-on value", stderr.ToString());
    }

    [Fact]
    public void Missing_file_is_a_usage_error_not_an_unhandled_exception()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "--provider", "sqlserver", "does-not-exist.sql" }, stdout, stderr, new StringReader(string.Empty));

        Assert.Equal(2, exitCode);
        Assert.Contains("Could not read", stderr.ToString());
    }

    [Fact]
    public void Ignored_finding_is_excluded_from_output_and_exit_code()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // "TRUNCATE TABLE Orders;" is a single line script, so the finding is on line 1.
        var exitCode = Program.Run(
            new[] { "--provider", "postgres", "--ignore", "BW010:1" },
            stdout,
            stderr,
            new StringReader("TRUNCATE TABLE Orders;"));

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("BW010", stdout.ToString());
        Assert.Contains("No issues found.", stdout.ToString());
        Assert.Contains("1 finding(s) suppressed", stdout.ToString());
    }

    [Fact]
    public void Ignore_for_a_different_line_does_not_suppress_the_finding()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(
            new[] { "--provider", "postgres", "--ignore", "BW010:99" },
            stdout,
            stderr,
            new StringReader("TRUNCATE TABLE Orders;"));

        Assert.Equal(1, exitCode);
        Assert.Contains("BW010", stdout.ToString());
    }

    [Fact]
    public void Ignore_file_with_comments_and_blank_lines_suppresses_matching_findings()
    {
        var ignoreFilePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(ignoreFilePath, "# known-safe truncate in seed migration\n\nBW010:1\n");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exitCode = Program.Run(
                new[] { "--provider", "postgres", "--ignore-file", ignoreFilePath },
                stdout,
                stderr,
                new StringReader("TRUNCATE TABLE Orders;"));

            Assert.Equal(0, exitCode);
            Assert.Contains("No issues found.", stdout.ToString());
        }
        finally
        {
            File.Delete(ignoreFilePath);
        }
    }

    [Fact]
    public void Malformed_ignore_value_is_a_clean_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(
            new[] { "--provider", "postgres", "--ignore", "BW010" },
            stdout,
            stderr,
            new StringReader("TRUNCATE TABLE Orders;"));

        Assert.Equal(2, exitCode);
        Assert.Contains("Invalid --ignore value", stderr.ToString());
    }

    [Fact]
    public void Nonexistent_ignore_file_is_a_clean_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(
            new[] { "--provider", "postgres", "--ignore-file", "does-not-exist-ignore.txt" },
            stdout,
            stderr,
            new StringReader("TRUNCATE TABLE Orders;"));

        Assert.Equal(2, exitCode);
        Assert.Contains("Could not read ignore file", stderr.ToString());
    }
}
