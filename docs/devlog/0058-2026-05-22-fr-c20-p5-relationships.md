# 0058 — FR-C20 P5: authored relationship traversal

2026-05-22 · CL-60 · branch `fr-c20-relationships-p5`

## Consensus

The Optimizer's answers on the two open questions: **(a) power→class — don't RE
now** (no consumer need; powers are reached by SNO lookup from a node's
`SnoPassivePower`, never filtered by class), **(b) P5 relationships — build
next** (it assembles board→node→glyph→affix→power by hand today). P5 is the last
consensus item.

## Built

`Catalog.Related(AssetRef) → IReadOnlyList<AssetLink>`, `AssetLink(string Role,
AssetRef Target)`. Each `Target` is a full `AssetRef`, so traversal chains
(`Related` the target again). Authored FK edges, straight from the decoded
definitions:

- `ParagonBoard` → `node` — `ParagonBoardDefinition.Cells` (distinct, non-empty).
- `ParagonNode` → `power` — `SnoPassivePower` (the legendary-node passive).
- `ParagonGlyph` → `affix` (`AffixSnoIds`) + `class` (`UsableByClassSnoIds`).

## Decisions / honesty

- **Node ↔ glyph is not a link.** A socket node accepts a glyph at *runtime*
  (player slotting); there's no authored node→glyph FK. Surfacing one would be
  fabrication. The consumer finds candidate glyphs for a class via
  `FindByFacet(ParagonGlyph, "class", …)` instead. The Optimizer's
  "node → glyph" chain step is therefore runtime, not catalogued.
- **`affix → power` doesn't exist.** `ParagonGlyphAffix` is a stat modifier
  (`AffectedRarity`/`Operation`/`Base`/`PerLevel`) — a leaf, no power FK. The
  "→ power" at the end of the chain is really the separate **node → power**
  (legendary passive) edge.
- **Sentinel guard.** `SnoPassivePower` uses a **negative** sentinel for "none"
  (not 0) — caught by a test (a node yielded a `power` link with `Sno = -1`).
  Fixed: all relationship targets filter SNO id **> 0**.

## Acceptance

`Catalog_discovers_and_retrieves_assets_by_kind_filter` extended: a board's
`Related` are all `ParagonNode`; some node chains `→ power` (`Power`, Sno > 0); a
glyph chains `→ affix` (`ParagonGlyphAffix`) + `→ class` (`PlayerClass`). 51/51
Diablo4 tests green on `3.0.2.71886`.

## State

This **closes the FR-C20 consensus backlog**: P1/P2/P3/P4/Q2/Q4/P2b/P5 all
shipped and consume-verified. Deferred (no consumer need): power→class facet
(needs a skill-kit RE), item `NameConvention` facets, the non-BC1/BC3 codec
tail, and the atlas GUI app.
