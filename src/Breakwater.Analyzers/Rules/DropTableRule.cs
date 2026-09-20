using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>BW002: dropping a table loses data and breaks application versions that still use it.</summary>
internal sealed class DropTableRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW002",
        "Dropping a table is not safe for a rolling deploy",
        "DropTable '{0}' loses data and breaks application versions that still read the table",
        "The data in the table is gone for good. During a rolling deploy the previous application " +
        "version keeps running against the new schema and fails on every query that mentions the table. " +
        "Stop using the table first, deploy, and drop it in a later migration.");

    public object[]? Check(MigrationOperation operation)
    {
        return operation.Kind == MigrationOperationKind.DropTable
            ? new object[] { operation.QualifiedTable }
            : null;
    }
}
