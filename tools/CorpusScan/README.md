# CorpusScan

Real-world-corpus scan tool for the precision gate in
`breakwater-quality-policy.md`. Not shipped in the NuGet package; used
locally and later by the `soak` CI workflow (GitHub-Release agent's scope)
to run the analyzer over public EF Core migration files and collect
findings per rule.

## What it does

For each corpus source (a local checkout of an open-source repo, or a
local directory of hand-written synthetic migration snippets):

1. Finds every `*.cs` file under a `Migrations` folder (or a configured
   folder), excluding `*.Designer.cs` and `*ModelSnapshot.cs` (those are
   not `Up`/`Down` methods and are not migrations analyzed by this tool
   or by breakwater itself, per `breakwater-quality-policy.md` principle
   5).
2. Generates a disposable `net8.0` console project per source (under
   `tools/CorpusScan/_work/<source>/`) that includes those files, wired
   up exactly like `samples/Breakwater.Samples`: a
   `Microsoft.EntityFrameworkCore.Relational` PackageReference plus a
   `ProjectReference` to `Breakwater.Analyzers.csproj` with
   `OutputItemType="Analyzer"` — the same mechanism a real
   `<PackageReference>` to the packed analyzer produces.
3. Runs `dotnet build -p:ErrorLog=<path>.sarif` and parses the emitted
   SARIF for `BW0xx` diagnostics (any severity — the analyzer's own
   `IsEnabledByDefault`/profile gating already decided what a normal
   build would show, but the scan wants every rule's raw finding count
   including Suggestion/strict-only ones, so it also passes
   `breakwater_profile = strict` via a per-source `.editorconfig` to
   surface BW011/BW020/BW030 too).
4. Aggregates findings per rule id across all sources into
   `tools/CorpusScan/results/corpus-scan.json` (counts, file:line
   locations, and the migration snippet's source repo) and a compact
   `tools/CorpusScan/results/corpus-scan-summary.md` scratch file for
   the reviewer to hand-label (this summary is not committed narrative
   content — see the note in `corpus-scan.json` on where it lives).

A compile error in a migration file (e.g. a custom enum type the
migration references that isn't available without the full source
project) does not stop the scan: `dotnet build` still runs analyzers and
still emits SARIF for the analyzer diagnostics even when the overall
build fails on unrelated `CS...` errors, so BW findings on the
successfully-resolved parts of the file are still collected. Files that
produce zero analyzer-relevant diagnostics because Roslyn could not bind
the `MigrationBuilder` calls at all are recorded as "unusable" in the
JSON output rather than silently dropped.

## Usage

```powershell
pwsh tools/CorpusScan/run-scan.ps1 -Sources tools/CorpusScan/sources.json
```

`sources.json` lists each corpus source: a `name`, a local `path`, and
whether it is `real` (an open-source repo checkout) or `synthetic` (hand-
written snippets under this tool's own `synthetic-corpus/` folder).

## Known limitation

This tool clones/reads from whatever local checkouts you point it at. It
does not itself perform network `git clone` calls (kept out of scope to
avoid the tool silently depending on network access at CI time); the
`soak` GitHub Actions workflow (GitHub-Release agent's scope) is where
repo checkout/caching for the real-corpus run belongs.
