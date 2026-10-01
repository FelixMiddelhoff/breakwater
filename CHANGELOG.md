# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- `BW037`: raw `CREATE TABLE`/`CREATE INDEX` with no `IF NOT EXISTS` guard
  (Suggestion tier; intentionally co-fires with `BW019` on Postgres since
  they flag different hazards on the same statement).
- `breakwater-sql init`: scaffolds a starter `.editorconfig` listing every
  `breakwater_*` key, commented out.
- `breakwater-sql --format sarif`: SARIF 2.1.0 output for GitHub code
  scanning, alongside the existing `table`/`json` formats.
- `breakwater-sql --fail-on <suggestion|warning>`: gate the exit code by
  severity instead of failing on any finding (default `warning`, matching
  prior behavior since every covered rule but `BW036` is Warning tier).
- `breakwater-sql --ignore <RULE:LINE>` / `--ignore-file <path>`: suppress
  specific findings in a generated script, which has no suppression-comment
  mechanism of its own.
- `breakwater-sql` now also checks `BW037` (was analyzer-only at first).
- Widened NuGet `PackageTags` on both packages for discoverability.
- Confirmed Central Package Management works cleanly with both packages;
  documented in `docs/tutorial.md`.

### Fixed
- README was missing `Configure` documentation for `breakwater_provider`,
  `breakwater_deploy_model`, `breakwater_since_migration` and
  `breakwater_small_tables` (only `breakwater_profile` was documented).
- `docs/tutorial.md`'s troubleshooting section said "until a baseline option
  exists" — `breakwater_since_migration` already is that option; corrected.
- README was missing a NuGet badge for `Breakwater.Tool`.

## [0.2.0]

### Added
- `Breakwater.Tool` (`breakwater-sql`): a companion dotnet global tool that
  lints the SQL script produced by `dotnet ef migrations script` for the
  same unsafe shapes the analyzer catches in inline `Sql(...)` calls, but
  against the final provider-specific SQL (`--provider` is required: a
  generated script has no C# guard to detect it from). Packed and released
  alongside `Breakwater.Analyzers` from the same `release.yml` run, sharing
  its version.
- `.github/actions/pr-comment`: a reusable composite GitHub Action other EF
  Core repos can use to get the same SARIF-based PR-comment check breakwater
  runs on its own PRs. Expects the consuming project to already reference
  `Breakwater.Analyzers` — does not modify the consumer's project files.

### Fixed
- `BW019`'s doc comment understated detection: it also fires via an
  explicit `breakwater_provider = postgres` override, not only the
  `IsNpgsql()` guard heuristic.

### Changed
- CI dependency bumps: `actions/checkout` 4→7, `coverlet.collector`
  10.0.1→10.1.0, `Microsoft.SourceLink.GitHub` 8.0.0→10.0.401,
  `Microsoft.CodeAnalysis.Analyzers`/`CSharp`/`CSharp.Workspaces` to 5.9.0
  (required adding `RS1038` to the analyzer project's `NoWarn`, since a
  compiler-extension-referencing-Workspaces check now fires project-wide
  even though only `CodeFixes/` uses Workspaces APIs).

[0.2.0]: https://github.com/FelixMiddelhoff/breakwater/releases/tag/v0.2.0

## [0.1.0]

### Added
- Roslyn analyzer for EF Core migrations covering 34 rules (`BW001`-`BW034`)
  across removal, column change, index/constraint, raw SQL, data, and
  discoverability categories: unsafe drops/renames, non-concurrent index
  creation, blocking constraint additions, NOT NULL columns without a
  default, unbounded data operations, unsafe raw SQL statements
  (`TRUNCATE`, filterless `UPDATE`/`DELETE`, SQL Server `GO` batches,
  `DROP DATABASE`), missing `[Migration]` attributes, duplicate migration
  ids, and more. See `docs/rules/` for the full list, one page per rule.
- `BW999`: crash guard — a failure in one rule's analysis is caught and
  reported instead of crashing the whole analyzer, and does not stop other
  rules from running.
- Severity tiers and profiles: `breakwater_profile = recommended | strict`
  (default `recommended`). Warning-tier rules are high-confidence and
  real-impact; Suggestion-tier rules are IDE hints only; strict-only rules
  (`BW011`, `BW020`, `BW030`) are off by default and enabled under
  `strict`.
- Suppression: `// breakwater: allow BWxxx <reason>` (a non-empty reason is
  required), plus standard `#pragma warning disable BWxxx` and
  `[SuppressMessage("Migration", "BWxxx")]`.
- SQL Server, PostgreSQL, SQLite and MySQL support, including
  provider-specific rules (e.g. `Npgsql`/MySQL-specific concurrent index
  and lock-timeout checks) that stay silent when the provider cannot be
  determined.
- Zero runtime dependencies; ships as an analyzer-only NuGet package.
- `docs/tutorial.md`: a full walkthrough from install to a rule fix.
- `docs/rules/BW001.md`-`BW034.md`: one documentation page per rule, each
  with a compiling example verified by an automated documentation test.
- `samples/Breakwater.Samples/`: a minimal EF Core sample project
  demonstrating Warning, Suggestion, and suppressed findings.
- Real-world corpus scan tooling (`tools/CorpusScan/`) used to validate
  rule precision against public EF Core repositories before release.

[0.1.0]: https://github.com/FelixMiddelhoff/breakwater/releases/tag/v0.1.0
