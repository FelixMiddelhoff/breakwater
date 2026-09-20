using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>BW001: dropping a column loses data and breaks application versions that still use it.</summary>
internal sealed class DropColumnRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW001",
        "Dropping a column is not safe for a rolling deploy",
        "DropColumn '{0}.{1}' loses data and breaks application versions that still read the column",
        "The data in the column is gone for good. During a rolling deploy the previous application " +
        "version keeps running against the new schema and fails on every query that mentions the column. " +
        "Stop using the column first, deploy, and drop it in a later migration.");

    public object[]? Check(MigrationOperation operation)
    {
        return operation.Kind == MigrationOperationKind.DropColumn
            ? new object[] { operation.QualifiedTable, operation.Column! }
            : null;
    }
}
