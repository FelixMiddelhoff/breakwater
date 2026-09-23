using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW027: a unique index on an existing table fails outright on any duplicate data, while holding
/// a lock for the build. Silent for a table created in this migration (already covered by BW007's
/// new-table silence) and for a column added nullable in this migration, which has no existing data
/// to conflict on.
/// </summary>
internal sealed class CreateUniqueIndexRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW027",
        "CreateIndex(unique: true) can fail on existing duplicate data",
        "CreateIndex '{0}' on '{1}' is unique and can fail if existing rows already have duplicate values",
        "Unlike a plain index, a unique index fails the whole migration if any two existing rows already " +
        "share a value, and it still holds a lock while it builds. Check for duplicates first (or clean " +
        "them up), and consider building the index concurrently/online before adding uniqueness.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.CreateIndex || !operation.Unique)
        {
            return null;
        }

        if (context.IsTableCreatedInThisMigration(operation.QualifiedTable))
        {
            return null;
        }

        if (operation.ForeignKeyColumn is { } column && context.IsNullableAddedColumn(operation.QualifiedTable, column))
        {
            return null;
        }

        return new object[] { operation.Column!, operation.QualifiedTable };
    }
}
