using Breakwater.Analyzers.Operations;
using Breakwater.Analyzers.Sql;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW036: <c>DROP PROCEDURE/FUNCTION/VIEW IF EXISTS &lt;name&gt;</c> immediately (or later, still
/// within the same <c>Sql(...)</c> call) followed by <c>CREATE [OR REPLACE] PROCEDURE/FUNCTION/
/// VIEW</c> of the <b>same name and kind</b> is a recognized, intentional idempotent-redefinition
/// idiom - seen twice in the real corpus (bitwarden-mysql's <c>GrantIdWithIndexes.cs</c>,
/// platformplatform's <c>AddFeatureFlagsSchema.cs</c>) - and is materially lower risk than an
/// unpaired DROP, which is exactly what it is trying to redefine, not remove.
/// </summary>
/// <remarks>
/// <b>Design decision (documented per the task's own instruction to make this call explicitly):
/// this is a separate rule id (BW036), not an in-place severity/message branch inside BW010.</b>
/// Reasons: (1) BW010's own descriptor is shared by every one of its trigger shapes (TRUNCATE,
/// unfiltered UPDATE/DELETE, unpaired DROP) - splitting its message format per case would make an
/// already-general rule harder to reason about and test in isolation; (2) a distinct id gives this
/// idiom its own <see cref="Descriptor"/>, its own severity (Suggestion/Info, since it is a
/// recognized safe idiom rather than a genuine hazard), and its own doc page and tests, matching
/// how every other "more specific rule supersedes a general one" case in this codebase is built
/// (BW023 over BW001, BW026/BW005/BW004 sharing <c>AlterColumn</c>) rather than invisible logic
/// buried inside BW010's <c>Check</c>; (3) it keeps BW010's own contract simple and unchanged
/// ("DROP always flags", exactly as its docs already say) for every other DROP shape (tables,
/// columns via a plain <c>DROP TABLE</c>/<c>ALTER TABLE ... DROP COLUMN</c>, indexes, etc.) -
/// only the specific DROP-IF-EXISTS-then-CREATE-same-object pairing is downgraded.
/// BW010 would otherwise also flag the DROP statement in this pair (it flags any statement
/// containing a <c>DROP</c> keyword). <see cref="RuleCatalog.Suppresses"/> lists BW036 as
/// suppressing BW010 for the same operation so the pair is reported once, at the lower severity,
/// not twice.
/// </remarks>
internal sealed class SqlIdempotentRedefineRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW036",
        "Raw SQL drops and immediately redefines a procedure, function, or view (idempotent redefinition)",
        "Sql(...) statement '{0}': DROP {1} IF EXISTS '{2}' is followed by a CREATE {1} of the same name - a " +
        "recognized idempotent-redefinition idiom, lower risk than an unpaired DROP",
        "A DROP ... IF EXISTS immediately followed by a CREATE of the same object name and kind is redefining " +
        "the object, not removing it - this is a common, intentional idiom for making a migration re-runnable, " +
        "materially lower risk than a DROP with no matching CREATE. Reported at Suggestion severity instead of " +
        "BW010's Warning for a genuinely unpaired DROP.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.Sql || operation.SqlText is null)
        {
            return null;
        }

        var statements = SqlTokenizer.Tokenize(operation.SqlText);
        for (var i = 0; i < statements.Count; i++)
        {
            if (!SqlStatementRisk.TryGetDropIfExists(statements[i], out var dropKind, out var dropName))
            {
                continue;
            }

            // Only a later statement counts: a CREATE that appears before the DROP in source
            // order is not the "redefine" idiom (order matters).
            for (var j = i + 1; j < statements.Count; j++)
            {
                if (SqlStatementRisk.TryGetCreate(statements[j], out var createKind, out var createName)
                    && string.Equals(createKind, dropKind, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(createName, dropName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return new object[] { statements[i].Text, dropKind, dropName };
                }
            }
        }

        return null;
    }
}
