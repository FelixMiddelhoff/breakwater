using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>Demonstrates BW001 (DropColumn) and BW002 (DropTable).</summary>
[Migration("20260102000000_DestructiveOps")]
public class DestructiveOps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW001
        migrationBuilder.DropColumn(name: "FullName", table: "Users");

        // breakwater: expect BW002
        migrationBuilder.DropTable(name: "Countries");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
