# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

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
