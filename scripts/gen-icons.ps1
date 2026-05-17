<#
.SYNOPSIS
  Regenerate the committed icon PNG ladders (16..512; 128 px is the
  packed NuGet icon). Reproducible; run after changing an icon source,
  then commit the regenerated PNGs.

  org mark : assets/Brown Owl.png (owner's finished raster design)
             composited on the brand tile (build/TileIcon) — NOT packed;
             it is the Wise Owl Software org-profile mark.
  package  : SVG lettermarks — WiseOwl.Casc "CASC" (build/Lettermark)
             and WiseOwl.Casc.Diablo4 "D·IV" — rasterised by IconGen.
.EXAMPLE
  pwsh scripts/gen-icons.ps1
#>
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

dotnet run --project build/TileIcon   -c Release -- "assets/Brown Owl.png" "assets/icons/wiseowl-org"
dotnet run --project build/Lettermark -c Release -- "assets/icons/wiseowl-casc.svg"
dotnet run --project build/IconGen    -c Release -- assets/icons @($args)[0]
Write-Host 'Icons regenerated under assets/icons/.'
