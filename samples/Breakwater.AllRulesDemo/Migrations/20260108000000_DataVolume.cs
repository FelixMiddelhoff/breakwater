using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW012 (many-row InsertData) and BW013 (schema change and data change mixed
/// in one migration).
/// </summary>
[Migration("20260108000000_DataVolume")]
public class DataVolume : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // breakwater: expect BW012
        migrationBuilder.InsertData(
            table: "Countries",
            column: "Name",
            values: new object[]
            {
                "C01", "C02", "C03", "C04", "C05", "C06", "C07", "C08", "C09", "C10",
                "C11", "C12", "C13", "C14", "C15", "C16", "C17", "C18", "C19", "C20",
                "C21", "C22", "C23", "C24", "C25", "C26", "C27", "C28", "C29", "C30",
                "C31", "C32", "C33", "C34", "C35", "C36", "C37", "C38", "C39", "C40",
                "C41", "C42", "C43", "C44", "C45", "C46", "C47", "C48", "C49", "C50",
                "C51",
            });

        migrationBuilder.AddColumn<string>(name: "Region", table: "Customers", nullable: true);

        // breakwater: expect BW013
        migrationBuilder.UpdateData(
            table: "Customers",
            keyColumn: "Id",
            keyValues: new object[] { 1 },
            column: "Region",
            values: new object[] { "EU" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
