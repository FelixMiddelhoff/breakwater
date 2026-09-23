using System.Collections.Generic;

namespace Breakwater.Analyzers.Operations;

/// <summary>
/// Facts about the migration an operation lives in, gathered once per <c>Up</c> method and
/// shared by every rule that looks at that method's operations.
/// </summary>
internal sealed class MigrationContext
{
    private readonly HashSet<string> _tablesCreatedInThisMigration;

    public MigrationContext(HashSet<string> tablesCreatedInThisMigration)
    {
        _tablesCreatedInThisMigration = tablesCreatedInThisMigration;
    }

    /// <summary>
    /// True when <paramref name="qualifiedTable"/> was created by a <c>CreateTable</c> call
    /// earlier in the same <c>Up</c> method, so the table is still empty: table-lock and
    /// not-null rules stay silent for it.
    /// </summary>
    public bool IsTableCreatedInThisMigration(string qualifiedTable) => _tablesCreatedInThisMigration.Contains(qualifiedTable);
}
