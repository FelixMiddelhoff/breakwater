using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW008: adding a foreign key or check constraint to an existing table validates every existing
/// row while holding a lock.
/// </summary>
internal sealed class AddValidatingConstraintRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW008",
        "AddForeignKey/AddCheckConstraint validates the whole table under lock",
        "{0} '{1}' on '{2}' validates every existing row while holding a lock",
        "EF adds the constraint already validated, which means every row in the table is checked while " +
        "the lock is held; on a large table this can run for a long time. On PostgreSQL, add the constraint " +
        "with raw SQL using 'NOT VALID', then run 'VALIDATE CONSTRAINT' in a later migration. On SQL " +
        "Server, add it 'WITH NOCHECK', then run a separate 'CHECK CONSTRAINT' step.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind is not (MigrationOperationKind.AddForeignKey or MigrationOperationKind.AddCheckConstraint))
        {
            return null;
        }

        if (context.IsTableCreatedInThisMigration(operation.QualifiedTable))
        {
            return null;
        }

        var operationName = operation.Kind == MigrationOperationKind.AddForeignKey ? "AddForeignKey" : "AddCheckConstraint";
        return new object[] { operationName, operation.Column!, operation.QualifiedTable };
    }
}
