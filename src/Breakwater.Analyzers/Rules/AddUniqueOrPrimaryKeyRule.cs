using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW009: adding a unique constraint or primary key to an existing table builds an index under
/// lock and fails if the existing data already has duplicates.
/// </summary>
internal sealed class AddUniqueOrPrimaryKeyRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW009",
        "AddUniqueConstraint/AddPrimaryKey builds an index under lock",
        "{0} '{1}' on '{2}' builds an index while holding a lock, and fails if a duplicate already exists",
        "Building the backing index holds a lock for as long as it takes, and the whole migration fails " +
        "if any two existing rows already collide - including rows an old application version keeps " +
        "inserting while it does not yet know about the new constraint. Create a unique index " +
        "concurrently first (outside a transaction) and verify it, then attach the constraint using that " +
        "index in a later migration.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind is not (MigrationOperationKind.AddUniqueConstraint or MigrationOperationKind.AddPrimaryKey))
        {
            return null;
        }

        if (context.IsTableCreatedInThisMigration(operation.QualifiedTable))
        {
            return null;
        }

        var operationName = operation.Kind == MigrationOperationKind.AddUniqueConstraint ? "AddUniqueConstraint" : "AddPrimaryKey";
        return new object[] { operationName, operation.Column!, operation.QualifiedTable };
    }
}
