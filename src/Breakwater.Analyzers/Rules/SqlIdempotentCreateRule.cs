using Breakwater.Analyzers.Operations;
using Breakwater.Analyzers.Sql;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW037: raw SQL <c>CREATE TABLE &lt;name&gt; (...)</c> or <c>CREATE [UNIQUE] INDEX &lt;name&gt;
/// ON &lt;table&gt; (...)</c> inside <c>migrationBuilder.Sql(...)</c> with no <c>IF NOT EXISTS</c>
/// guard - the structural-safety counterpart to BW036, but for a bare <c>CREATE</c> with nothing to
/// pair against. If the migration ever runs twice (a partial-deploy retry, or two environments
/// applying the same raw-SQL migration), the statement throws because the table/index already
/// exists - the same "non-idempotent raw DDL" risk class BW036 already addresses for a
/// <c>DROP ... IF EXISTS</c>-then-<c>CREATE</c> pair, just for the simpler bare-<c>CREATE</c> shape.
/// </summary>
/// <remarks>
/// <b>Scope: only <c>TABLE</c> and <c>INDEX</c></b>, the structural/schema-object set matching this
/// rule's own purpose - deliberately not scope-creeping into <c>PROCEDURE</c>/<c>FUNCTION</c>/
/// <c>VIEW</c>, which is BW036's redefinable-object set and already has its own idiom (DROP IF
/// EXISTS + CREATE) to recognize there.
///
/// <b>Overlap, checked deliberately (not assumed):</b>
/// - BW010 flags TRUNCATE/DROP/unfiltered UPDATE-DELETE - none of those keywords appear in a plain
///   <c>CREATE TABLE</c>/<c>CREATE INDEX</c> statement, so it never co-fires here.
/// - BW035 looks for <c>ALTER TABLE ... DROP/ADD COLUMN</c> - a different statement shape
///   (<c>ALTER</c>, not <c>CREATE</c>), so it never co-fires here either.
/// - <b>BW019 (Postgres raw SQL) genuinely can co-fire</b> on the same <c>CREATE INDEX</c>
///   statement: BW019 flags a missing <c>CONCURRENTLY</c>, BW037 flags a missing
///   <c>IF NOT EXISTS</c> - two different hazards of the same statement (one is a locking problem,
///   the other is a re-run-safety problem), so both firing is correct, not noise, the same
///   precedent as BW010 and BW034 both firing on a single <c>DROP DATABASE</c> statement. No
///   suppression is registered between BW019 and BW037 for this reason.
///
/// <b>Severity: Suggestion, matching BW036's own tier</b>, not Warning. A bare <c>CREATE TABLE</c>/
/// <c>CREATE INDEX</c> is overwhelmingly the common, correct shape for a migration that is only
/// ever applied once (EF Core's migration history table already prevents that in the normal case) -
/// the guard only matters for the retry/multi-environment scenario this rule calls out. Flagging
/// every bare CREATE at Warning would reproduce the same build-noise problem the BW011/BW020/BW030
/// strict-only tier exists to avoid (see <c>breakwater-quality-policy.md</c>), so this ships at the
/// same "worth knowing, not a guaranteed hazard" Suggestion tier BW036 already uses.
///
/// <b>Provider-agnostic, not provider-gated</b>: unlike BW019 (which only fires when
/// <see cref="MigrationOperation.IsNpgsql"/>), this rule checks for the <c>IF NOT EXISTS</c> keyword
/// regardless of provider. <c>IF NOT EXISTS</c> is supported by MySQL/Postgres/SQLite for both
/// <c>CREATE TABLE</c> and <c>CREATE INDEX</c>, but SQL Server has no equivalent inline syntax (it
/// needs an <c>IF NOT EXISTS (...) CREATE ...</c> wrapper statement instead, a completely different
/// shape this tokenizer-based rule does not attempt to recognize). Provider-gating (like BW019) was
/// considered and rejected: BW019 gates because its *safe alternative* (<c>CONCURRENTLY</c>) is
/// Postgres-only syntax that would be a false suggestion on another provider, but here the *hazard*
/// (throwing on a re-run) is universal across every provider, including SQL Server - gating the rule
/// off for SQL Server would silently under-report a real risk there, not avoid a false one. The SQL
/// Server wrapper-shape caveat is documented in the rule's doc page instead of gated in code, so SQL
/// Server migrations using the wrapper form correctly stay silent (no <c>IF NOT EXISTS</c> keyword
/// immediately after the kind, so <see cref="SqlStatementRisk.TryGetCreateWithoutIfNotExists"/>
/// still reports a finding on the wrapper's inner bare <c>CREATE</c> - called out as a known
/// limitation in docs rather than silently claimed as covered).
/// </remarks>
internal sealed class SqlIdempotentCreateRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW037",
        "Raw SQL creates a table or index with no IF NOT EXISTS guard (non-idempotent)",
        "Sql(...) statement '{0}': CREATE {1} '{2}' has no IF NOT EXISTS guard - re-running this migration " +
        "(a partial-deploy retry, or applying the same raw-SQL migration in two environments) throws because " +
        "the {1} already exists",
        "A bare CREATE TABLE/CREATE INDEX inside raw SQL has no typed-API equivalent for Breakwater to see, " +
        "and unlike the typed MigrationBuilder.CreateTable/CreateIndex calls (which EF Core's migration " +
        "history table already protects from a double-apply), nothing stops this raw SQL from running twice. " +
        "Add an IF NOT EXISTS guard (MySQL/PostgreSQL/SQLite) or the provider's equivalent conditional-create " +
        "wrapper (SQL Server) so the migration is safe to re-run.",
        severity: RuleDescriptors.Suggestion);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.Sql || operation.SqlText is null)
        {
            return null;
        }

        foreach (var statement in SqlTokenizer.Tokenize(operation.SqlText))
        {
            if (SqlStatementRisk.TryGetCreateWithoutIfNotExists(statement, out var kind, out var name))
            {
                return new object[] { statement.Text, kind, name };
            }
        }

        return null;
    }
}
