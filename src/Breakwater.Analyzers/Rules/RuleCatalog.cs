using System.Collections.Immutable;

namespace Breakwater.Analyzers.Rules;

/// <summary>Every rule the analyzer runs, in rule-id order.</summary>
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
        new AddColumnVolatileDefaultRule(),
        new CreateIndexOnlineRule(),
        new AddValidatingConstraintRule(),
        new AddUniqueOrPrimaryKeyRule(),
        new DropConstraintRule(),
        new SqlUnsafeStatementRule());

    /// <summary>
    /// Rules that can both fire on the same <c>AlterColumn</c> call. Only the first match in
    /// catalog order is reported, per the "one finding per problem" noise policy.
    /// </summary>
    public static ImmutableArray<string> AlterColumnExclusiveGroup { get; } =
        ImmutableArray.Create("BW004", "BW005", "BW015");
}
