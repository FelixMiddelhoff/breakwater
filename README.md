# Breakwater

Roslyn analyzer that catches unsafe EF Core migrations before production does.

`dotnet ef migrations add` happily generates a `DropColumn`, a `RenameColumn`,
a `CreateIndex` that locks the table, or an `AlterColumn` that fails on
existing data — and nothing warns you. Breakwater reads the migration when
you build and tells you before production does.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "Email", table: "Users");
}
```

```
warning BW001: DropColumn 'Users.Email' loses data and breaks application
               versions that still read the column
```

## Install

```
dotnet add package Breakwater.Analyzers
```

The package is not yet published to nuget.org (release is pending). Once
published, this adds one line to your `.csproj`:

```xml
<PackageReference Include="Breakwater.Analyzers" Version="0.1.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

The package contains only the analyzer: **zero runtime dependencies**, nothing
ships in your build output. It works with any EF Core provider; provider-aware
rules currently recognize SQL Server, PostgreSQL, SQLite and MySQL.

See [the tutorial](docs/tutorial.md) for a full walkthrough: install, first
warning, where results show up, reading and acting on a result, CI, and every
possible outcome.

## Rules

Only a migration's `Up` method is analyzed; `Down` is ignored (except BW011,
which looks at `Down` itself). Each rule has a page under
[`docs/rules/`](docs/rules/) with the full "why it hurts" explanation and a
safe alternative.

`breakwater_profile` (see [Configure](#configure)) controls which tier is
active: **Warning** and **Suggestion** rules run under the default
`recommended` profile; **Strict-only** rules are silent unless
`breakwater_profile = strict` is set.

| Id | Trigger | Default tier |
|----|---------|---------------|
| [BW001](docs/rules/BW001.md) | `DropColumn` | Warning |
| [BW002](docs/rules/BW002.md) | `DropTable` | Warning |
| [BW003](docs/rules/BW003.md) | `RenameColumn`, `RenameTable` | Warning |
| [BW004](docs/rules/BW004.md) | `AlterColumn` type change, length/precision narrowing | Warning |
| [BW005](docs/rules/BW005.md) | `AlterColumn` nullable to not-null without a default | Warning |
| [BW006](docs/rules/BW006.md) | `AddColumn` not-null without a default on an existing table | Warning |
| [BW007](docs/rules/BW007.md) | `CreateIndex` on an existing table without a concurrent/online annotation | Warning |
| [BW008](docs/rules/BW008.md) | `AddForeignKey` / `AddCheckConstraint` on an existing table | Warning |
| [BW009](docs/rules/BW009.md) | `AddUniqueConstraint` / `AddPrimaryKey` on an existing table | Warning |
| [BW010](docs/rules/BW010.md) | Raw `Sql(...)`: `UPDATE`/`DELETE` without `WHERE`, `TRUNCATE`, `DROP` | Warning |
| [BW011](docs/rules/BW011.md) | `Down` is empty or throws `NotSupportedException` | Strict-only |
| [BW012](docs/rules/BW012.md) | `InsertData` / `UpdateData` / `DeleteData` with many rows | Suggestion |
| [BW013](docs/rules/BW013.md) | Schema change and data change in the same migration | Suggestion |
| [BW014](docs/rules/BW014.md) | Provider-specific type change (e.g. Postgres enum-like type) | Suggestion |
| [BW015](docs/rules/BW015.md) | `AlterColumn` collation change | Warning |
| [BW016](docs/rules/BW016.md) | Postgres `AddColumn` with a volatile default (`now()`, `gen_random_uuid()`) | Warning |
| [BW017](docs/rules/BW017.md) | `DropPrimaryKey`, `DropUniqueConstraint`, `DropCheckConstraint`, `DropForeignKey` | Suggestion |
| [BW018](docs/rules/BW018.md) | `DropSchema`, `DropSequence`, `AlterSequence` / `RestartSequence` | Suggestion |
| [BW019](docs/rules/BW019.md) | Raw `Sql` on Postgres: `CREATE INDEX` without `CONCURRENTLY`, `LOCK TABLE`, `VACUUM FULL`, unsafe `ALTER TABLE ... SET DATA TYPE` | Warning |
| [BW020](docs/rules/BW020.md) | Postgres DDL migration without `SET lock_timeout` | Strict-only |
| [BW021](docs/rules/BW021.md) | `AlterDatabase` (collation, edition) | Suggestion |
| [BW022](docs/rules/BW022.md) | Migration mixes several risky operations on the same table | Suggestion |
| [BW023](docs/rules/BW023.md) | `DropColumn` and `AddColumn` on the same table (likely an undetected rename) | Warning |
| [BW024](docs/rules/BW024.md) | `DeleteData` (often from removing/changing `HasData` seed rows) | Warning |
| [BW025](docs/rules/BW025.md) | `AddForeignKey` on a column added in the same migration with a placeholder default | Warning |
| [BW026](docs/rules/BW026.md) | `AlterColumn` that adds or removes identity/value generation | Warning |
| [BW027](docs/rules/BW027.md) | `CreateIndex` with `unique: true` on an existing table | Suggestion |
| [BW028](docs/rules/BW028.md) | Migration class missing `[Migration("id")]`, or duplicate migration ids | Warning |
| [BW029](docs/rules/BW029.md) | `suppressTransaction: true`, or MySQL migration with several operations | Suggestion |
| [BW030](docs/rules/BW030.md) | `AddForeignKey` with `onDelete: Cascade` | Strict-only |
| [BW031](docs/rules/BW031.md) | Raw `Sql` containing `GO`, `USE`, or other client-side batch separators | Warning |
| [BW032](docs/rules/BW032.md) | `Database.EnsureCreated()` in a project that also has migrations | Warning |
| [BW033](docs/rules/BW033.md) | Raw `Sql` that creates a login/user with a password, or embeds a connection string | Warning |
| [BW034](docs/rules/BW034.md) | Raw `Sql` that disables a safety check (`NOCHECK CONSTRAINT`, `DISABLE TRIGGER`, `SET FOREIGN_KEY_CHECKS=0`, `DROP DATABASE`) | Warning |

`BW999` is not a migration rule: it reports if a rule itself throws, so a bug
in Breakwater surfaces as a low-severity diagnostic instead of crashing your
build.

## Configure

**Profile** — `breakwater_profile` in `.editorconfig` switches on the
strict-only rules (BW011, BW020, BW030), which are silent by default because
they are too common or too opinionated for most teams:

```ini
[*.cs]
breakwater_profile = strict
```

**Per-rule severity** — any rule's severity can be changed or turned off the
usual Roslyn way:

```ini
# Fail the build on data loss
dotnet_diagnostic.BW001.severity = error

# Turn a rule off entirely
dotnet_diagnostic.BW017.severity = none
```

**Suppress one call with a reason** — put a comment on the line above the
flagged call:

```csharp
// breakwater: allow BW001 Email column has been unused since v2.3, safe to drop
migrationBuilder.DropColumn(name: "Email", table: "Users");
```

A reason is required; an empty or missing reason leaves the diagnostic in
place and appends a note asking for one. `#pragma warning disable BW001` and
`[SuppressMessage("Migration", "BW001")]` also work, the same as any other
analyzer.

## License

MIT
