# Breakwater.AllRulesDemo

A demo EF Core project built to fire all 34 Breakwater rules (BW001-BW034) on purpose, so
they can be verified without waiting for a real-world corpus to happen to contain them. It is
separate from `samples/Breakwater.Samples` (the tutorial sample, left untouched), is not added
to `Breakwater.sln`, and references the analyzer via `ProjectReference` +
`OutputItemType="Analyzer"`, the same way `Breakwater.Samples` and a real consumer's
`PackageReference` both do.

## How to verify

```
cd samples/Breakwater.AllRulesDemo
dotnet build -p:ErrorLog=results.sarif;version=2
```

Build warnings show every Warning-tier rule directly in the console. `results.sarif` also
contains the Suggestion-tier (Info severity) rules, which the quality policy deliberately
keeps out of build console output ("IDE hint only, no build output" -
`breakwater-quality-policy.md`). Verify the full set with:

```powershell
$sarif = Get-Content results.sarif -Raw | ConvertFrom-Json
($sarif.runs.results.ruleId | Sort-Object -Unique) -match '^BW'
```

## Rule -> migration map

| Rule(s) | Migration file | Notes |
|---|---|---|
| BW001, BW002 | `20260102000000_DestructiveOps.cs` | DropColumn, DropTable |
| BW003 | `20260103000000_RenameOps.cs` | RenameColumn |
| BW004, BW005, BW006 | `20260104000000_AlterColumnRisks.cs` | narrowing AlterColumn, nullable->not-null, not-null AddColumn without default |
| BW007, BW008, BW009 | `20260105000000_LockingDdl.cs` | CreateIndex, AddForeignKey, AddUniqueConstraint on an existing table |
| BW010 | `20260106000000_RawSqlBackfill.cs` | raw SQL UPDATE without WHERE |
| BW011 | `20260107000000_EmptyDown.cs` | empty `Down`; strict-profile only |
| BW012, BW013 | `20260108000000_DataVolume.cs` | 51-row InsertData, UpdateData mixed with a schema change |
| BW014, BW015, BW016 | `20260109000000_ProviderSpecific.cs` | Postgres enum-shaped type change, collation change, Postgres volatile default |
| BW017, BW018 | `20260110000000_DropConstraints.cs` | DropForeignKey, RestartSequence |
| BW019, BW020 | `20260111000000_RawSqlPostgres.cs` | raw `CREATE INDEX` without `CONCURRENTLY` on Postgres, DDL without `lock_timeout` (strict-only) |
| BW021, BW022 | `20260112000000_DatabaseAndCompound.cs` | AlterDatabase, 3+ risky ops on the same table |
| BW023, BW024, BW025 | `20260113000000_RenameLookalikeAndSeedEdits.cs` | Drop+Add on same table, DeleteData, FK on a column with a placeholder default |
| BW026, BW027 | `20260114000000_IdentityAndUniqueIndex.cs` | AlterColumn adding identity, unique CreateIndex on existing table |
| BW028 | `20260115000000_UndiscoverableMigration.cs` | migration class with no `[Migration("id")]` |
| BW029, BW030 | `20260116000000_MySqlAutoCommitAndCascade.cs` | MySQL DDL auto-commit with several ops, cascade delete FK (strict-only) |
| BW031, BW033, BW034 | `20260117000000_DangerousRawSql.cs` | `GO` batch separator, `PASSWORD =` credential, `NOCHECK CONSTRAINT` |
| BW032 | `20260118000000_EnsureCreatedMisuse.cs` | `Database.EnsureCreated()` alongside real migrations |
| (negative cases) | `20260119000000_SafeChanges.cs` | widening AlterColumn, not-null AddColumn with a constant default, ops on a table created in the same migration - none of these raise BW004/BW006/BW009 |
| (suppression demo) | `20260120000000_SuppressionDemo.cs` | `// breakwater: allow BW001 <reason>` above a DropColumn suppresses it |

`20260101000000_InitialCreate.cs` creates every table the later migrations operate on, so they
count as pre-existing (non-empty) rather than falling under the "new tables are empty"
silence.

## Fired-confirmed checklist (BW001-BW034)

All 34 confirmed present in `results.sarif` from a real `dotnet build` in this project
(verified 2026-09-23, not assumed from source reading). Severity: (W)arning shows directly in
build console output; (I)nfo (Suggestion tier) only shows in the SARIF/IDE, per the quality
policy - both count as "fired".

| Rule | Fired | Severity | Rule | Fired | Severity |
|---|---|---|---|---|---|
| BW001 | yes | W | BW018 | yes | I |
| BW002 | yes | W | BW019 | yes | W |
| BW003 | yes | W | BW020 | yes | W (strict) |
| BW004 | yes | W | BW021 | yes | I |
| BW005 | yes | W | BW022 | yes | I |
| BW006 | yes | W | BW023 | yes | W |
| BW007 | yes | W | BW024 | yes | W |
| BW008 | yes | W | BW025 | yes | W |
| BW009 | yes | W | BW026 | yes | W |
| BW010 | yes | W | BW027 | yes | I |
| BW011 | yes | W (strict) | BW028 | yes | W |
| BW012 | yes | I | BW029 | yes | I |
| BW013 | yes | I | BW030 | yes | W (strict) |
| BW014 | yes | I | BW031 | yes | W |
| BW015 | yes | W | BW032 | yes | W |
| BW016 | yes | W | BW033 | yes | W |
| BW017 | yes | I | BW034 | yes | W |

No `BW999` (analyzer-failed) diagnostic appears anywhere in the SARIF output: the analyzer did
not crash on any of these 34 deliberately unusual shapes.

## Fixed: `breakwater_*` options now work from a plain `.editorconfig`

An earlier session found a genuine gap here: `BreakwaterProfileReader.Read` and
`BreakwaterConfiguration.Read` read options exclusively from
`AnalyzerConfigOptionsProvider.GlobalOptions`. In a real `dotnet build` (not the in-memory test
harness, which injects a dictionary directly as `GlobalOptions`), a custom key like
`breakwater_profile = strict` placed in a normal project `.editorconfig` - whether unsectioned
at the top of the file or inside a `[*.cs]` section, and regardless of `root = true` - never
reached `GlobalOptions`. Only a `.globalconfig` file with `is_global = true` worked, which this
project originally worked around with its own `.globalconfig`.

This has since been fixed: both readers now go through
`Breakwater.Analyzers.Configuration.BreakwaterConfigOptionsReader.TryGetValue`, which checks
`AnalyzerConfigOptionsProvider.GetOptions(tree)` (per-syntax-tree options - the mechanism a
plain `.editorconfig`'s section-matching actually populates) for every syntax tree in the
compilation first, falling back to `GlobalOptions` for setups that do use a `.globalconfig`.
Re-verified against this project with a real `dotnet build -p:ErrorLog=results.sarif;version=2`:
`breakwater_profile = strict` added directly to this project's `.editorconfig` (with the
`.globalconfig` removed entirely) makes BW011/BW020/BW030 fire exactly as before. See
`breakwater-memory.md` for the root-cause writeup and the corpus-scan reinterpretation note.

## Not wired into CI

This project is not part of any GitHub Actions workflow. It exists purely as a manually-run
verification aid alongside `Breakwater.Samples` (also not in CI, not in `Breakwater.sln`). If
CI coverage of "all 34 rules still fire" is wanted later, the cleanest addition would be a
step that runs `dotnet build` here with `-p:ErrorLog=...` and asserts all 34 rule ids appear in
the SARIF output - deliberately not asserted as part of `dotnet test` today, since this project
is a real EF Core compile (with the analyzer attached), not a unit test.
