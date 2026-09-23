using Breakwater.Analyzers.Operations;
using Breakwater.Analyzers.Sql;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW010: raw SQL that runs an UPDATE/DELETE without a WHERE clause, a TRUNCATE, or a DROP holds
/// a lock and a long transaction against the whole table (or drops it outright). The SQL text
/// must be a compile-time constant (plain/verbatim/raw literal, constant interpolation, or
/// concatenation of constants); anything else is not statically knowable and stays silent.
/// </summary>
internal sealed class SqlUnsafeStatementRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW010",
        "Raw SQL runs an unfiltered UPDATE/DELETE, a TRUNCATE, or a DROP",
        "Sql(...) statement '{0}': {1}",
        "A statement like this holds a lock and a long transaction for as long as it takes to touch " +
        "every row (or drops the object outright), which can cause replication lag and lock escalation " +
        "on a large table. Batch the change outside the migration, or in chunks with its own WHERE " +
        "clause per batch.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.Sql || operation.SqlText is null)
        {
            return null;
        }

        // Each statement in a multi-statement Sql(...) call is tokenized and judged on its own;
        // the first unsafe one found is reported (one finding per problem, same as every other
        // rule here reports once per operation).
        foreach (var statement in SqlTokenizer.Tokenize(operation.SqlText))
        {
            var reason = SqlStatementRisk.Evaluate(statement);
            if (reason is not null)
            {
                return new object[] { statement.Text, reason };
            }
        }

        return null;
    }
}
