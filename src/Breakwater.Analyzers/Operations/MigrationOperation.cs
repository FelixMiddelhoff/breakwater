using System.Collections.Immutable;
using Breakwater.Analyzers.Configuration;
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
    /// True when the call is guarded by a <c>migrationBuilder.IsNpgsql()</c> check (in <c>auto</c>
    /// provider mode), or when <c>breakwater_provider = postgres</c> is configured (which trusts
    /// the override outright, without requiring the guard). Not set means "provider unknown", and
    /// provider-specific rules stay silent for it.
    /// </summary>
    public bool IsNpgsql { get; internal set; }

    /// <summary>
    /// The resolved provider for this operation: the configured <c>breakwater_provider</c>
    /// override when one is set, otherwise the best guess from the guard/annotation heuristics
    /// (<see cref="BreakwaterDatabaseProvider.Auto"/> when nothing was detected).
    /// </summary>
    public BreakwaterDatabaseProvider DetectedProvider { get; internal set; } = BreakwaterDatabaseProvider.Auto;

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

    /// <summary>True when <c>Sql(..., suppressTransaction: true)</c> was passed.</summary>
    public bool SqlSuppressesTransaction { get; init; }

    // InsertData / UpdateData / DeleteData details.

    /// <summary>
    /// The number of rows the call statically provides, when it can be counted from an array
    /// initializer; null when the row count is not statically knowable (a variable, a method
    /// call, ...), per "silent when unsure".
    /// </summary>
    public int? RowCount { get; init; }

    // AddForeignKey details.

    /// <summary>
    /// The first entry of the <c>columns</c> argument, when it is a constant array. Used by
    /// <c>AddForeignKey</c> (the referencing column, for BW025) and reused as-is by
    /// <c>CreateIndex</c> (the first indexed column, for BW027).
    /// </summary>
    public string? ForeignKeyColumn { get; init; }

    /// <summary>True when <c>onDelete</c> was passed <c>ReferentialAction.Cascade</c> (or the equivalent int).</summary>
    public bool OnDeleteCascade { get; init; }

    /// <summary>
    /// True when <see cref="ForeignKeyColumn"/> was added earlier in the same migration with a
    /// constant placeholder default (<c>0</c>, <c>""</c>, <c>Guid.Empty</c>). Computed by
    /// <see cref="Operations.MigrationContext"/>, not the reader, since it depends on another
    /// operation in the same method.
    /// </summary>
    public bool ForeignKeyColumnHasPlaceholderDefault { get; init; }

    // AddColumn placeholder-default detail (for BW025).

    /// <summary>
    /// True when <c>defaultValue</c> is a recognizable constant placeholder: <c>0</c> (or another
    /// numeric zero), an empty string, or <c>Guid.Empty</c>.
    /// </summary>
    public bool HasPlaceholderDefaultValue { get; init; }

    // AlterColumn / CreateIndex chained annotation details (for BW026).

    /// <summary>Names of every <c>.Annotation(name, value)</c> chained onto this call.</summary>
    public ImmutableHashSet<string> AnnotationNames { get; init; } = ImmutableHashSet<string>.Empty;

    /// <summary>Names of every <c>.OldAnnotation(name, value)</c> chained onto this call.</summary>
    public ImmutableHashSet<string> OldAnnotationNames { get; init; } = ImmutableHashSet<string>.Empty;

    /// <summary>
    /// True when the call sits inside an <c>if (migrationBuilder.IsMySql())</c> guard (in
    /// <c>auto</c> mode), or when <c>breakwater_provider = mysql</c> is configured.
    /// </summary>
    public bool IsMySql { get; internal set; }
}
