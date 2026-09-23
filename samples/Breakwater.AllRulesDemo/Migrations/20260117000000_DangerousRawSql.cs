using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW031 (raw SQL containing a client-side GO batch separator), BW033 (raw SQL
/// creating a login with a plaintext password) and BW034 (raw SQL disabling a safety check).
/// </summary>
[Migration("20260117000000_DangerousRawSql")]
public class DangerousRawSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW031
        migrationBuilder.Sql("""
            ALTER TABLE Orders ADD Notes2 nvarchar(max) NULL
            GO
            UPDATE Orders SET Notes2 = ''
            """);

        // breakwater: expect BW033
        migrationBuilder.Sql("CREATE LOGIN reporting_user WITH PASSWORD = 'Sup3rSecret!'");

        // breakwater: expect BW034
        migrationBuilder.Sql("ALTER TABLE Orders NOCHECK CONSTRAINT FK_Orders_Customers");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
