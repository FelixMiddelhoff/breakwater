using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW022: a migration that mixes several risky operations on the same table compounds their lock
/// time. Advisory only, and only appears when nothing more specific already fired for this
/// operation (enforced in <c>MigrationAnalyzer</c>, since that needs to see every other rule's
/// result for the same operation first). Fires once per table, on the last risky operation seen
/// for it.
/// </summary>
internal sealed class MixedRiskyOperationsRule : IMigrationRule
{
    private const int Threshold = 3;

    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW022",
        "Migration mixes several risky operations on the same table",
        "'{0}' has {1} risky operations in this migration; consider splitting it across several migrations",
        "Several locking operations on the same table in one migration add up: each one's lock time " +
        "stacks on the others. Splitting them into separate migrations lets each one run (and be rolled " +
        "back) independently, and keeps any single lock as short as possible.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        var count = context.RiskyOperationCount(operation.QualifiedTable);
        if (count < Threshold || !context.IsLastRiskyOperationForTable(operation.QualifiedTable, operation.Location))
        {
            return null;
        }

        return new object[] { operation.QualifiedTable, count };
    }
}
