using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.Samples.Migrations;

/// <summary>Creates the tables the later migrations in this sample operate on.</summary>
[Migration("20260101000000_InitialCreate")]
public class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<int>(),
                Email = table.Column<string>(nullable: false),
                LegacyNotes = table.Column<string>(nullable: true),
            });

        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<int>(),
                CustomerId = table.Column<int>(),
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Orders");
        migrationBuilder.DropTable(name: "Customers");
    }
}
