using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW021 (AlterDatabase) and BW022 (several risky operations on the same table,
/// advisory - only reported because AlterColumn here is not already caught by a more specific
/// rule; note the AddColumn/DropColumn pair on Orders below is followed by an unrelated
/// AlterColumn on Priority which triggers BW022's "compounding risk" advisory).
/// </summary>
[Migration("20260112000000_DatabaseAndCompound")]
public class DatabaseAndCompound : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW021
        migrationBuilder.AlterDatabase();

        migrationBuilder.AddColumn<string>(name: "Notes", table: "Orders", nullable: true);
        migrationBuilder.DropColumn(name: "LegacyFlag", table: "Orders");
        // breakwater: expect BW022
        migrationBuilder.AlterColumn<int>(name: "Priority", table: "Orders", nullable: true, oldNullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
