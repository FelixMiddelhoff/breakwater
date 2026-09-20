# Tutorial

This tutorial takes you from installing Breakwater to reading and acting on its
results. It assumes you have an EF Core project with migrations, or you are
willing to make a small one as shown in step 2.

Contents:

1. [Install](#1-install)
2. [See your first warning](#2-see-your-first-warning)
3. [Where results show up](#3-where-results-show-up)
4. [Read a result](#4-read-a-result)
5. [Act on a result](#5-act-on-a-result)
6. [Use it in CI](#6-use-it-in-ci)
7. [Every possible outcome](#7-every-possible-outcome)
8. [Troubleshooting](#8-troubleshooting)
9. [Uninstall](#9-uninstall)

## 1. Install

Add the package to the project that contains your migrations (the one with the
`Migrations` folder, not necessarily the one with your `DbContext` usage):

```
dotnet add package Breakwater.Analyzers
```

This adds one line to the `.csproj`:

```xml
<PackageReference Include="Breakwater.Analyzers" Version="0.1.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

`PrivateAssets=all` means the package never flows to projects that reference
yours, and nothing from it ends up in your build output. Breakwater is a
compile-time tool only.

To cover every project in a solution at once, put the same
`<PackageReference>` in a `Directory.Build.props` file at the solution root.
Projects without EF Core are ignored by the analyzer, so this is safe.

Requirements: a .NET SDK that can build your project (.NET 6 SDK or newer) and
EF Core migrations (the analyzer looks for
`Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder`, which comes with
`Microsoft.EntityFrameworkCore.Relational`). It works with every EF Core
provider.

## 2. See your first warning

If you have no migration yet, create a throwaway class library:

```
dotnet new classlib -n Shop
cd Shop
dotnet add package Microsoft.EntityFrameworkCore.Relational
dotnet add package Breakwater.Analyzers
```

Add `Migrations/Cleanup.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

namespace Shop.Migrations;

public partial class Cleanup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Email", table: "Users");
        migrationBuilder.RenameColumn(name: "Mail", table: "Customers", newName: "EmailAddress");
        migrationBuilder.DropTable(name: "LegacyOrders", schema: "shop");
    }
}
```

Build:

```
dotnet build
```

You get three warnings (the line and column depend on your file):

```
Migrations/Cleanup.cs(9,9): warning BW001: DropColumn 'Users.Email' loses data and breaks application versions that still read the column (https://github.com/FelixMiddelhoff/breakwater/blob/main/docs/rules/BW001.md)
Migrations/Cleanup.cs(10,9): warning BW003: RenameColumn 'Customers.Mail' to 'EmailAddress' breaks application versions that still use the old name (https://github.com/FelixMiddelhoff/breakwater/blob/main/docs/rules/BW003.md)
Migrations/Cleanup.cs(11,9): warning BW002: DropTable 'shop.LegacyOrders' loses data and breaks application versions that still read the table (https://github.com/FelixMiddelhoff/breakwater/blob/main/docs/rules/BW002.md)
```

If you use real migrations instead, note that `dotnet ef migrations add`
produces exactly these calls when you delete or rename a property, so warnings
usually appear right after you generate a migration and build.

## 3. Where results show up

- **Command line**: `dotnet build` and `dotnet test` print them as warnings
  (or errors, see step 5).
- **Visual Studio, Rider, VS Code (C# Dev Kit)**: squiggles under the call
  while you edit, and entries in the error list / problems panel. A rule id in
  the list links to its documentation page. If nothing appears right after
  installing, reload the solution once so the IDE picks up the analyzer.
- **SARIF file**: ask the compiler to write all diagnostics to a file that
  code-scanning tools understand:

  ```
  dotnet build -p:ErrorLog=breakwater.sarif
  ```

  The file lists every finding with rule id, message and position.

## 4. Read a result

```
Migrations/Cleanup.cs(9,9): warning BW001: DropColumn 'Users.Email' loses data ... (https://...)
```

| Part | Meaning |
|------|---------|
| `Migrations/Cleanup.cs(9,9)` | File, line and column of the `migrationBuilder` call |
| `warning` | Severity; you control it (step 5) |
| `BW001` | Rule id; the page `docs/rules/BW001.md` explains it |
| `DropColumn 'Users.Email'` | The operation and the table and column it touches. A schema is included when the call passes one (`shop.LegacyOrders`) |
| link | The rule's documentation page: why it hurts, the safe alternative |

Names that are not compile-time constants (for example returned from a
method) are shown as `?`, for instance `'Users.?'`. The warning is still
reported because the operation itself is what is risky.

## 5. Act on a result

You have four choices per finding. Pick deliberately; the rule pages explain
what "safe" means for each rule.

**Fix the migration.** Follow the safe alternative on the rule's page, for
example split a column drop over two releases. This is the intended outcome.

**Accept it for one call.** Wrap the call in a pragma. This documents that you
looked at it:

```csharp
// Column has been unused since release 4.2; old versions are gone.
#pragma warning disable BW001
migrationBuilder.DropColumn(name: "Email", table: "Users");
#pragma warning restore BW001
```

**Change how strict a rule is** in `.editorconfig` (project-wide or per
folder; a file in `Migrations/` affects only that folder):

```ini
[*.cs]
# Fail the build when a migration drops a column
dotnet_diagnostic.BW001.severity = error

# Show a rename only as a hint in the IDE
dotnet_diagnostic.BW003.severity = suggestion

# Turn a rule off
dotnet_diagnostic.BW002.severity = none
```

Valid severities: `error`, `warning`, `suggestion`, `silent`, `none`.

**Turn a rule off for the build only**, without touching `.editorconfig`:

```
dotnet build -p:NoWarn=BW003
```

## 6. Use it in CI

Because findings are ordinary compiler diagnostics, no extra tool is needed.

Fail the pipeline on data loss but keep the rest as warnings:

```
dotnet build -warnaserror:BW001,BW002
```

Or fail on every Breakwater rule with `dotnet_diagnostic.BWxxx.severity = error`
lines in `.editorconfig`, as in step 5.

Note that `dotnet build` skips compiling when nothing changed, so a cached CI
run can show no warnings. Add `--no-incremental` when you want a fresh
analysis.

Upload the SARIF file to GitHub code scanning to see findings on pull
requests:

```yaml
- run: dotnet build --configuration Release -p:ErrorLog=breakwater.sarif
- uses: github/codeql-action/upload-sarif@v3
  if: always()
  with:
    sarif_file: breakwater.sarif
```

(Uploading needs `security-events: write` permission on the job, and code
scanning enabled for the repository.)

## 7. Every possible outcome

| What you see | What it means | What to do |
|--------------|---------------|------------|
| Nothing at all | Either the migrations are fine as far as the current rules can tell, or the analyzer did not run (see step 8) | Check with the example from step 2 that warnings appear |
| `warning BW00x` | A risky operation in `Up` | Fix, suppress or reconfigure (step 5) |
| `error BW00x` | You raised the rule to `error` or use `-warnaserror` | Same, the build fails until resolved |
| Warning with a `?` in the name | The table or column name is computed, not a constant | Judge by the code; the finding is still valid |
| Warning inside a helper method, not in `Up` | Helper methods that take a `MigrationBuilder` are analyzed too | Fix or suppress there |
| No warning for an operation in `Down` | `Down` is not analyzed on purpose; undoing a migration is rarely run in production | Nothing |
| No warning for a method called `DropColumn` on your own type | Only calls on EF Core's `MigrationBuilder` are analyzed | Nothing |
| No warning for an unknown operation such as your own extension method on `MigrationBuilder` | Breakwater does not guess what custom operations do | Review by hand |

The rules covered so far are BW001 to BW003; the table above grows with each
new rule, and the README lists the current set.

## 8. Troubleshooting

**No warnings although the migration has `DropColumn`.**

- The package must be referenced by the project that *contains* the migration
  code. Check `dotnet list package` in that project.
- The project must reference EF Core relational (`Microsoft.EntityFrameworkCore.Relational`);
  without `MigrationBuilder` the analyzer has nothing to look at.
- Rebuild without the cache: `dotnet build --no-incremental`.
- The call must be inside `Up` (or a helper), not `Down`.
- In the IDE, reload the solution once after installing.

**Warnings appear in the IDE but not on the command line (or the reverse).**
Build with the same configuration in both, and check that no `NoWarn` or
`.editorconfig` file differs between what the IDE and the build see.

**A migration from years ago is flagged.** It is already in production and
cannot be changed. Until a baseline option exists, lower the severity for the
old files with a `.editorconfig` in an `OldMigrations` folder, or suppress the
rules there.

**Build time.** The analyzer only inspects calls in your migration code, so its
cost is negligible compared to compilation.

## 9. Uninstall

```
dotnet remove package Breakwater.Analyzers
```

Nothing else was added to your project, so nothing else has to be cleaned up
(except any `.editorconfig` lines and `#pragma warning` comments you added
for `BW` rule ids).
