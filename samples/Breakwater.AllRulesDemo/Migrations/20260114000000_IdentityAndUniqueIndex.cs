using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW026 (AlterColumn adding identity/value generation) and BW027 (unique
/// CreateIndex on an existing, non-empty table).
/// </summary>
[Migration("20260114000000_IdentityAndUniqueIndex")]
public class IdentityAndUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW026
        migrationBuilder.AlterColumn<int>(name: "Id", table: "Orders")
            .Annotation("SqlServer:Identity", "1, 1");

        // breakwater: expect BW027
        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            columns: new[] { "Email" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
