<#
.SYNOPSIS
  Rasterise assets/icons/*.svg → the committed PNG size ladder
  (16/32/48/64/128/256/512). SVGs are the source of truth; PNGs (incl. the
  128 px packed NuGet icon) are generated. Run after editing an SVG.
.EXAMPLE
  pwsh scripts/gen-icons.ps1            # all
  pwsh scripts/gen-icons.ps1 wiseowl-casc
#>
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet run --project build/IconGen -c Release -- assets/icons @($args)[0]
Write-Host 'Icons regenerated under assets/icons/.'
