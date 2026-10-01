using System.IO;

namespace Breakwater.Tool;

/// <summary>
/// <c>breakwater-sql init</c>: writes a starter <c>.editorconfig</c> snippet listing every
/// <c>breakwater_*</c> key (commented out, with the same explanation the README gives each one)
/// so a new user can uncomment what they need instead of copying it by hand from the docs.
/// </summary>
internal static class InitCommand
{
    public const string Scaffold =
        """
        # Breakwater configuration - uncomment what you need.
        # Full explanation of each key: https://github.com/FelixMiddelhoff/breakwater#configure
        [*.cs]

        # Switch on the strict-only rules (BW011, BW020, BW030), silent by default.
        # breakwater_profile = strict

        # Name the database when it can't be auto-detected from a guard or annotation.
        # breakwater_provider = postgres   # sqlserver | postgres | sqlite | mysql | auto (default)

        # Downgrade BW001/BW002/BW003 to Info for a project that accepts deploy downtime.
        # breakwater_deploy_model = downtime_ok   # rolling (default) | downtime_ok

        # Adopting an existing project: skip every migration at or before this id.
        # breakwater_since_migration = 20260101000000_LastMigrationBeforeBreakwater

        # Downgrade table-lock rules to Info for tables known to be small (comma-separated).
        # breakwater_small_tables = FeatureFlags, AppSettings

        """;

    /// <summary>Returns the process exit code: 0 on success, 2 on a usage error (an existing file without --force).</summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        string? path = null;
        var force = false;

        foreach (var arg in args)
        {
            if (arg == "--force")
            {
                force = true;
            }
            else if (path is null)
            {
                path = arg;
            }
            else
            {
                stderr.WriteLine($"Unexpected extra argument '{arg}'.");
                stderr.WriteLine(Usage);
                return 2;
            }
        }

        if (path is null)
        {
            stdout.Write(Scaffold);
            return 0;
        }

        if (File.Exists(path) && !force)
        {
            stderr.WriteLine($"'{path}' already exists. Pass --force to overwrite, or omit a path to print to stdout instead.");
            return 2;
        }

        File.WriteAllText(path, Scaffold);
        stdout.WriteLine($"Wrote {path}");
        return 0;
    }

    public const string Usage = "Usage: breakwater-sql init [<path>] [--force]\nPrints the scaffold to stdout, or writes it to <path> (refuses to overwrite an existing file without --force).";
}
