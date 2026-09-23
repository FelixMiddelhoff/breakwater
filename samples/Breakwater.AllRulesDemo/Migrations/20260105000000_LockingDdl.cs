using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW007 (CreateIndex without concurrent/online), BW008 (AddForeignKey on an
/// existing table validated under lock) and BW009 (AddUniqueConstraint under lock). The
/// project's .editorconfig sets breakwater_provider = postgres explicitly, so these fire
/// without needing an IsNpgsql() runtime guard.
/// </summary>
[Migration("20260105000000_LockingDdl")]
public class LockingDdl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW007
        migrationBuilder.CreateIndex(
            name: "IX_Orders_CustomerId",
            table: "Orders",
            columns: new[] { "CustomerId" });

        // breakwater: expect BW008
        migrationBuilder.AddForeignKey(
            name: "FK_Orders_Customers",
            table: "Orders",
            columns: new[] { "CustomerId" },
            principalTable: "Customers");

        // breakwater: expect BW009
        migrationBuilder.AddUniqueConstraint(
            name: "AK_Customers_Email",
            table: "Customers",
            columns: new[] { "Email" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
