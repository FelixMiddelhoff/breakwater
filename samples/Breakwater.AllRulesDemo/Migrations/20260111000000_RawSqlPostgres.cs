using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW019 (raw SQL CREATE INDEX without CONCURRENTLY on Postgres, hidden inside
/// migrationBuilder.Sql) and BW020 (Postgres DDL migration without SET lock_timeout - strict
/// profile only, enabled via this project's .editorconfig).
/// </summary>
[Migration("20260111000000_RawSqlPostgres")]
public class RawSqlPostgres : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.IsNpgsql())
        {
            // breakwater: expect BW019
            migrationBuilder.Sql("CREATE INDEX IX_Products_Name ON \"Products\" (\"Name\")");

            // breakwater: expect BW020
            migrationBuilder.AddColumn<string>(name: "ExternalRef", table: "Products", nullable: true);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
