using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW025: an <c>AddForeignKey</c> on a column that was added in the same migration with a
/// placeholder default (<c>0</c>, <c>""</c>, <c>Guid.Empty</c>) points every existing row at a
/// parent that almost certainly does not exist for that placeholder value: the migration fails
/// halfway, or (if EF ever creates it unvalidated) the constraint is invalid from the start.
/// </summary>
internal sealed class ForeignKeyPlaceholderDefaultRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW025",
        "AddForeignKey references a column added with a placeholder default",
        "AddForeignKey '{0}' on '{1}.{2}' references a column added in this migration with a placeholder default",
        "Every existing row now has the placeholder value (0, an empty string, or Guid.Empty) in the " +
        "new column, and the foreign key requires a matching parent row for it. Unless a parent row with " +
        "that exact key already exists, the migration fails when it validates the constraint. Backfill the " +
        "column with real parent ids before adding the foreign key, or add it as NOT VALID and validate " +
        "once the backfill is done.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.AddForeignKey || operation.ForeignKeyColumn is not { } column)
        {
            return null;
        }

        if (!context.IsPlaceholderDefaultColumn(operation.QualifiedTable, column))
        {
            return null;
        }

        return new object[] { operation.Column!, operation.QualifiedTable, column };
    }
}
