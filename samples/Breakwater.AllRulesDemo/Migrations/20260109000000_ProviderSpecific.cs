using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW014 (provider-specific type change), BW015 (collation change) and BW016
/// (Postgres volatile default rewrites the table). The project-wide breakwater_provider =
/// postgres setting makes BW014/BW016 fire without needing the IsNpgsql() guard, but the
/// guard is kept here too since it is how a real migration would look and the rule must also
/// work when detection falls back to guard-sniffing.
/// </summary>
[Migration("20260109000000_ProviderSpecific")]
public class ProviderSpecific : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.IsNpgsql())
        {
            // breakwater: expect BW014
            migrationBuilder.AlterColumn<OrderStatusV2>(
                name: "Status",
                table: "Orders",
                oldClrType: typeof(OrderStatusV1));

            // breakwater: expect BW016
            migrationBuilder.AddColumn<System.DateTime>(
                name: "CreatedAt",
                table: "Orders",
                defaultValueSql: "now()");
        }

        // breakwater: expect BW015
        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "Products",
            collation: "Latin1_General_CI_AS",
            oldCollation: "SQL_Latin1_General_CP1_CI_AS");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}

public enum OrderStatusV1 { Open, Closed }
public enum OrderStatusV2 { Open, Fulfilled, Closed }
