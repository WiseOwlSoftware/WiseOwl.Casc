#!/usr/bin/env bash
# Regenerate the committed icon PNG ladders (16/32/48/64/128/256/512;
# 128 px is the packed NuGet icon). Reproducible; run after changing an
# icon source, then commit the regenerated PNGs.
#
#   org mark : the owner's finished raster design assets/Brown Owl.png,
#              composited on the brand tile (build/TileIcon). NOT packed —
#              it is the Wise Owl Software org-profile mark.
#   package  : SVG lettermarks — WiseOwl.Casc "CASC" (build/Lettermark),
#              WiseOwl.Casc.Diablo4 "D·IV" — rasterised by build/IconGen.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet run --project build/TileIcon   -c Release -- "assets/Brown Owl.png" "assets/icons/wiseowl-org"
dotnet run --project build/Lettermark -c Release -- "assets/icons/wiseowl-casc.svg"
dotnet run --project build/IconGen    -c Release -- assets/icons "${1:-}"
echo "Icons regenerated under assets/icons/."
