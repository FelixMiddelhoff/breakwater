# Breakwater PR Comment action

A composite GitHub Action that builds your EF Core project and posts (or
updates) a single pull-request comment summarizing
[Breakwater](https://github.com/FelixMiddelhoff/breakwater) migration-safety
findings on the files the PR actually touches. It's the same mechanism
breakwater uses to comment on its own PRs
(`.github/workflows/pr-review.yml` in this repo), packaged so other EF Core
repos can reuse it without copy-pasting the workflow and script.

> **Status: not yet published.** `Breakwater.Analyzers` has not been pushed
> to nuget.org yet (see the main [README](../../../README.md#install)), and
> this action has not been tagged (`@v1` below is aspirational until a
> release tag exists). Until then, point `uses:` at a commit SHA on this
> repo if you want to try it. This section documents the intended, honest
> end state.

## What it does

1. Diffs the PR's head against its base ref to get the list of changed
   files.
2. Runs `dotnet build` with MSBuild's `-p:ErrorLog=...;version=2` logger to
   produce a SARIF file — no extra package, this is a standard MSBuild
   feature.
3. Reads that SARIF file, filters findings down to Breakwater's own rule ids
   (`BW###`) on files the PR changed, and posts one summarizing comment. A
   second run on the same PR edits that comment in place (it's marked with
   an HTML comment so it's found again) instead of posting a new one each
   push.

## Design choice: your repo must already reference Breakwater.Analyzers

This action does **not** inject a `<PackageReference>` into your `.csproj`
for you. It expects the project(s) under `working-directory` to already
reference `Breakwater.Analyzers` (see the main README's
[Install](../../../README.md#install) section) before this action runs.

We considered having the action add the reference automatically (e.g. via
`dotnet add package`) so a consumer's workflow would need even less setup.
We chose not to, because silently rewriting a `.csproj` the action doesn't
own is a bigger, riskier claim than a CI step should make on someone else's
repository — it can collide with central package management, lock files,
pinned versions, or a `Directory.Packages.props`, and it's surprising
behavior to debug when it goes wrong. Requiring the reference up front is
one extra line of setup for the consumer (`dotnet add package
Breakwater.Analyzers`) in exchange for the action never touching files it
wasn't asked to touch.

## Inputs

| Input | Required | Default | Description |
|---|---|---|---|
| `github-token` | no | `${{ github.token }}` | Token used to read/write the PR comment. Needs `pull-requests: write`. |
| `working-directory` | no | `.` | Directory containing the solution/project(s) to build. |
| `build-args` | no | `` | Extra arguments appended to `dotnet build` (e.g. a specific `.sln` path, `--configuration Release`). |
| `sarif-path` | no | `breakwater-results.sarif` | Path (relative to `working-directory`) the SARIF log is written to and read from. |
| `base-ref` | no | the PR's base ref | Base branch to diff against when computing changed files. |
| `docs-base-url` | no | `` | Base URL for linking each finding's rule id to its docs page, e.g. `https://github.com/OWNER/REPO/blob/main/docs/rules`. Rule ids render as plain text if left empty. |
| `dotnet-version` | no | `8.0.x` | SDK version passed to `actions/setup-dotnet`. |
| `setup-dotnet` | no | `true` | Set `false` if your workflow already sets up the .NET SDK it wants. |

## Example consumer workflow

```yaml
name: breakwater-pr-review
on:
  pull_request:
    branches: [ main ]

permissions:
  contents: read
  pull-requests: write

jobs:
  breakwater:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0
      - uses: FelixMiddelhoff/breakwater/.github/actions/pr-comment@v1
```

That's it — assuming your `.csproj`(s) already reference
`Breakwater.Analyzers`. `fetch-depth: 0` is required so the action can diff
against the PR's base branch. Like breakwater's own workflow, use
`pull_request` (not `pull_request_target`): it builds the PR's own code
under a fork-safe, read-mostly token.

A longer example pinning the .NET version and a specific solution file:

```yaml
      - uses: FelixMiddelhoff/breakwater/.github/actions/pr-comment@v1
        with:
          dotnet-version: '9.0.x'
          build-args: MyApp.sln
          docs-base-url: https://github.com/FelixMiddelhoff/breakwater/blob/main/docs/rules
```

## Limitations

- Requires the consumer's project(s) to already reference
  `Breakwater.Analyzers` (see above).
- Only comments on findings for files the PR changed, same as breakwater's
  own workflow — it won't surface pre-existing findings elsewhere in the
  repo.
- Not yet live-fired across a real cross-repo Action run as part of this
  change; see the root `pr-review.yml` for the same caveat about this
  repo's own workflow. The composite action's YAML was validated for syntax
  and the underlying `post-sarif-comment.js` logic was exercised locally
  against a real SARIF file (see commit message / PR description for
  details).
