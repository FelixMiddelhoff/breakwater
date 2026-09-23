using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW029 (MySQL DDL auto-commits, so several operations in one migration cannot
/// roll back together as a unit) and BW030 (cascade delete foreign key - strict profile only,
/// enabled via this project's .editorconfig).
/// </summary>
[Migration("20260116000000_MySqlAutoCommitAndCascade")]
public class MySqlAutoCommitAndCascade : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.IsMySql())
        {
            // breakwater: expect BW029
            migrationBuilder.AddColumn<string>(name: "Nickname3", table: "Users", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Bio", table: "Users", nullable: true);
        }

        // breakwater: expect BW030
        migrationBuilder.AddForeignKey(
            name: "FK_Products_Customers_Backup",
            table: "Products",
            columns: new[] { "SupplierId" },
            principalTable: "Customers",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
