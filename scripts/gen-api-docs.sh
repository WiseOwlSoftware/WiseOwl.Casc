#!/usr/bin/env bash
# Regenerate the committed API reference under docs/api/ from the XML doc
# comments (the source of truth). Idempotent; --clean removes stale pages so
# the tree never drifts from the public surface. CI runs this and fails on
# any diff (see .github/workflows/ci.yml).
#
#   scripts/gen-api-docs.sh
#
set -euo pipefail
cd "$(dirname "$0")/.."

SRC="https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main"

dotnet tool restore
dotnet build WiseOwl.Casc.slnx -c Release --nologo -v q

# Generate from the netstandard2.0 build: the public surface is identical
# across all TFMs, and the ns2.0 facade resolves cleanly under the doc tool.
gen() {
  local proj="$1" pkg="$2"
  dotnet xmldocmd \
    "src/$proj/bin/Release/netstandard2.0/$pkg.dll" \
    "docs/api/$pkg" \
    --visibility public --clean --source "$SRC"
}

gen WiseOwl.Casc          WiseOwl.Casc
gen WiseOwl.Casc.Diablo4  WiseOwl.Casc.Diablo4

echo "API docs regenerated under docs/api/."
