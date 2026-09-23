using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates the "// breakwater: allow BWxxx &lt;reason&gt;" suppression convention: this
/// DropColumn would otherwise raise BW001, but the comment above it with a non-empty reason
/// suppresses it. No diagnostic is expected from this migration.
/// </summary>
[Migration("20260120000000_SuppressionDemo")]
public class SuppressionDemo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: allow BW001 column was never populated, verified with the author
        migrationBuilder.DropColumn(name: "SupplierId", table: "Products");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
