using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>Demonstrates BW010 (raw SQL UPDATE without WHERE).</summary>
[Migration("20260106000000_RawSqlBackfill")]
public class RawSqlBackfill : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW010
        migrationBuilder.Sql("UPDATE \"Orders\" SET \"Status\" = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
