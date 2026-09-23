using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW004: an <c>AlterColumn</c> that changes the CLR type, or narrows length, precision or
/// scale, can rewrite the table and fail on existing data. Widening (comparable the other way)
/// stays silent, per <c>old*</c> arguments EF already gives us.
/// </summary>
internal sealed class AlterColumnTypeChangeRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW004",
        "AlterColumn narrows the column and can fail or rewrite the table",
        "AlterColumn '{0}.{1}' changes the column in a way that can fail on existing data or rewrite the table",
        "Narrowing a type, length, precision or scale can truncate or reject existing values, and many " +
        "providers rewrite the whole table to apply the change. Add a new column, backfill it in batches, " +
        "then switch the code and drop the old column.");

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

        var narrows =
            (operation.OldClrType is not null && operation.ClrType is not null && operation.OldClrType != operation.ClrType) ||
            IsNarrowed(operation.MaxLength, operation.OldMaxLength) ||
            IsNarrowed(operation.Precision, operation.OldPrecision) ||
            IsNarrowed(operation.Scale, operation.OldScale);

        return narrows ? new object[] { operation.QualifiedTable, operation.Column! } : null;
    }

    /// <summary>True only when both sides are known and the new value is strictly smaller.</summary>
    private static bool IsNarrowed(int? value, int? oldValue) => value.HasValue && oldValue.HasValue && value.Value < oldValue.Value;
}
