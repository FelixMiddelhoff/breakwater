using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW018: dropping a schema/sequence, or altering/restarting a sequence, can lose data (a
/// sequence's current value) or produce duplicate ids while an old application version still runs.
/// </summary>
internal sealed class DropSchemaOrSequenceRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW018",
        "Dropping or restarting a schema/sequence can lose data or duplicate ids",
        "{0} '{1}' can lose data (a sequence's current value) or produce duplicate ids while an old application version still runs",
        "Dropping a schema removes everything in it with no way back; dropping, altering or restarting " +
        "a sequence can reset or lose its current value, and an application version still running against " +
        "the old value can hand out ids that collide with rows already written under the new one.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        var operationName = operation.Kind switch
        {
            MigrationOperationKind.DropSchema => "DropSchema",
            MigrationOperationKind.DropSequence => "DropSequence",
            MigrationOperationKind.AlterSequence => "AlterSequence/RestartSequence",
            _ => null,
        };

        return operationName is null ? null : new object[] { operationName, operation.QualifiedTable };
    }
}
