using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW012: seeding or changing many rows through <c>InsertData</c>/<c>UpdateData</c>/<c>DeleteData</c>
/// runs as one long transaction. Only fires when the row count is statically countable and over
/// the threshold; an unknown row count stays silent, per "silent when unsure".
/// </summary>
internal sealed class DataVolumeRule : IMigrationRule
{
    private const int RowThreshold = 50;

    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW012",
        "InsertData/UpdateData/DeleteData seeds or changes many rows in one migration",
        "{0} on '{1}' changes {2} rows in one transaction",
        "A large data change inside a migration holds its lock and transaction open for as long as " +
        "the whole batch takes, which can cause lock escalation and replication lag. Consider batching " +
        "the change outside the migration, or splitting it across several migrations.",
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

        if (operationName is null || operation.RowCount is not { } rowCount || rowCount <= RowThreshold)
        {
            return null;
        }

        return new object[] { operationName, operation.QualifiedTable, rowCount };
    }
}
