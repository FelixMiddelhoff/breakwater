using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Operations;

/// <summary>
/// One call on a <c>MigrationBuilder</c>, reduced to plain data so that rules
/// never have to deal with Roslyn syntax or symbols.
/// </summary>
internal sealed class MigrationOperation
{
    /// <summary>Shown in place of a name that is not a compile-time constant.</summary>
    public const string UnknownName = "?";

    public MigrationOperation(
        MigrationOperationKind kind,
        string? schema,
        string table,
        string? column,
        string? newName,
        Location location)
    {
        Kind = kind;
        Schema = schema;
        Table = table;
        Column = column;
        NewName = newName;
        Location = location;
    }

    public MigrationOperationKind Kind { get; }

    /// <summary>The schema of the table, or null when none was given.</summary>
    public string? Schema { get; }

    /// <summary>The table the operation works on (or the table itself for table operations).</summary>
    public string Table { get; }

    /// <summary>The column the operation works on, or null for table operations.</summary>
    public string? Column { get; }

    /// <summary>The new name for rename operations, or null otherwise.</summary>
    public string? NewName { get; }

    public Location Location { get; }

    /// <summary>The table name including its schema, as a developer would write it.</summary>
    public string QualifiedTable => Schema is null ? Table : Schema + "." + Table;
}
