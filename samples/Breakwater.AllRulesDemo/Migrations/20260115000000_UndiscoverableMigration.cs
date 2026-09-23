using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

// breakwater: expect BW028
// Deliberately missing [Migration("...")] and [DbContext(...)]: EF will never discover this
// migration class, so it silently never runs even though it lives right next to the others.
public class UndiscoverableMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Nickname2", table: "Users", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
