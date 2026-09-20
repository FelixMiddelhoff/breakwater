using System.Collections.Immutable;

namespace Breakwater.Analyzers.Rules;

/// <summary>Every rule the analyzer runs, in rule-id order.</summary>
internal static class RuleCatalog
{
    public static ImmutableArray<IMigrationRule> All { get; } = ImmutableArray.Create<IMigrationRule>(
        new DropColumnRule(),
        new DropTableRule(),
        new RenameRule());
}
