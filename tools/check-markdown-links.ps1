<#
.SYNOPSIS
    Checks that every relative (non-http) link and image reference in
    docs/*.md, docs/rules/*.md and README.md resolves to a real file.
    Used by the `docs` GitHub Actions workflow; no third-party action.

.DESCRIPTION
    Scans Markdown link syntax `[text](target)` and image syntax
    `![alt](target)`. A target is skipped (not checked) when it:
      - starts with a scheme (http://, https://, mailto:, etc.)
      - is a bare in-page anchor (#section)
    Anything else is resolved relative to the Markdown file's own
    directory and must exist on disk. A `#fragment` suffix on a relative
    link is stripped before the file-existence check (fragment targets
    inside the linked file are not verified).

.EXAMPLE
    pwsh tools/check-markdown-links.ps1
#>
param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot/..")
)

$ErrorActionPreference = "Stop"

$files = @(Get-ChildItem -Path $RepoRoot -Filter "*.md" -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj|_work)[\\/]' })

$linkPattern = '(?<!\!)\[[^\]]*\]\(([^)]+)\)|!\[[^\]]*\]\(([^)]+)\)'
$broken = @()
$checked = 0

foreach ($file in $files) {
    $text = Get-Content -Path $file.FullName -Raw
    $matches = [regex]::Matches($text, $linkPattern)
    foreach ($m in $matches) {
        $target = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
        $target = $target.Trim()

        if ($target -eq "") { continue }
        if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') { continue }  # scheme, e.g. https:, mailto:
        if ($target.StartsWith("#")) { continue }                     # in-page anchor

        $withoutFragment = ($target -split '#')[0]
        if ($withoutFragment -eq "") { continue }  # e.g. "./#top", nothing to resolve

        $checked++
        $resolved = Join-Path $file.DirectoryName $withoutFragment
        if (-not (Test-Path $resolved)) {
            $broken += [pscustomobject]@{
                file   = $file.FullName.Substring($RepoRoot.Path.Length + 1)
                target = $target
            }
        }
    }
}

Write-Host "Checked $checked relative link(s) across $($files.Count) Markdown file(s)."

if ($broken.Count -gt 0) {
    Write-Host "Broken links:" -ForegroundColor Red
    foreach ($b in $broken) {
        Write-Host "  $($b.file): $($b.target)" -ForegroundColor Red
    }
    exit 1
}

Write-Host "All relative links resolve." -ForegroundColor Green
exit 0
