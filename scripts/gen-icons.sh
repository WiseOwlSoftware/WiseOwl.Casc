#!/usr/bin/env bash
# Rasterise assets/icons/*.svg → the committed PNG size ladder
# (16/32/48/64/128/256/512). The SVGs are the source of truth; the PNGs
# (incl. the 128 px packed NuGet icon) are generated artefacts. Run after
# editing an SVG, then commit the regenerated PNGs.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project build/IconGen -c Release -- assets/icons "${1:-}"
echo "Icons regenerated under assets/icons/."
