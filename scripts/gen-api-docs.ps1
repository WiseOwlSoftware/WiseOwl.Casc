<#
.SYNOPSIS
  Regenerate the committed API reference under docs/api/ from the XML doc
  comments (the source of truth). Idempotent; -clean removes stale pages so
  the tree never drifts. CI runs this and fails on any diff.
.EXAMPLE
  pwsh scripts/gen-api-docs.ps1
#>
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$src = 'https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main'

dotnet tool restore
dotnet build WiseOwl.Casc.slnx -c Release --nologo -v q

function Gen([string]$proj, [string]$pkg) {
  # The public surface is identical across all TFMs; the ns2.0 facade
  # resolves cleanly under the doc tool.
  dotnet xmldocmd `
    "src/$proj/bin/Release/netstandard2.0/$pkg.dll" `
    "docs/api/$pkg" `
    --visibility public --clean --source $src
}

Gen 'WiseOwl.Casc'          'WiseOwl.Casc'
Gen 'WiseOwl.Casc.Diablo4'  'WiseOwl.Casc.Diablo4'

Write-Host 'API docs regenerated under docs/api/.'
