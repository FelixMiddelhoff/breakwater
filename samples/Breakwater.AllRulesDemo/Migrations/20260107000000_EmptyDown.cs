using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW011 (empty Down - irreversible migration). Off by default; this project's
/// .editorconfig turns the strict profile on so it fires here.
/// </summary>
[Migration("20260107000000_EmptyDown")]
public class EmptyDown : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Nickname", table: "Users", nullable: true);
    }

    // breakwater: expect BW011
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
