using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW017: dropping a primary key, unique constraint, check constraint or foreign key removes a
/// guarantee that code or data may still rely on.
/// </summary>
internal sealed class DropConstraintRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW017",
        "Dropping a constraint removes a guarantee the application may rely on",
        "{0} '{1}' on '{2}' removes a guarantee that code or data may still depend on",
        "Once the constraint is gone, nothing stops rows that violate it, and an old application " +
        "version (or code that never expected the constraint to disappear) may depend on it still " +
        "holding. Confirm the constraint is really unused before dropping it, and consider keeping the " +
        "backing index if queries still rely on it.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        var operationName = operation.Kind switch
        {
            MigrationOperationKind.DropPrimaryKey => "DropPrimaryKey",
            MigrationOperationKind.DropUniqueConstraint => "DropUniqueConstraint",
            MigrationOperationKind.DropCheckConstraint => "DropCheckConstraint",
            MigrationOperationKind.DropForeignKey => "DropForeignKey",
            _ => null,
        };

        return operationName is null
            ? null
            : new object[] { operationName, operation.Column!, operation.QualifiedTable };
    }
}
