using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW013: a migration that changes the schema and changes data holds the schema lock for the
/// whole data change too, not just the schema change.
/// </summary>
internal sealed class SchemaAndDataChangeRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW013",
        "Migration mixes a schema change with a data change",
        "{0} on '{1}' shares a transaction with a schema change in the same migration",
        "One migration, one transaction: the schema change's lock is held for as long as the data " +
        "change takes too. Split the schema change and the data change into separate migrations so " +
        "each holds its lock for as little time as possible.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        var operationName = operation.Kind switch
        {
            MigrationOperationKind.InsertData => "InsertData",
            MigrationOperationKind.UpdateData => "UpdateData",
            MigrationOperationKind.DeleteData => "DeleteData",
            _ => null,
        };

        if (operationName is null || !context.HasSchemaOperation)
        {
            return null;
        }

        return new object[] { operationName, operation.QualifiedTable };
    }
}
