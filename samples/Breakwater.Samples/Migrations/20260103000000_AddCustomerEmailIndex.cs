using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.Samples.Migrations;

/// <summary>
/// Shows BW017 (Suggestion tier: dropping a constraint removes a guarantee the application
/// may rely on) and a suppressed BW007 (a plain CreateIndex, accepted here with a reason
/// because the table is known to be tiny in this sample).
/// </summary>
[Migration("20260103000000_AddCustomerEmailIndex")]
public class AddCustomerEmailIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_Orders_Sku_NotEmpty", table: "Orders");

        // breakwater: allow BW007 Customers has under 100 rows in every environment
        migrationBuilder.CreateIndex(
            name: "IX_Customers_Email",
            table: "Customers",
            columns: new[] { "Email" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Customers_Email", table: "Customers");
        migrationBuilder.AddCheckConstraint(name: "CK_Orders_Sku_NotEmpty", table: "Orders", sql: "\"Sku\" <> ''");
    }
}
