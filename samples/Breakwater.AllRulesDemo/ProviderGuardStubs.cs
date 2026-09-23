using Microsoft.EntityFrameworkCore.Migrations;

namespace Breakwater.AllRulesDemo;

/// <summary>
/// Breakwater's provider-guard detection (<c>if (migrationBuilder.IsNpgsql())</c> / <c>IsMySql()</c>)
/// is purely syntactic - it matches on the member name, not a resolved symbol from the real
/// Npgsql/Pomelo provider packages (see MigrationOperationReader.IsGuardedByIsNpgsql /
/// IsGuardedByIsMySql). This project does not reference those provider packages (it only
/// references Microsoft.EntityFrameworkCore.Relational, matching Breakwater.Samples), so these
/// small local stubs exist purely to make the demo migrations that use the guard pattern
/// compile for real with `dotnet build`. They carry no behavior.
/// </summary>
internal static class ProviderGuardStubs
{
    public static bool IsNpgsql(this MigrationBuilder migrationBuilder) => true;

    public static bool IsMySql(this MigrationBuilder migrationBuilder) => true;
}
