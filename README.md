# Breakwater

Roslyn analyzer that flags unsafe EF Core migrations at build time.

`dotnet ef migrations add` happily generates a `DropColumn`, a `RenameColumn` or
an index build that locks the table. Breakwater reads the migration when you
build and tells you before production does.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "Email", table: "Users");
    // warning BW001: DropColumn 'Users.Email' loses data and breaks application
    //                versions that still read the column
}
```

## Install

```
dotnet add package Breakwater.Analyzers
```

The package contains only the analyzer. It adds no runtime dependency and works
with any EF Core provider.

## Learn more

- [Tutorial](docs/tutorial.md): install, try it, read results, configure, use in CI, troubleshoot.

## Rules

| Id | What it flags |
|----|---------------|
| [BW001](docs/rules/BW001.md) | `DropColumn` |
| [BW002](docs/rules/BW002.md) | `DropTable` |
| [BW003](docs/rules/BW003.md) | `RenameColumn`, `RenameTable` |

Only `Up` is analyzed; `Down` is not.

## Configure

Change a rule's severity in `.editorconfig`:

```ini
# Fail the build on data loss
dotnet_diagnostic.BW001.severity = error

# Turn a rule off
dotnet_diagnostic.BW003.severity = none
```

## License

MIT
