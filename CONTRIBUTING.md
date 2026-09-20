# Contributing

Thanks for helping. Bug reports, false positives and rule ideas are all
welcome as issues; pull requests are welcome for anything you have already
discussed in an issue.

## Build and test

You need the .NET 8 SDK or newer.

```
dotnet build
dotnet test
```

## How the code is organised

- `src/Breakwater.Analyzers/Operations`: turns a call on `MigrationBuilder`
  into a small plain object (`MigrationOperation`).
- `src/Breakwater.Analyzers/Rules`: one class per rule. A rule looks at one
  `MigrationOperation` and either returns the message arguments or null.
- `tests/`: every rule has tests for the case it reports, the safe case
  next to it, and edge cases. Test snippets use small look-alikes of the EF
  Core types (`tests/.../Support/EfCoreStubs.cs`).
- `docs/rules/`: one page per rule.

## Adding a rule

1. Create `Rules/<Name>Rule.cs` implementing `IMigrationRule` and add it to
   `RuleCatalog`.
2. Add tests: reported, not reported, and the edge cases you can think of
   (named arguments, schema, unknown names, `Down`).
3. Add `docs/rules/BWxxx.md` and a line in the README rule table and in
   `CHANGELOG.md`.

## Style

Code should read well for a person who has not seen it before: short methods,
names that say what a thing is, comments that explain why. `dotnet build`
treats warnings as errors and enforces the `.editorconfig`.

## Releasing

Maintainers only. Set `<Version>` in `Directory.Build.props`, move the
`[Unreleased]` entries in `CHANGELOG.md` under the new version, and push a
tag `vX.Y.Z`. The `release` workflow checks that everything agrees, runs the
tests, creates the GitHub release and publishes the package to nuget.org.
