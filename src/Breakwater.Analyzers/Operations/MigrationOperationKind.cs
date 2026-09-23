namespace Breakwater.Analyzers.Operations;

/// <summary>The kinds of migration operation that Breakwater understands.</summary>
internal enum MigrationOperationKind
{
    DropColumn,
    DropTable,
    RenameColumn,
    RenameTable,
    AlterColumn,
    AddColumn,
    CreateTable,
    CreateIndex,
    AddForeignKey,
    AddCheckConstraint,
    AddUniqueConstraint,
    AddPrimaryKey,
    DropPrimaryKey,
    DropUniqueConstraint,
    DropCheckConstraint,
    DropForeignKey,
    Sql,
    InsertData,
    UpdateData,
    DeleteData,
    DropSchema,
    DropSequence,
    AlterSequence,
    AlterDatabase,
}
