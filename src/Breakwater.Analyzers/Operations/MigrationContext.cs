using System.Collections.Generic;

namespace Breakwater.Analyzers.Operations;

/// <summary>
/// Facts about the migration an operation lives in, gathered once per <c>Up</c> method and
/// shared by every rule that looks at that method's operations.
/// </summary>
internal sealed class MigrationContext
{
    private readonly HashSet<string> _tablesCreatedInThisMigration;

    public MigrationContext(HashSet<string> tablesCreatedInThisMigration, bool suppressTransaction = false)
    {
        _tablesCreatedInThisMigration = tablesCreatedInThisMigration;
        SuppressTransaction = suppressTransaction;
    }

    /// <summary>
    /// True when <paramref name="qualifiedTable"/> was created by a <c>CreateTable</c> call
    /// earlier in the same <c>Up</c> method, so the table is still empty: table-lock and
    /// not-null rules stay silent for it.
    /// </summary>
    public bool IsTableCreatedInThisMigration(string qualifiedTable) => _tablesCreatedInThisMigration.Contains(qualifiedTable);

    /// <summary>
    /// True when the <c>Up</c> method sets <c>migrationBuilder.SuppressTransaction = true;</c>
    /// anywhere, the flag EF requires alongside a Postgres <c>CreatedConcurrently</c> index build
    /// (concurrent index creation cannot run inside the migration's transaction).
    /// </summary>
    public bool SuppressTransaction { get; }
}
