using System.IO;
using Breakwater.Tool;
using Xunit;

namespace Breakwater.Tool.Tests;

/// <summary>Covers <c>breakwater-sql init</c>: scaffold to stdout, to a file, and the overwrite guard.</summary>
public class InitCommandTests
{
    [Fact]
    public void No_path_prints_the_scaffold_to_stdout()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "init" }, stdout, stderr, new StringReader(string.Empty));

        Assert.Equal(0, exitCode);
        Assert.Contains("breakwater_profile = strict", stdout.ToString());
        Assert.Contains("breakwater_since_migration", stdout.ToString());
    }

    [Fact]
    public void A_path_writes_the_scaffold_to_that_file()
    {
        var path = Path.GetTempFileName();
        File.Delete(path);
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exitCode = Program.Run(new[] { "init", path }, stdout, stderr, new StringReader(string.Empty));

            Assert.Equal(0, exitCode);
            Assert.Contains("breakwater_provider", File.ReadAllText(path));
            Assert.Contains("Wrote", stdout.ToString());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void An_existing_file_without_force_is_a_usage_error()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing content");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exitCode = Program.Run(new[] { "init", path }, stdout, stderr, new StringReader(string.Empty));

            Assert.Equal(2, exitCode);
            Assert.Contains("already exists", stderr.ToString());
            Assert.Equal("existing content", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void An_existing_file_with_force_is_overwritten()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "existing content");
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exitCode = Program.Run(new[] { "init", path, "--force" }, stdout, stderr, new StringReader(string.Empty));

            Assert.Equal(0, exitCode);
            Assert.Contains("breakwater_profile", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void An_extra_argument_is_a_usage_error()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "init", "a.editorconfig", "b.editorconfig" }, stdout, stderr, new StringReader(string.Empty));

        Assert.Equal(2, exitCode);
        Assert.Contains("Unexpected extra argument", stderr.ToString());
    }
}
