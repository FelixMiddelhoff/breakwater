using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW004 (narrowing AlterColumn), BW005 (nullable to not-null without
/// backfill/default) and BW006 (not-null AddColumn without default).
/// </summary>
[Migration("20260104000000_AlterColumnRisks")]
public class AlterColumnRisks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW004
        migrationBuilder.AlterColumn<string>(
            name: "Description",
            table: "Products",
            maxLength: 50,
            oldMaxLength: 500);

        // breakwater: expect BW005
        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Customers",
            nullable: false,
            oldNullable: true);

        // breakwater: expect BW006
        migrationBuilder.AddColumn<string>(
            name: "Sku",
            table: "Products",
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
