namespace Breakwater.Analyzers.Operations;

/// <summary>The kinds of migration operation that Breakwater understands.</summary>
internal enum MigrationOperationKind
{
    DropColumn,
    DropTable,
    RenameColumn,
    RenameTable,
}
