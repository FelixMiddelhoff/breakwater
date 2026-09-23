using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>BW003: renaming a column or table breaks application versions that use the old name.</summary>
internal sealed class RenameRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW003",
        "Renaming is not safe for a rolling deploy",
        "{0} '{1}' to '{2}' breaks application versions that still use the old name",
        "A rename is instant for the database but the previous application version keeps using the old " +
        "name and fails. Add the new column or table, copy the data, switch the code over, and drop the " +
        "old one in a later migration. EF Core generates renames by itself when you rename a property, " +
        "so check the generated migration.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        switch (operation.Kind)
        {
            case MigrationOperationKind.RenameColumn:
                return new object[]
                {
                    "RenameColumn",
                    operation.QualifiedTable + "." + operation.Column,
                    operation.NewName ?? MigrationOperation.UnknownName,
                };
            case MigrationOperationKind.RenameTable:
                return new object[]
                {
                    "RenameTable",
                    operation.QualifiedTable,
                    operation.NewName ?? MigrationOperation.UnknownName,
                };
            default:
                return null;
        }
    }
}
