using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo.Migrations;

/// <summary>
/// Demonstrates BW032 (Database.EnsureCreated() called in a project that also has
/// migrations - EnsureCreated skips the migration history table entirely).
/// </summary>
public class DemoStartup
{
    public void Configure(DatabaseFacade database)
    {
        // breakwater: expect BW032
        database.EnsureCreated();
    }
}

[Migration("20260118000000_EnsureCreatedMisuse")]
public class EnsureCreatedMisuse : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
