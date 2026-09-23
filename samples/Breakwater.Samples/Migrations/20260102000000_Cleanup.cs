using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.Samples.Migrations;

/// <summary>
/// Shows two Warning-tier findings: BW001 (dropping a column loses data during a rolling
/// deploy) and BW006 (a NOT NULL column with no default fails on a non-empty table).
/// </summary>
[Migration("20260102000000_Cleanup")]
public class Cleanup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LegacyNotes", table: "Customers");

        migrationBuilder.AddColumn<string>(
            name: "Sku",
            table: "Orders",
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "LegacyNotes", table: "Customers", nullable: true);
        migrationBuilder.DropColumn(name: "Sku", table: "Orders");
    }
}
