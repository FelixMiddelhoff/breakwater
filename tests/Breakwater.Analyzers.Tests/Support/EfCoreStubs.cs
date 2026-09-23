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

                public virtual void AlterColumn<T>(
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
                    string? oldCollation = null) { }
            }

            public static class NpgsqlMigrationBuilderExtensions
            {
                public static bool IsNpgsql(this MigrationBuilder builder) => true;
            }
        }
        """;
}
