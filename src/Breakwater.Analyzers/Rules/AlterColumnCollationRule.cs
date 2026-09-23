using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>BW015: changing a column's collation rewrites the table and its indexes.</summary>
internal sealed class AlterColumnCollationRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW015",
        "AlterColumn changes the collation",
        "AlterColumn '{0}.{1}' changes collation from '{2}' to '{3}': the table and its indexes are rewritten",
        "SQL Server and most other providers have to rewrite the column's data and every index that " +
        "touches it to apply a new collation. On a large table this holds a lock for a long time. Consider " +
        "whether the comparison behaviour can instead be handled in application code or a new column.");

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

        if (operation.Collation is null || operation.OldCollation is null)
        {
            return null;
        }

        return operation.Collation != operation.OldCollation
            ? new object[] { operation.QualifiedTable, operation.Column!, operation.OldCollation, operation.Collation }
            : null;
    }
}
