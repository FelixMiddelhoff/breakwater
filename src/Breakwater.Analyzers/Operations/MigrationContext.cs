using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Operations;

/// <summary>
/// Facts about the migration an operation lives in, gathered once per <c>Up</c> method and
/// shared by every rule that looks at that method's operations.
/// </summary>
internal sealed class MigrationContext
{
    private readonly HashSet<string> _tablesCreatedInThisMigration;
    private readonly HashSet<string> _tablesWithDropColumn;
    private readonly HashSet<string> _tablesWithAddColumn;
    private readonly HashSet<string> _placeholderDefaultColumns;
    private readonly HashSet<string> _nullableAddedColumns;
    private readonly Dictionary<string, int> _riskyOperationCountByTable;
    private readonly Dictionary<string, Location> _lastRiskyLocationByTable;

    public MigrationContext(
        HashSet<string> tablesCreatedInThisMigration,
        bool suppressTransaction = false,
        HashSet<string>? tablesWithDropColumn = null,
        HashSet<string>? tablesWithAddColumn = null,
        HashSet<string>? placeholderDefaultColumns = null,
        bool hasSchemaOperation = false,
        int operationCount = 0,
        Location? firstOperationLocation = null,
        Dictionary<string, int>? riskyOperationCountByTable = null,
        Dictionary<string, Location>? lastRiskyLocationByTable = null,
        HashSet<string>? nullableAddedColumns = null,
        bool hasLockTimeoutStatement = false)
    {
        _tablesCreatedInThisMigration = tablesCreatedInThisMigration;
        SuppressTransaction = suppressTransaction;
        _tablesWithDropColumn = tablesWithDropColumn ?? new HashSet<string>();
        _tablesWithAddColumn = tablesWithAddColumn ?? new HashSet<string>();
        _placeholderDefaultColumns = placeholderDefaultColumns ?? new HashSet<string>();
        HasSchemaOperation = hasSchemaOperation;
        OperationCount = operationCount;
        FirstOperationLocation = firstOperationLocation;
        _riskyOperationCountByTable = riskyOperationCountByTable ?? new Dictionary<string, int>();
        _lastRiskyLocationByTable = lastRiskyLocationByTable ?? new Dictionary<string, Location>();
        _nullableAddedColumns = nullableAddedColumns ?? new HashSet<string>();
        HasLockTimeoutStatement = hasLockTimeoutStatement;
    }

    /// <summary>True when a Sql(...) call in this migration mentions "lock_timeout" (BW020).</summary>
    public bool HasLockTimeoutStatement { get; }

    /// <summary>
    /// True when <paramref name="qualifiedTable"/>.<paramref name="column"/> was added earlier in
    /// this migration as nullable: BW027 stays silent for a unique index on it, since a freshly
    /// added nullable column has no existing data to conflict on.
    /// </summary>
    public bool IsNullableAddedColumn(string qualifiedTable, string column) =>
        _nullableAddedColumns.Contains(qualifiedTable + "." + column);

    /// <summary>
    /// True when <paramref name="qualifiedTable"/> was created by a <c>CreateTable</c> call
    /// earlier in the same <c>Up</c> method, so the table is still empty: table-lock and
    /// not-null rules stay silent for it.
    /// </summary>
    public bool IsTableCreatedInThisMigration(string qualifiedTable) => _tablesCreatedInThisMigration.Contains(qualifiedTable);

    /// <summary>
    /// True when <c>migrationBuilder.SuppressTransaction = true;</c> is set anywhere in the
    /// method, the flag EF requires alongside a Postgres <c>CreatedConcurrently</c> index build
    /// (concurrent index creation cannot run inside the migration's transaction).
    /// </summary>
    public bool SuppressTransaction { get; }

    /// <summary>True when the method also has a schema-changing operation (BW013).</summary>
    public bool HasSchemaOperation { get; }

    /// <summary>The total number of Breakwater-recognized operations in the method (BW029).</summary>
    public int OperationCount { get; }

    /// <summary>The location of the first recognized operation in the method (BW029's single-fire anchor).</summary>
    public Location? FirstOperationLocation { get; }

    /// <summary>
    /// True when <paramref name="qualifiedTable"/> has both a <c>DropColumn</c> and an
    /// <c>AddColumn</c> call in this migration - the shape BW023 treats as a likely undetected
    /// rename.
    /// </summary>
    public bool HasDropAndAddColumn(string qualifiedTable) =>
        _tablesWithDropColumn.Contains(qualifiedTable) && _tablesWithAddColumn.Contains(qualifiedTable);

    /// <summary>
    /// True when <paramref name="qualifiedTable"/>.<paramref name="column"/> was added earlier in
    /// this migration with a constant placeholder default (BW025).
    /// </summary>
    public bool IsPlaceholderDefaultColumn(string qualifiedTable, string column) =>
        _placeholderDefaultColumns.Contains(qualifiedTable + "." + column);

    /// <summary>How many "risky" operations (everything but CreateTable/Sql/*Data) touch this table (BW022).</summary>
    public int RiskyOperationCount(string qualifiedTable) =>
        _riskyOperationCountByTable.TryGetValue(qualifiedTable, out var count) ? count : 0;

    /// <summary>The location of the last risky operation seen for this table (BW022's single-fire anchor).</summary>
    public bool IsLastRiskyOperationForTable(string qualifiedTable, Location location) =>
        _lastRiskyLocationByTable.TryGetValue(qualifiedTable, out var last) && last.Equals(location);
}
