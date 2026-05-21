# Devlog 0051 — FR-C16 R14: flat recipe + bActive-driven activation, owner-oracle validated

*2026-05-21*

Owner-directed reframe: flatten the node recipe to a single z-ordered list
of atomic components the consumer draws verbatim (draw all whose activation
holds), and follow the *authored data* (`bActive`, `rgbaTint`, anchoring,
all widget children) rather than invented activations. A long visual-QA loop
against the owner's in-game screenshots corrected several interpretations —
the discipline was: use each correction to find *why* the recipe-following
was wrong, never to patch the drawing.

## Surface (CL-52)

`ParagonNodeRecipe.Components : ParagonNodeComponent[]`, each
`(ZOrder, Source, ImageHandle, Rect, Alpha, Activation, DefaultActive, Tint)`.
Removed the CL-50/51 `ParagonNodeRecipeLayer`/`NodeDiscLayer`/
`NodeSelectionDiscs`/`NodeSlot`/`CompositeLayers` nesting.

## Interpretation corrections (each from an owner-oracle observation)

- Emit **every** widget's drawable layers (own `hImageFrame` + handle
  children), not just `Template_Node_*` → recovers the glyph-socket base
  (`0xF6443089`, grey-tinted) + overlay in `Usage_Slot_2`.
- **`bActive` = default visibility.** `bActive=0` layers are default-off; one
  that would fire at rest (no decoded trigger) → `Never` (the magic interior
  `0xFEC31E48` was masking the disc).
- **Base disc = Unpurchased↔Purchased**, not unselected/selected. `bActive=1`
  no-ring disc = unpurchased; `bActive=0` red-ring/brighter disc = purchased.
  `Node_Purchased`'s name was literally right; my "selected" reinterpretation
  was the bug. New `NodeFact.Unpurchased`.
- **Node kind = one mutually-exclusive dimension** (Common peer to
  Magic/Rare/Legendary; Socket/Gate/Start in the same enum — engine
  `Play_UI_Menu_Paragon_Purchase_Node_*`).
- **Purchased add-on**: arrows → purchasable neighbour, connectors →
  *purchased* neighbour (new `NeighbourPurchased[dir]` facts); both gated on
  the node Purchased. Gate/start draw no arrows (no purchasable neighbours —
  consumer fact logic; the gate's arrow is drawn by its *neighbour*).
- **`rgbaTint`** (multiply) + **anchoring** (`eVerticalAnchoring` 3=centre /
  0=absolute) now applied → the 120² Located ring centres correctly.
- **GlyphNodeGlow** red ring gated to `KindSocket` (was drawing on every
  purchased node).
- **Selection highlight** (orange-fuzz + white square) is **not** in scene
  657304 — a shared engine-applied topmost cursor (lead:
  `ContextualHighlight_Square` TiledStyle 2434982, source `0xB320888F`). Spun
  off as its own FR.

## Hash sanity check (new tool)

`build/SnoScan checkfields` recomputes every `KnownFieldNames`/`KnownTypeNames`
hash from its symbol and flags mismatches. Caught exactly one: `0x093CBAA8`
mislabelled `eGroupType` — it's `eHorizontalAnchoring` (real
`FieldHash("eGroupType")`=`0x05862894`). That mislabel was the root cause of
the Located-ring mis-placement. Dictionary corrected.

## Validation

A `build/RecipeRender` tool interprets the shipped recipe into a labelled
kind×state grid (no consumer logic) — used throughout the QA loop. 50/50
Diablo4 tests green; hash dictionary clean (54 verified, 0 mismatch).
