namespace Breakwater.Analyzers.Tests.Support;

/// <summary>
/// Minimal look-alikes of the EF Core migration types, compiled together with every
/// test snippet. Only the parts of the real API the analyzer looks at are copied, with
/// the same names, parameter names and defaults, so tests need no EF Core package.
/// </summary>
internal static class EfCoreStubs
{
    public const string Source = """
        namespace Microsoft.EntityFrameworkCore.Migrations
        {
            public abstract class Migration
            {
                protected abstract void Up(MigrationBuilder migrationBuilder);
                protected virtual void Down(MigrationBuilder migrationBuilder) { }
            }

            public class MigrationBuilder
            {
                public virtual void DropColumn(string name, string table, string? schema = null) { }
                public virtual void DropTable(string name, string? schema = null) { }
                public virtual void RenameColumn(string name, string table, string newName, string? schema = null) { }
                public virtual void RenameTable(string name, string? schema = null, string? newName = null, string? newSchema = null) { }
                public virtual void CreateTable(string name, string? schema = null) { }

                public virtual void AddColumn<T>(
                    string name,
                    string table,
                    string? type = null,
                    int? maxLength = null,
                    string? schema = null,
                    bool nullable = false,
                    object? defaultValue = null,
                    string? defaultValueSql = null,
                    int? precision = null,
                    int? scale = null,
                    string? collation = null) { }

                public virtual OperationBuilder AlterColumn<T>(
                    string name,
                    string table,
                    string? type = null,
                    int? maxLength = null,
                    string? schema = null,
                    bool nullable = false,
                    object? defaultValue = null,
                    string? defaultValueSql = null,
                    int? precision = null,
                    int? scale = null,
                    string? collation = null,
                    System.Type? oldClrType = null,
                    int? oldMaxLength = null,
                    bool oldNullable = false,
                    int? oldPrecision = null,
                    int? oldScale = null,
                    string? oldCollation = null) => new OperationBuilder();

                public bool SuppressTransaction { get; set; }

                public virtual OperationBuilder CreateIndex(
                    string name,
                    string table,
                    string[] columns,
                    string? schema = null,
                    bool unique = false,
                    string? filter = null) => new OperationBuilder();

                public virtual void AddForeignKey(
                    string name,
                    string table,
                    string[] columns,
                    string principalTable,
                    string[]? principalColumns = null,
                    string? schema = null,
                    string? principalSchema = null,
                    int? onUpdate = null,
                    int? onDelete = null) { }

                public virtual void AddCheckConstraint(
                    string name,
                    string table,
                    string sql,
                    string? schema = null) { }

                public virtual void AddUniqueConstraint(
                    string name,
                    string table,
                    string[] columns,
                    string? schema = null) { }

                public virtual void AddPrimaryKey(
                    string name,
                    string table,
                    string[] columns,
                    string? schema = null) { }

                public virtual void DropPrimaryKey(string name, string table, string? schema = null) { }
                public virtual void DropUniqueConstraint(string name, string table, string? schema = null) { }
                public virtual void DropCheckConstraint(string name, string table, string? schema = null) { }
                public virtual void DropForeignKey(string name, string table, string? schema = null) { }

                public virtual void Sql(string sql, bool suppressTransaction = false) { }

                public virtual void InsertData(string table, string[] columns, object[,] values, string? schema = null) { }
                public virtual void InsertData(string table, string column, object[] values, string? schema = null) { }

                public virtual void UpdateData(string table, string keyColumn, object[] keyValues, string[] columns, object[,] values, string? schema = null) { }
                public virtual void UpdateData(string table, string keyColumn, object[] keyValues, string column, object[] values, string? schema = null) { }

                public virtual void DeleteData(string table, string keyColumn, object[] keyValues, string? schema = null) { }
                public virtual void DeleteData(string table, string[] keyColumns, object[,] keyValues, string? schema = null) { }

                public virtual void DropSchema(string name) { }
                public virtual void DropSequence(string name, string? schema = null) { }
                public virtual void AlterSequence(string name, string? schema = null, int? incrementBy = null) { }
                public virtual void RestartSequence(string name, string? schema = null, long? startValue = null) { }
                public virtual void AlterDatabase() { }
            }

            public class OperationBuilder
            {
                public virtual OperationBuilder Annotation(string name, object? value) => this;
                public virtual OperationBuilder OldAnnotation(string name, object? value) => this;
            }

            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class MigrationAttribute : System.Attribute
            {
                public MigrationAttribute(string id) { Id = id; }
                public string Id { get; }
            }

            public static class NpgsqlMigrationBuilderExtensions
            {
                public static bool IsNpgsql(this MigrationBuilder builder) => true;
            }

            public static class MySqlMigrationBuilderExtensions
            {
                public static bool IsMySql(this MigrationBuilder builder) => true;
            }
        }

        namespace Microsoft.EntityFrameworkCore.Infrastructure
        {
            public class DatabaseFacade
            {
                public virtual bool EnsureCreated() => true;
            }
        }
        """;
}
