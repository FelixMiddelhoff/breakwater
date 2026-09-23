using Breakwater.Analyzers.Operations;
using Breakwater.Analyzers.Sql;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW035: raw SQL that disguises a structural schema change. <c>migrationBuilder.Sql(...)</c> is
/// tokenized and scanned for an <c>ALTER TABLE</c> statement containing a <c>DROP COLUMN</c> (the
/// same data-loss risk BW001 catches for the typed API) or an <c>ADD COLUMN ... NOT NULL</c> with
/// no <c>DEFAULT</c> (the same risk BW006 catches) - both invisible to BW001/BW006 because they
/// only look at typed <c>MigrationBuilder</c> calls, never at DDL text inside a string. Real-world
/// pattern this was built for: bitwarden-mysql's <c>GrantIdWithIndexes.cs</c>, which does
/// <c>ALTER TABLE `Grant` DROP COLUMN `Id`</c> then <c>ALTER TABLE `Grant` ADD COLUMN `Id` INT
/// AUTO_INCREMENT UNIQUE</c> inside a raw SQL stored procedure body (see
/// <c>breakwater-memory.md</c>'s "BW035+ candidates" note).
/// </summary>
/// <remarks>
/// Overlap with other raw-SQL rules on the same statement, checked deliberately (per the task's
/// own instruction, not assumed):
/// - <b>BW010</b> flags any statement containing a <c>DROP</c> keyword anywhere, including
///   <c>ALTER TABLE ... DROP COLUMN ...</c> - unlike BW019/BW031/BW033/BW034 (which look for
///   genuinely different keywords/shapes and so never collide), this is the *same* aspect of the
///   *same* statement reported with a vaguer message ("DROP removes the object outright"), which
///   would be confusing double coverage rather than two different problems. BW035 is strictly
///   more specific for this shape, so <see cref="RuleCatalog.Suppresses"/> lists BW035 as
///   suppressing BW010 for the same operation (most-specific-wins, the same pattern already used
///   for BW023 over BW001). The ADD COLUMN NOT NULL branch never triggers BW010 (no DROP/TRUNCATE
///   keyword, and it is not a leading UPDATE/DELETE), so no suppression is needed there.
/// - BW019 (Postgres raw-SQL) looks for <c>CREATE INDEX</c>/<c>LOCK TABLE</c>/<c>VACUUM FULL</c>/
///   <c>ALTER TABLE ... SET DATA TYPE</c> - none of those shapes match an <c>ALTER TABLE ... DROP/
///   ADD COLUMN</c> statement, so it structurally never co-fires here.
/// - BW031 (GO/USE batch separators), BW033 (secrets), BW034 (disables safety) scan for unrelated
///   substrings/keywords not present in a plain DROP/ADD COLUMN statement, so they do not collide
///   either; if one of them *did* also fire on the same raw SQL string, that would be a genuinely
///   different problem (a batch separator, a leaked secret) and co-firing would be correct, same
///   precedent as BW010/BW034 both firing on a <c>DROP DATABASE</c> statement (see the M8 note in
///   <c>breakwater-memory.md</c>).
/// </remarks>
internal sealed class SqlStructuralChangeRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW035",
        "Raw SQL disguises a structural schema change (DROP COLUMN or ADD COLUMN NOT NULL)",
        "Sql(...) statement '{0}': {1}",
        "ALTER TABLE ... DROP COLUMN and ALTER TABLE ... ADD COLUMN ... NOT NULL (with no DEFAULT) inside " +
        "a raw SQL string carry exactly the same risk as the typed DropColumn/AddColumn calls BW001/BW006 " +
        "catch, but bypass them entirely because the change is hidden in SQL text. Use the typed API " +
        "(so BW001/BW006 can see it and suggest the safe expand/contract sequence), or apply the same " +
        "safe alternative by hand: for a drop, stop using the column first and drop it in a later " +
        "migration; for a not-null add, add it nullable, backfill, then constrain.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.Sql || operation.SqlText is null)
        {
            return null;
        }

        foreach (var statement in SqlTokenizer.Tokenize(operation.SqlText))
        {
            var reason = SqlStatementRisk.EvaluateStructuralChange(statement);
            if (reason is not null)
            {
                return new object[] { statement.Text, reason };
            }
        }

        return null;
    }
}
