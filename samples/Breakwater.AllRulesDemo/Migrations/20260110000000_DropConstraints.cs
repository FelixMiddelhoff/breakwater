using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>Demonstrates BW017 (DropForeignKey) and BW018 (RestartSequence).</summary>
[Migration("20260110000000_DropConstraints")]
public class DropConstraints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW017
        migrationBuilder.DropForeignKey(name: "FK_Orders_Customers", table: "Orders");

        // breakwater: expect BW018
        migrationBuilder.RestartSequence(name: "OrderNumbers", startValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
