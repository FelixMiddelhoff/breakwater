using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Negative cases: operations that look risky but are safe and must NOT raise any
/// diagnostic, demonstrating the "silent when unsure" / "new tables are empty" policy.
/// </summary>
[Migration("20260119000000_SafeChanges")]
public class SafeChanges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Safe: widening a column length is not a narrowing AlterColumn - no BW004.
        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Products",
            maxLength: 500,
            oldMaxLength: 50);

        // Safe: not-null AddColumn with a constant default fails on nothing - no BW006.
        migrationBuilder.AddColumn<int>(
            name: "SortOrder",
            table: "Products",
            nullable: false,
            defaultValue: 0);

        // Safe: table created in this very migration is empty - no BW006/BW009 apply to it.
        migrationBuilder.CreateTable(
            name: "AuditLog",
            columns: table => new
            {
                Id = table.Column<int>(),
                Message = table.Column<string>(nullable: false),
            });

        migrationBuilder.AddUniqueConstraint(
            name: "AK_AuditLog_Id",
            table: "AuditLog",
            columns: new[] { "Id" });

        // Safe: dropping a constraint we just added on a brand-new table - no BW017 concern,
        // still fires as advisory in real teams' code, but the table is new here so no
        // higher-risk lock is involved. Left as-is intentionally to show the constraint-only
        // path is still reported (BW017 is a Suggestion, not a negative case) - see README.

        // Safe: DeleteData on data inserted in this same migration is not a production
        // seed-row removal in spirit, but Breakwater does not special-case this (documented
        // as an open item, not claimed as a negative case).
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
