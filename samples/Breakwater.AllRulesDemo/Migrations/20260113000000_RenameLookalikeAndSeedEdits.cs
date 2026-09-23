using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW023 (drop+add on the same table looks like an undetected rename), BW024
/// (DeleteData removes rows in production) and BW025 (AddForeignKey on a column just added
/// with a placeholder default of 0, so existing rows point at a parent that does not exist).
/// </summary>
[Migration("20260113000000_RenameLookalikeAndSeedEdits")]
public class RenameLookalikeAndSeedEdits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW023
        migrationBuilder.DropColumn(name: "Name", table: "Roles");
        migrationBuilder.AddColumn<string>(name: "DisplayName", table: "Roles", nullable: true);

        // breakwater: expect BW024
        migrationBuilder.DeleteData(table: "Roles", keyColumn: "Id", keyValues: new object[] { 3 });

        migrationBuilder.AddColumn<int>(name: "SupplierId", table: "Products", nullable: false, defaultValue: 0);

        // breakwater: expect BW025
        migrationBuilder.AddForeignKey(
            name: "FK_Products_Suppliers",
            table: "Products",
            columns: new[] { "SupplierId" },
            principalTable: "Customers");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
