using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW006: an <c>AddColumn</c> that is NOT NULL without a default fails as soon as the table
/// already has rows, and an old application version keeps inserting rows without the new column.
/// </summary>
internal sealed class AddColumnNotNullRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW006",
        "AddColumn requires NOT NULL without a default",
        "AddColumn '{0}.{1}' is NOT NULL without a default: fails on a non-empty table",
        "Every existing row needs a value for the new column, and an old application version keeps " +
        "inserting rows that do not know about it either. Add the column as nullable and backfill, then " +
        "constrain it in a later migration, or give it a constant default here.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.AddColumn)
        {
            return null;
        }

        if (context.IsTableCreatedInThisMigration(operation.QualifiedTable))
        {
            return null;
        }

        var hasDefault = operation.HasDefaultValue || operation.HasDefaultValueSql;

        return !operation.Nullable && !hasDefault
            ? new object[] { operation.QualifiedTable, operation.Column! }
            : null;
    }
}
