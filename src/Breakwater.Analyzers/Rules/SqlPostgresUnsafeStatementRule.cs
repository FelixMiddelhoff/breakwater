using Breakwater.Analyzers.Operations;
using Breakwater.Analyzers.Sql;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW019: the same class of risk as BW007 (unsafe index build) and BW004 (unsafe type change),
/// but hidden inside raw SQL on Postgres, so those rules never see it. Only fires inside a known
/// <c>IsNpgsql()</c> guard, per "silent when unsure" for provider-specific rules.
/// </summary>
internal sealed class SqlPostgresUnsafeStatementRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW019",
        "Raw SQL on PostgreSQL builds an index, locks a table, or rewrites a table under lock",
        "Sql(...) statement '{0}': {1}",
        "Raw SQL bypasses migrationBuilder's own operations, so Breakwater cannot see the risk unless " +
        "it looks at the SQL text itself. This statement takes a lock for longer than it needs to; see " +
        "the message for the safer form.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.Sql || operation.SqlText is null || !operation.IsNpgsql)
        {
            return null;
        }

        foreach (var statement in SqlTokenizer.Tokenize(operation.SqlText))
        {
            var reason = SqlStatementRisk.EvaluatePostgresRisk(statement);
            if (reason is not null)
            {
                return new object[] { statement.Text, reason };
            }
        }

        return null;
    }
}
