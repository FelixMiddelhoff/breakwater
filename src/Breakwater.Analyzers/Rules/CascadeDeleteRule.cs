using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW030: <c>AddForeignKey(..., onDelete: ReferentialAction.Cascade)</c> means one delete can wipe
/// child tables, and SQL Server rejects multiple cascade paths at runtime. Off by default: this is
/// EF's default for required relationships and is far too common to warn on by default.
/// </summary>
internal sealed class CascadeDeleteRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW030",
        "AddForeignKey with onDelete: Cascade",
        "AddForeignKey '{0}' on '{1}' cascades deletes: removing a parent row also removes every matching child row",
        "A cascade delete can silently remove far more data than the person running the delete " +
        "expected, and SQL Server rejects a schema with more than one cascade path into the same table. " +
        "Confirm this is intentional; consider ReferentialAction.Restrict and deleting children explicitly.",
        isEnabledByDefault: false);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.AddForeignKey || !operation.OnDeleteCascade)
        {
            return null;
        }

        return new object[] { operation.Column!, operation.QualifiedTable };
    }
}
