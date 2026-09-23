using System;
using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW016: on PostgreSQL, a constant default is applied as metadata only (instant), but a
/// volatile default such as <c>now()</c> or <c>gen_random_uuid()</c> has to be computed and
/// written for every existing row, rewriting the whole table.
/// </summary>
internal sealed class AddColumnVolatileDefaultRule : IMigrationRule
{
    // Common volatile built-ins. Not exhaustive by design: only flag the well-known ones so the
    // rule never guesses about a function it does not recognise (silent when unsure).
    private static readonly string[] VolatileFunctions =
    {
        "now(",
        "current_timestamp",
        "clock_timestamp(",
        "gen_random_uuid(",
        "uuid_generate_v4(",
        "random(",
        "nextval(",
    };

    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW016",
        "AddColumn default is computed per row on PostgreSQL",
        "AddColumn '{0}.{1}' default '{2}' is volatile: PostgreSQL rewrites the whole table to fill it in",
        "A constant default is applied as metadata only and is instant, even on a huge table. A volatile " +
        "default has to be computed and written for every existing row, which rewrites the table and holds " +
        "a lock for the duration. Use a constant default here, or backfill the value in a separate step.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.AddColumn)
        {
            return null;
        }

        if (!operation.IsNpgsql)
        {
            return null;
        }

        if (context.IsTableCreatedInThisMigration(operation.QualifiedTable))
        {
            return null;
        }

        if (operation.DefaultValueSql is not { } sql)
        {
            return null;
        }

        var isVolatile = Array.Exists(VolatileFunctions, fn => sql.Contains(fn, StringComparison.OrdinalIgnoreCase));
        return isVolatile
            ? new object[] { operation.QualifiedTable, operation.Column!, sql }
            : null;
    }
}
