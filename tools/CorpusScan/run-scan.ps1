<#
.SYNOPSIS
    Runs the breakwater analyzer over migration files from one or more corpus
    sources (real open-source checkouts or a local synthetic corpus) and
    records per-rule findings for the precision gate
    (breakwater-quality-policy.md). See README.md in this folder.

.PARAMETER Sources
    Path to a JSON file listing corpus sources. Each entry:
      { "name": "eShop", "path": "D:/Claude/eShop", "kind": "real" }
    "path" is searched recursively for *.cs files under any "Migrations"
    folder (case-insensitive), excluding *.Designer.cs and *ModelSnapshot.cs.
    For a "synthetic" source, every *.cs file directly under "path" is used
    (no Migrations-folder filtering) since the synthetic corpus is already
    just migration snippets.

.PARAMETER OutDir
    Where results go. Defaults to tools/CorpusScan/results next to this
    script.
#>
param(
    [Parameter(Mandatory = $true)][string]$Sources,
    # Left unset by default rather than "$PSScriptRoot/results" here: in
    # Windows PowerShell 5.1, a default parameter value referencing
    # $PSScriptRoot is bound before the script body runs, when $PSScriptRoot
    # is not yet populated -- it silently evaluates to "" and writes results
    # to the filesystem root (e.g. D:\results) instead of next to this
    # script. Resolved for real below, once $PSScriptRoot is valid.
    [string]$OutDir
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/../.."
$analyzerProj = Join-Path $root "src/Breakwater.Analyzers/Breakwater.Analyzers.csproj"
$workDir = "$PSScriptRoot/_work"
if ([string]::IsNullOrEmpty($OutDir)) { $OutDir = "$PSScriptRoot/results" }
New-Item -ItemType Directory -Force -Path $workDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$sourceList = Get-Content $Sources -Raw | ConvertFrom-Json

$allFindings = @()   # one row per BW diagnostic
$sourceStats = @()   # per-source file/build stats

foreach ($src in $sourceList) {
    Write-Host "== Source: $($src.name) ($($src.kind)) ==" -ForegroundColor Cyan
    $srcPath = $src.path
    if (-not (Test-Path $srcPath)) {
        Write-Warning "Path not found, skipping: $srcPath"
        continue
    }

    $supportFiles = @()   # Designer.cs partials: not migrations themselves,
                          # but real projects always compile them alongside
                          # their sibling migration file, and the
                          # [Migration("id")] attribute BW028 looks for
                          # normally lives there (on the same partial class).
                          # Leaving them out would make BW028 fire on every
                          # single migration in the corpus for a reason that
                          # is an artifact of this tool's own file selection,
                          # not a real gap in the migration -- so they are
                          # included in the compile set for completeness but
                          # not counted as "migration files" and not treated
                          # as a source of BW001-027 findings.
    if ($src.kind -eq "synthetic") {
        $files = Get-ChildItem -Path $srcPath -Filter "*.cs" -Recurse
    }
    else {
        # "Migrations" is not a name EF Core owns: some repos have unrelated
        # folders with that name (CLI-tool migrations, a different migration
        # framework like YesSql/OrchardCore's IMigration/DataMigration). Only
        # a file whose body actually overrides MigrationBuilder is an EF Core
        # migration breakwater can analyze.
        $candidateDir = Get-ChildItem -Path $srcPath -Recurse -Filter "*.cs" |
            Where-Object { $_.FullName -match "[\\/]Migrations[\\/]" -and $_.Name -notmatch "ModelSnapshot\.cs$" }
        $files = $candidateDir | Where-Object {
            $_.Name -notmatch "\.Designer\.cs$" -and
            (Select-String -Path $_.FullName -Pattern "MigrationBuilder" -SimpleMatch -Quiet)
        }
        # A handful of migration files in large repos (e.g. bitwarden's
        # PostgresMigrations/MySqlMigrations) reference a sibling project's
        # namespace via `using` (helper base classes/extension methods that
        # live outside the Migrations folder, e.g. `Bit.Core`/`Bit.EfShared`)
        # that this tool never checks out. That is a CS0234 (namespace not
        # found) *error*, not a warning `NoWarn` can suppress, and a CS-level
        # error in even one file of the throwaway project stops the whole
        # compilation from producing any analyzer SARIF output at all (unlike
        # a resolvable-type CS0246, which the compile tolerates enough for
        # analyzers to still run — see NoWarn below). Excluding these few
        # files up front (counted separately, not silently dropped) lets the
        # other 150+ real migration files in the same source still get
        # scanned instead of the whole source silently producing zero
        # findings.
        $unresolvableUsingPattern = '^\s*using\s+Bit\.(Core|EfShared)(\.[\w]+)*\s*;'
        $excludedForUnresolvableUsing = $files | Where-Object {
            (Select-String -Path $_.FullName -Pattern $unresolvableUsingPattern -Quiet)
        }
        if ($excludedForUnresolvableUsing.Count -gt 0) {
            Write-Warning "  excluding $($excludedForUnresolvableUsing.Count) file(s) with an unresolvable cross-project `using` (not checked out by this tool): $($excludedForUnresolvableUsing.Name -join ', ')"
            $files = $files | Where-Object { $excludedForUnresolvableUsing -notcontains $_ }
        }
        # A Designer.cs is a support file only when its non-designer sibling
        # made it into $files (keeps unrelated Designer.cs noise, e.g. from a
        # non-EF migration folder, out of the compile set).
        $migrationBaseNames = $files | ForEach-Object { $_.Name -replace '\.cs$', '' } | Sort-Object -Unique
        $supportFiles = $candidateDir | Where-Object {
            $_.Name -match "\.Designer\.cs$" -and
            ($migrationBaseNames -contains ($_.Name -replace '\.Designer\.cs$', ''))
        }
    }

    if ($files.Count -eq 0) {
        Write-Warning "No migration files found under $srcPath"
        $sourceStats += [pscustomobject]@{ name = $src.name; kind = $src.kind; fileCount = 0; buildSucceeded = $null }
        continue
    }

    $work = Join-Path $workDir $src.name
    if (Test-Path $work) { Remove-Item $work -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    $codeDir = Join-Path $work "code"
    New-Item -ItemType Directory -Force -Path $codeDir | Out-Null

    # Copy files flat (renamed to avoid filename collisions) so a throwaway
    # project can include them without fighting the source repos' own project
    # structure. Different DbContexts across a big repo (e.g. abp's many
    # modules) commonly each have their own migration class named "Initial"
    # in the same namespace once flattened, which would be a real CS0111
    # duplicate-member error, not an analyzer concern -- give every file's
    # namespace a unique per-copy suffix so unrelated migrations never
    # collide as the same type. Class names are not referenced across files,
    # so this is safe and does not change what the analyzer sees per file.
    $copied = @{}
    $nsRegex = New-Object System.Text.RegularExpressions.Regex '(?m)^(\s*namespace\s+)([\w\.]+)'
    # Some repos (e.g. platformplatform) put `[DbContext(typeof(SomeDbContext))]`
    # on the migration class. The DbContext type lives outside the Migrations
    # folder this tool checks out, so it is a real CS0246 -- an error, not a
    # warning, so `NoWarn` cannot suppress it and it silently blocks the whole
    # compile from producing any analyzer SARIF output at all. The attribute is
    # irrelevant to every rule (only `[Migration("id")]`, handled separately
    # below, matters to BW028), so it is stripped instead of worked around.
    $dbContextAttrRegex = New-Object System.Text.RegularExpressions.Regex '(?m)^\s*\[DbContext\(typeof\([\w\.]+\)\)\]\s*\r?\n'
    $i = 0
    foreach ($f in $files) {
        $i++
        $suffix = "Bw{0:D4}" -f $i
        $dest = Join-Path $codeDir ("f{0:D4}_{1}" -f $i, $f.Name)
        $content = Get-Content $f.FullName -Raw
        $evaluator = { param($m) "$($m.Groups[1].Value)$($m.Groups[2].Value)_$suffix" }
        $content = $nsRegex.Replace($content, [System.Text.RegularExpressions.MatchEvaluator]$evaluator, 1)
        $content = $dbContextAttrRegex.Replace($content, '')
        Set-Content -Path $dest -Value $content -Encoding utf8 -NoNewline
        $copied[$dest] = $f.FullName

        # The real [Migration("id")] attribute lives on the Designer.cs
        # partial in a real project, which also drags in the project's own
        # DbContext type (not available to this throwaway compile). Rather
        # than compile the whole Designer.cs, lift just the attribute text
        # and splice it onto this copy's own class declaration -- same
        # observable effect (BW028 sees the attribute) without the
        # unrelated DbContext/using dependency chain.
        $designerSibling = $supportFiles | Where-Object { ($_.Name -replace '\.Designer\.cs$', '') -eq ($f.Name -replace '\.cs$', '') } | Select-Object -First 1
        if ($designerSibling) {
            $dcontent = Get-Content $designerSibling.FullName -Raw
            $attrMatch = [regex]::Match($dcontent, '\[Migration\("[^"]*"\)\]')
            if ($attrMatch.Success -and $content -notmatch [regex]::Escape($attrMatch.Value)) {
                $classRegex = New-Object System.Text.RegularExpressions.Regex '(?m)^(\s*)(public\s+partial\s+class\s)'
                $content = $classRegex.Replace($content, "`$1$($attrMatch.Value)`n`$1`$2", 1)
                Set-Content -Path $dest -Value $content -Encoding utf8 -NoNewline
            }
        }
    }

    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <!-- Some repos (e.g. platformplatform) rely on project-wide implicit
         usings (a GlobalUsings.cs this tool does not check out) and omit
         `using System;` from individual migration files, which otherwise
         fails even on BCL types like DateTimeOffset. -->
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <NoWarn>`$(NoWarn);CS0108;CS0114;CS0246;CS0103</NoWarn>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
  </PropertyGroup>
  <ItemGroup>
    <!-- Broad provider coverage: real-world migrations reference whichever
         provider's extension methods/annotations (IsNpgsql, IsMySql,
         SqlServer:Online, Npgsql:CreatedConcurrently, ...) their own project
         used. Referencing all four keeps as many files compiling clean as
         possible without pulling in each repo's full dependency graph. -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="8.0.8" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.4" />
    <PackageReference Include="Pomelo.EntityFrameworkCore.MySql" Version="8.0.2" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.8" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.8" />
    <PackageReference Include="Pgvector.EntityFrameworkCore" Version="0.2.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="$analyzerProj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
"@
    Set-Content -Path (Join-Path $work "CorpusScanTarget.csproj") -Value $csproj -Encoding utf8

    # breakwater_profile = strict so BW011/BW020/BW030 (strict-only) also
    # surface their raw finding counts for the precision review.
    Set-Content -Path (Join-Path $work ".editorconfig") -Value "root = true`n`n[*.cs]`nbreakwater_profile = strict`n" -Encoding utf8

    $sarif = Join-Path $work "scan.sarif"
    Push-Location $work
    # The semicolon in "ErrorLog=<path>;version=2" must survive MSBuild's own
    # property-list splitting; %3B is the reliable escape across both
    # PowerShell and a plain shell invocation (a bare ";" gets swallowed and
    # silently falls back to the legacy SARIF 1.0.0 schema, which the parser
    # below does not understand).
    $buildOutput = & dotnet build --nologo -v:q "-p:ErrorLog=$sarif%3Bversion=2" 2>&1
    $buildExit = $LASTEXITCODE
    Pop-Location

    $findingsThisSource = 0
    if (Test-Path $sarif) {
        $sarifJson = Get-Content $sarif -Raw | ConvertFrom-Json
        foreach ($run in $sarifJson.runs) {
            foreach ($result in $run.results) {
                if ($result.ruleId -match "^BW\d{3}$" -or $result.ruleId -eq "BW999") {
                    $loc = $null
                    if ($result.locations -and $result.locations.Count -gt 0) {
                        $loc = $result.locations[0].physicalLocation.artifactLocation.uri
                    }
                    $findingsThisSource++
                    $allFindings += [pscustomobject]@{
                        source    = $src.name
                        kind      = $src.kind
                        ruleId    = $result.ruleId
                        message   = $result.message.text
                        file      = $loc
                        line      = $result.locations[0].physicalLocation.region.startLine
                    }
                }
            }
        }
    }
    else {
        Write-Warning "No SARIF produced for $($src.name) (build exit $buildExit)"
    }

    Write-Host "  files=$($files.Count) findings=$findingsThisSource buildExit=$buildExit"
    $sourceStats += [pscustomobject]@{
        name = $src.name; kind = $src.kind; fileCount = $files.Count
        buildExit = $buildExit; findings = $findingsThisSource
    }
}

# ---- Aggregate per rule ----
$byRule = $allFindings | Group-Object ruleId | ForEach-Object {
    [pscustomobject]@{
        ruleId = $_.Name
        count  = $_.Count
        sample = ($_.Group | Select-Object -First 50)
    }
} | Sort-Object ruleId

$totalOperationsNote = "operation count is approximated as total BW-relevant finding count is NOT operations; see corpus-scan-summary for reviewer notes"

$result = [pscustomobject]@{
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    sources      = $sourceStats
    totalFindings = $allFindings.Count
    byRule       = $byRule
}

$resultPath = Join-Path $OutDir "corpus-scan.json"
$result | ConvertTo-Json -Depth 8 | Set-Content -Path $resultPath -Encoding utf8
Write-Host "Wrote $resultPath" -ForegroundColor Green

# Flat CSV of every finding, for hand review.
$csvPath = Join-Path $OutDir "corpus-scan-findings.csv"
$allFindings | Export-Csv -Path $csvPath -NoTypeInformation -Encoding utf8
Write-Host "Wrote $csvPath" -ForegroundColor Green
