using Microsoft.EntityFrameworkCore;

namespace Breakwater.AllRulesDemo;

/// <summary>
/// A minimal DbContext just so this project looks like a real EF Core project with
/// migrations. Its model does not need to match the migrations below exactly - Breakwater
/// only looks at the migration code itself, not the model.
/// </summary>
public class ShopContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // No provider package is referenced here on purpose: this sample only exists to show
        // Breakwater's build-time warnings on the migrations below, never to actually run.
    }
}
