using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>BW021: <c>AlterDatabase</c> (collation, edition, ...) is database-wide and often needs exclusive access.</summary>
internal sealed class AlterDatabaseRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW021",
        "AlterDatabase affects the whole database",
        "AlterDatabase changes the whole database and often needs exclusive access to run",
        "A database-level change (collation, edition, ...) can require no other connections be active, " +
        "and affects every table and every other application sharing the database, not just this one.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        return operation.Kind == MigrationOperationKind.AlterDatabase ? System.Array.Empty<object>() : null;
    }
}
