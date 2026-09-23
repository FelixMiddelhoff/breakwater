using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW005: an <c>AlterColumn</c> that goes from nullable to not-null, with no default value to
/// backfill existing rows, fails the moment a NULL already exists.
/// </summary>
internal sealed class AlterColumnNotNullRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW005",
        "AlterColumn requires NOT NULL without a default or backfill",
        "AlterColumn '{0}.{1}' becomes NOT NULL without a default: fails if a NULL already exists",
        "The constraint is validated against every existing row. Any row with NULL in this column, " +
        "including rows an old application version is still inserting during a rolling deploy, makes the " +
        "migration fail. Backfill the column in a separate migration first, then constrain it, or give it " +
        "a default value here.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.AlterColumn)
        {
            return null;
        }

        if (context.IsTableCreatedInThisMigration(operation.QualifiedTable))
        {
            return null;
        }

        var tightensToNotNull = !operation.Nullable && operation.OldNullable;
        var hasBackfill = operation.HasDefaultValue || operation.HasDefaultValueSql;

        return tightensToNotNull && !hasBackfill
            ? new object[] { operation.QualifiedTable, operation.Column! }
            : null;
    }
}
