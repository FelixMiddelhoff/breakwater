using System.Collections.Generic;
using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW014: a Postgres <c>AlterColumn</c> that changes between two non-primitive CLR types (the
/// shape a mapped Postgres enum change takes) can fail or rewrite the table inside the migration's
/// transaction. Narrow and conservative: only fires inside a known <c>IsNpgsql()</c> guard, and
/// only when both the old and new CLR types are known and neither looks like a built-in .NET type
/// (a custom/enum type on both sides is the pattern that is actually provider-specific; a plain
/// primitive-to-primitive change is already BW004's job). Provider unknown or either type unknown:
/// silent, per "silent when unsure".
/// </summary>
internal sealed class ProviderSpecificTypeChangeRule : IMigrationRule
{
    private static readonly HashSet<string> PrimitiveClrTypes = new()
    {
        "string", "int", "long", "short", "byte", "bool", "double", "float", "decimal",
        "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Guid",
        "byte[]", "char", "uint", "ulong", "ushort", "sbyte",
    };

    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW014",
        "AlterColumn changes a provider-specific type",
        "AlterColumn '{0}.{1}' changes a mapped type from '{2}' to '{3}' on PostgreSQL",
        "Changing the CLR type behind a mapped Postgres type (for example a mapped enum) inside the " +
        "migration's transaction can fail or force a table rewrite, depending on the provider version. " +
        "Verify the generated SQL and, if needed, run the type change outside the migration's transaction.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.AlterColumn || !operation.IsNpgsql)
        {
            return null;
        }

        if (operation.OldClrType is not { } oldType || operation.ClrType is not { } newType || oldType == newType)
        {
            return null;
        }

        if (PrimitiveClrTypes.Contains(oldType) || PrimitiveClrTypes.Contains(newType))
        {
            return null;
        }

        return new object[] { operation.QualifiedTable, operation.Column!, oldType, newType };
    }
}
