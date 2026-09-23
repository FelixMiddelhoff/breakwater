using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW023: a <c>DropColumn</c> and an <c>AddColumn</c> on the same table in one migration is the
/// shape EF generates for a renamed-and-retyped property it did not recognize as a rename: data in
/// the old column is silently lost. More specific than BW001 for the same <c>DropColumn</c> call,
/// so it suppresses BW001 (most-specific-wins, see <c>RuleCatalog.Suppresses</c>).
/// </summary>
internal sealed class DropAndAddColumnRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW023",
        "DropColumn and AddColumn on the same table look like an undetected rename",
        "DropColumn '{0}.{1}' is paired with an AddColumn on the same table in this migration: likely a rename EF did not detect",
        "When a property is renamed and its type also changed, EF Core sometimes cannot tell it apart " +
        "from removing one property and adding another, and generates a DropColumn plus an AddColumn " +
        "instead of a RenameColumn. The data in the old column is lost. If this really is a rename, use " +
        "'migrationBuilder.RenameColumn' instead (add a cast/conversion migration afterwards if the type " +
        "changed too); if it is really two unrelated changes, this is a false positive.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.DropColumn)
        {
            return null;
        }

        return context.HasDropAndAddColumn(operation.QualifiedTable)
            ? new object[] { operation.QualifiedTable, operation.Column! }
            : null;
    }
}
