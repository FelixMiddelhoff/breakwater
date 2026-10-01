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
    public void Missing_file_is_a_usage_error_not_an_unhandled_exception()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "--provider", "sqlserver", "does-not-exist.sql" }, stdout, stderr, new StringReader(string.Empty));

        Assert.Equal(2, exitCode);
        Assert.Contains("Could not read", stderr.ToString());
    }
}
