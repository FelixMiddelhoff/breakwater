using System.Text.RegularExpressions;
using Breakwater.Analyzers.Configuration;
using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW031: raw SQL containing <c>GO</c> (SQL Server Management Studio/sqlcmd's batch separator) or
/// <c>USE</c> (a database switch) is not valid inside a single ADO.NET command: EF sends the whole
/// string as one command and it fails at runtime, not at build time.
/// </summary>
internal sealed class SqlBatchSeparatorRule : IMigrationRule
{
    // A line that is only "GO" (optionally followed by a repeat count), matching how SSMS/sqlcmd
    // recognize the batch separator: it must be alone on its line.
    private static readonly Regex GoSeparator = new(@"^[ \t]*GO[ \t]*(\d+)?[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex UseStatement = new(@"(?:^|;)\s*USE\s+[\[\w]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW031",
        "Raw SQL contains a GO/USE batch separator",
        "Sql(...) contains '{0}', a client-side batch separator that EF cannot send as one command",
        "'GO' and 'USE' are recognized by SSMS/sqlcmd, not by the ADO.NET command EF sends the string " +
        "through. The migration compiles and looks fine, then fails at runtime with a SQL syntax error. " +
        "Split the string into separate migrationBuilder.Sql(...) calls, one per batch.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.Sql || operation.SqlText is not { } sql)
        {
            return null;
        }

        // GO/USE are SQL Server-only syntax; a known non-SQL Server provider (from an explicit
        // breakwater_provider override, or an auto-detected guard/annotation) means this text is
        // not actually being sent to SQL Server, so stay silent.
        if (operation.DetectedProvider is BreakwaterDatabaseProvider.Postgres or BreakwaterDatabaseProvider.MySql or BreakwaterDatabaseProvider.Sqlite)
        {
            return null;
        }

        if (GoSeparator.IsMatch(sql))
        {
            return new object[] { "GO" };
        }

        if (UseStatement.IsMatch(sql))
        {
            return new object[] { "USE" };
        }

        return null;
    }
}
