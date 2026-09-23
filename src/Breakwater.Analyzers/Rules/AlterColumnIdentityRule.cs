using System.Linq;
using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW026: an <c>AlterColumn</c> that adds or removes identity/value-generation (a chained
/// <c>.Annotation("...Identity...", ...)</c> or <c>.Annotation("...ValueGenerationStrategy...", ...)</c>
/// not matched by the equivalent <c>.OldAnnotation</c>, or vice versa) cannot be applied in place on
/// most providers: the generated script fails or the table is rebuilt. Shares one <c>AlterColumn</c>
/// call with BW004/BW005/BW015; see <c>RuleCatalog.AlterColumnExclusiveGroup</c>.
/// </summary>
internal sealed class AlterColumnIdentityRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW026",
        "AlterColumn adds or removes identity/value generation",
        "AlterColumn '{0}.{1}' adds or removes identity/value generation, which cannot be applied in place on most providers",
        "Value generation (identity, sequences, ...) is baked into the column's storage on most " +
        "providers; changing it cannot be done with a plain ALTER COLUMN and the generated script either " +
        "fails or rebuilds the whole table. Add a new column with the desired generation, backfill it, " +
        "then switch the code and drop the old column.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.AlterColumn)
        {
            return null;
        }

        var addedGeneration = operation.AnnotationNames.Any(IsGenerationAnnotation) && !operation.OldAnnotationNames.Any(IsGenerationAnnotation);
        var removedGeneration = operation.OldAnnotationNames.Any(IsGenerationAnnotation) && !operation.AnnotationNames.Any(IsGenerationAnnotation);

        return addedGeneration || removedGeneration
            ? new object[] { operation.QualifiedTable, operation.Column! }
            : null;
    }

    private static bool IsGenerationAnnotation(string name) =>
        name.IndexOf("Identity", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("ValueGenerationStrategy", System.StringComparison.OrdinalIgnoreCase) >= 0;
}
