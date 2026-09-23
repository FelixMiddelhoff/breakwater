using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Creates the tables and sequence every later migration in this demo operates on. Because
/// these tables are created here, Breakwater treats them as pre-existing (non-empty) in every
/// later migration - the "new tables are empty" silence only applies within the same migration.
/// </summary>
[Migration("20260101000000_InitialCreate")]
public class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(),
                Email = table.Column<string>(nullable: false),
                Mail = table.Column<string>(nullable: true),
                FullName = table.Column<string>(nullable: true),
            });

        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<int>(),
                Email = table.Column<string>(nullable: false),
            });

        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<int>(),
                CustomerId = table.Column<int>(),
                Status = table.Column<int>(nullable: false),
                LegacyFlag = table.Column<bool>(nullable: false),
            });

        migrationBuilder.CreateTable(
            name: "Products",
            columns: table => new
            {
                Id = table.Column<int>(),
                Description = table.Column<string>(nullable: true),
                Name = table.Column<string>(nullable: false),
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<int>(),
                Name = table.Column<string>(nullable: false),
            });

        migrationBuilder.CreateTable(
            name: "Countries",
            columns: table => new
            {
                Id = table.Column<int>(),
                Name = table.Column<string>(nullable: false),
            });

        migrationBuilder.CreateSequence(name: "OrderNumbers");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropSequence(name: "OrderNumbers");
        migrationBuilder.DropTable(name: "Countries");
        migrationBuilder.DropTable(name: "Roles");
        migrationBuilder.DropTable(name: "Products");
        migrationBuilder.DropTable(name: "Orders");
        migrationBuilder.DropTable(name: "Customers");
        migrationBuilder.DropTable(name: "Users");
    }
}
