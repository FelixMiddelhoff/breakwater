using System.Text.RegularExpressions;
using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW033: raw SQL that creates a login/user with a password, or that embeds a connection string,
/// puts a credential straight into source control and into every environment that ever checks it
/// out.
/// </summary>
internal sealed class SqlSecretRule : IMigrationRule
{
    private static readonly Regex PasswordClause = new(@"PASSWORD\s*=|IDENTIFIED\s+BY", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ConnectionString = new(@"(?:Server|Data\s*Source)\s*=.*?(?:Password|Pwd)\s*=", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW033",
        "Raw SQL creates a login/user with a password, or embeds a connection string",
        "Sql(...) contains a credential (a PASSWORD/IDENTIFIED BY clause or a connection string)",
        "Whatever this migration writes to the database, it also writes to source control, and to " +
        "every environment and every developer machine that checks it out. Create the login/user through " +
        "a secret-managed deployment step instead, or generate the password separately and never commit it.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.Sql || operation.SqlText is not { } sql)
        {
            return null;
        }

        return PasswordClause.IsMatch(sql) || ConnectionString.IsMatch(sql) ? System.Array.Empty<object>() : null;
    }
}
