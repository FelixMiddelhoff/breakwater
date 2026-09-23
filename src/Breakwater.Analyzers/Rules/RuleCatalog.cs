using System.Collections.Generic;
using System.Collections.Immutable;

namespace Breakwater.Analyzers.Rules;

/// <summary>Every operation-level rule the analyzer runs. BW011, BW028 and BW999 are wired up
/// separately in <see cref="Breakwater.Analyzers.MigrationAnalyzer"/> since they look outside a
/// single operation (BW011/BW028) or are the engine's own crash guard (BW999).</summary>
internal static class RuleCatalog
{
    public static ImmutableArray<IMigrationRule> All { get; } = ImmutableArray.Create<IMigrationRule>(
        new DropColumnRule(),
        new DropTableRule(),
        new RenameRule(),
        new AlterColumnTypeChangeRule(),
        new AlterColumnNotNullRule(),
        new AddColumnNotNullRule(),
        new AlterColumnCollationRule(),
        new AlterColumnIdentityRule(),
        new ProviderSpecificTypeChangeRule(),
        new AddColumnVolatileDefaultRule(),
        new CreateIndexOnlineRule(),
        new CreateUniqueIndexRule(),
        new AddValidatingConstraintRule(),
        new AddUniqueOrPrimaryKeyRule(),
        new DropConstraintRule(),
        new DropSchemaOrSequenceRule(),
        new AlterDatabaseRule(),
        new SqlUnsafeStatementRule(),
        new SqlPostgresUnsafeStatementRule(),
        new SqlBatchSeparatorRule(),
        new SqlSecretRule(),
        new SqlDisablesSafetyRule(),
        new PostgresLockTimeoutRule(),
        new DropAndAddColumnRule(),
        new DeleteDataRule(),
        new ForeignKeyPlaceholderDefaultRule(),
        new CascadeDeleteRule(),
        new DataVolumeRule(),
        new SchemaAndDataChangeRule(),
        new PartialFailureRiskRule(),
        new MixedRiskyOperationsRule());

    /// <summary>
    /// Rules that can all fire on the same <c>AlterColumn</c> call. Only the first match, in the
    /// order listed here (most specific first), is reported, per the "one finding per problem"
    /// noise policy.
    /// </summary>
    public static ImmutableArray<string> AlterColumnExclusiveGroup { get; } =
        ImmutableArray.Create("BW004", "BW005", "BW015", "BW026");

    /// <summary>
    /// Maps a rule id to the ids it suppresses when it also fires for the same operation
    /// (most-specific-wins). Unlike <see cref="AlterColumnExclusiveGroup"/> this is not
    /// symmetric: BW023 is strictly more specific than BW001 for the same <c>DropColumn</c> call.
    /// </summary>
    public static ImmutableDictionary<string, ImmutableArray<string>> Suppresses { get; } =
        new Dictionary<string, ImmutableArray<string>>
        {
            ["BW023"] = ImmutableArray.Create("BW001"),
        }.ToImmutableDictionary();
}
