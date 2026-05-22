# 0061 — FR-C19 #30: the authored node-selection recipe (ContextualHighlight_Square)

2026-05-22 · CL-64 · branch `fr-c19-node-selection-recipe`

## Trigger

Owner, twice, firmly: *"you should be able to data mine and find a recipe that
does this drawing"* and *"I did not ask you to invent a recipe. I said find the
existing recipe that matches."* I'd been reconstructing/guessing the 9-slice
composition (CL-62 wrong recipe, CL-63 "unrecoverable") instead of finding the
authored recipe.

## The find

Searching the UI-style group for the hover highlight: **`ContextualHighlight_Square`**
(TiledStyle 2434982) — the engine's named "square contextual (hover) highlight."
It is a `TiledWindowPieces` record with **exactly 4 piece handles** (`+0x60..+0x6C`),
then zeros, `ImageScale` 0.5. So the authored recipe is **4 corners, no edges, no
centre** — exactly the owner's "four corners surround the node square."

Its own 4 handles (`0xB320888F`, `0x17A06222`, `0xCFED8744`, `0x91EC9171`) are
**engine-internal** — resolve to no texture frame (new `SnoScan findhandle`
scanned all 140,197 group-44 textures: not found), like the #24 fire-rim.

## The pairing (owner-approved option a)

Surface `ContextualHighlight_Square` as the recipe, paired with the **drawable
corner art** from `SelectionRectangleInset` (585031)'s window-pieces — the 4
corners of `2DUITiled_SelectionHighlight` (585030), whose roles I verified by
decoding + viewing each piece:

```
TL 0x95DA4E78   TR 0x5192E52B   BR 0xEA71A5AD   BL 0xB1C206BA
```

Compositing these four, each in one quadrant of the node square, produces the
clean hollow orange/white border matching the owner oracle (validated via
`AtlasExport compose … c4`).

## API

`Diablo4Storage.ReadNodeSelectionHighlight()` → `NodeSelectionHighlight(int
RecipeSno, string RecipeName, uint TopLeft, uint TopRight, uint BottomRight,
uint BottomLeft)` (+ `Corners`, `IsEmpty`). Drawing recipe: hollow square border
sized to the node perimeter — 4 corners only, each in its quadrant, no
edges/centre/fill.

## Where I went wrong before (so it's recorded)

- CL-62 asserted a row-major `[TL,T,TR,L,C,R,BL,B,BR]` mapping and a
  corners+edges+centre placement — the mapping was wrong (it's corners-CW then
  centre then edges) and I drew edges/centre, producing overlap.
- CL-63 over-corrected to "composition unrecoverable / engine-side." Wrong: the
  pieces are clean corner/edge brackets; the composition is recoverable, and the
  *named authored recipe* (`ContextualHighlight_Square`) existed all along — I
  hadn't searched for it.

## Acceptance

`ReadNodeSelectionHighlight_pairs_recipe_with_corner_art` — recipe name/SNO
resolve; the 4 verified corner handles; each resolves to a real frame. 53/53
Diablo4 tests green on `3.0.2.71886`.
