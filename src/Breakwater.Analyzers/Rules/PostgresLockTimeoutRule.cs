using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW020: a Postgres DDL migration with no <c>SET lock_timeout</c> waits behind whatever long
/// transaction happens to be running, and every later query then queues behind the DDL - an
/// outage from a change that looked instant in testing. Strict-profile only: this would flag
/// nearly every Postgres migration, so it ships off by default.
/// </summary>
internal sealed class PostgresLockTimeoutRule : IMigrationRule
{
    private static readonly System.Collections.Generic.HashSet<MigrationOperationKind> DdlKinds = new()
    {
        MigrationOperationKind.DropColumn, MigrationOperationKind.DropTable, MigrationOperationKind.RenameColumn,
        MigrationOperationKind.RenameTable, MigrationOperationKind.AlterColumn, MigrationOperationKind.AddColumn,
        MigrationOperationKind.CreateTable, MigrationOperationKind.CreateIndex, MigrationOperationKind.AddForeignKey,
        MigrationOperationKind.AddCheckConstraint, MigrationOperationKind.AddUniqueConstraint,
        MigrationOperationKind.AddPrimaryKey, MigrationOperationKind.DropPrimaryKey,
        MigrationOperationKind.DropUniqueConstraint, MigrationOperationKind.DropCheckConstraint,
        MigrationOperationKind.DropForeignKey,
    };

    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW020",
        "PostgreSQL DDL migration has no SET lock_timeout",
        "{0} on '{1}' runs with no lock_timeout set; it can queue behind (and then block) unrelated queries",
        "Without a lock_timeout, this DDL statement waits indefinitely for its lock, and every later " +
        "query on the same table queues up behind it once it starts waiting. Run " +
        "\"SET lock_timeout = '2s';\" (an example value) via Sql(...) before the DDL, and retry the " +
        "migration if it times out.",
        isEnabledByDefault: false);

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (!operation.IsNpgsql || !DdlKinds.Contains(operation.Kind) || context.HasLockTimeoutStatement)
        {
            return null;
        }

        return new object[] { operation.Kind, operation.QualifiedTable };
    }
}
