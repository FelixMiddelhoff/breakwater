using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>Demonstrates BW003 (RenameColumn).</summary>
[Migration("20260103000000_RenameOps")]
public class RenameOps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW003
        migrationBuilder.RenameColumn(name: "Mail", table: "Users", newName: "EmailAddress");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(name: "EmailAddress", table: "Users", newName: "Mail");
    }
}
