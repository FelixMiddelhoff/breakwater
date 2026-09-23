using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW029: <c>suppressTransaction: true</c>, or any migration on MySQL (where DDL auto-commits
/// statement by statement), with more than one operation: a failure partway through leaves the
/// database half migrated, with nothing to roll back. Fires once per migration, on the first
/// recognized operation, so a multi-operation migration is not reported once per operation.
/// </summary>
internal sealed class PartialFailureRiskRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW029",
        "Migration has no transaction to roll back to on failure",
        "This migration runs {0} without a wrapping transaction; a failure partway through leaves the database half migrated",
        "Either SuppressTransaction is set, or the migration targets MySQL, where DDL statements each " +
        "commit on their own. If any operation after the first fails, the earlier ones already happened " +
        "and there is no transaction to roll back - the database is left in a state that matches neither " +
        "the old nor the new migration. Keep migrations with several operations small, and verify each one " +
        "individually before combining them.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (context.OperationCount <= 1 || context.FirstOperationLocation is not { } firstLocation || !firstLocation.Equals(operation.Location))
        {
            return null;
        }

        if (!context.SuppressTransaction && !operation.IsMySql)
        {
            return null;
        }

        return new object[] { context.OperationCount + " operations" };
    }
}
