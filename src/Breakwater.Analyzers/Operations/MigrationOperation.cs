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

    // AddColumn / AlterColumn details. Null/unset means "not known" (not a compile-time
    // constant, or the parameter was not given), and rules must treat that as "cannot tell"
    // rather than guessing, per the noise policy.

    /// <summary>The CLR type argument of <c>AddColumn&lt;T&gt;</c> / <c>AlterColumn&lt;T&gt;</c>.</summary>
    public string? ClrType { get; init; }

    /// <summary>AlterColumn's <c>oldClrType</c>, as EF's <c>typeof(T)</c> argument, display-formatted.</summary>
    public string? OldClrType { get; init; }

    public bool Nullable { get; init; }

    /// <summary>AlterColumn's <c>oldNullable</c>.</summary>
    public bool OldNullable { get; init; }

    public int? MaxLength { get; init; }

    public int? OldMaxLength { get; init; }

    public int? Precision { get; init; }

    public int? OldPrecision { get; init; }

    public int? Scale { get; init; }

    public int? OldScale { get; init; }

    public string? Collation { get; init; }

    public string? OldCollation { get; init; }

    /// <summary>True when a non-null constant (or any expression) was passed for <c>defaultValue</c>.</summary>
    public bool HasDefaultValue { get; init; }

    /// <summary>True when a non-null constant (or any expression) was passed for <c>defaultValueSql</c>.</summary>
    public bool HasDefaultValueSql { get; init; }

    /// <summary>The constant text of <c>defaultValueSql</c>, or null when absent or not a constant.</summary>
    public string? DefaultValueSql { get; init; }

    /// <summary>
    /// True when the call is guarded by a <c>migrationBuilder.IsNpgsql()</c> check, the one
    /// signal Breakwater currently uses to know a branch targets PostgreSQL. Not set means
    /// "provider unknown", and provider-specific rules stay silent for it.
    /// </summary>
    public bool IsNpgsql { get; init; }

    // CreateIndex / constraint details.

    /// <summary>CreateIndex's <c>unique</c> argument.</summary>
    public bool Unique { get; init; }

    /// <summary>
    /// True when the <c>CreateIndex</c> call is chained with
    /// <c>.Annotation("Npgsql:CreatedConcurrently", true)</c>.
    /// </summary>
    public bool IsCreatedConcurrently { get; init; }

    /// <summary>
    /// True when the <c>CreateIndex</c> call is chained with <c>.Annotation("SqlServer:Online", true)</c>.
    /// </summary>
    public bool IsOnline { get; init; }

    // Sql details.

    /// <summary>
    /// The <c>sql</c> argument's compile-time constant text, or null when it is not a
    /// compile-time constant (a variable, <c>File.ReadAllText(...)</c>, a resource, string
    /// concatenation with a non-constant operand, ...). BW010 stays silent when this is null:
    /// the content is not statically knowable, so it does not guess.
    /// </summary>
    public string? SqlText { get; init; }
}
