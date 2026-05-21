# 0052 — FR-C19: selection highlight is authored as TiledStyle recipes

2026-05-21 · CL-53 · branch `fr-c19-selection-highlight-api`

## Trigger

Owner question on the in-progress `ReadSelectionHighlight()` accessor:
*"Shouldn't the game already have a recipe for drawing the highlight? Why not
just return that recipe like we do for other drawings?"*

The accessor I had started returned a bespoke flat inventory — small "border
slices" + large "shape frames" — and the consumer would have had to assemble
the 9-slice itself. That is consumer dispatch, which the owner's standing rule
forbids. The question was the right one.

## Finding

The engine **does** author the selection highlight as recipes — as
`TiledStyle` (group 103) 9-slice records, the same mechanism we already expose
via `ReadTiledStyle` / `TiledStyleDefinition`.

`find SelectionHighlight` →
- group 44 (Texture): `2DUI_SelectionHighlight` (337357),
  `2DUITiled_SelectionHighlight` (585030), `…Expertise` (1284518).
- group 103 (TiledStyle): two `Reliquary*_SelectionHighlight`.

A new reverse-index recon command — `SnoScan stylefor <atlasSno>` (scan all
group-103 TiledStyles, resolve each `SourceImageHandle` to its atlas, match) —
pinned the recipes that compose the selection atlases:

```
atlas 585030 (tiled):   585031  SelectionRectangleInset        (partial)
atlas 337357 (frames):  478960  ControllerSelectionRectangle   (partial)
                        478961  ControllerSelectionCircle
                       1298996  ControllerSelectionDiamond
                       2314766  ControllerSelectionTearDrop
                       2434945  ControllerSelectionAPS
```

So the square node selection is `SelectionRectangleInset` (inset 9-slice over
the purpose-built tiled sheet), and the per-silhouette variants are the
`ControllerSelection*` recipes.

## Correction it forced

The FR-C19 *info* delivery had labelled the large frames by eyeballed
geometry: `0xBA7D2638` "circle (216²)", `0x0BD8A829` "diamond (179²)". The
authored TiledStyle names say otherwise — `0xBA7D2638` is the source of
`ControllerSelectionTearDrop`, `0x0BD8A829` is `ControllerSelectionCircle`.
Textbook `no-atlas-name-jumps` / `widget-name-not-role`: never assert a role
from atlas-frame size. The typed API now derives `SelectionShape` from the
engine's own recipe name, never from geometry.

## API

`ReadSelectionHighlight()` ⇒ `SelectionHighlight(IReadOnlyList<…> Styles)`,
each `SelectionHighlightStyle(int TiledStyleSno, string Name,
SelectionShape Shape, uint SourceImageHandle, int AtlasSno)`. The consumer
picks the style matching the node's silhouette and decodes it through the
existing `ReadTiledStyle` 9-slice path — no composition or shape guessing on
its side. Resolution is name-/reference-driven (atlas resolved by name, styles
discovered by which atlas they compose), so it is robust to id churn.

`SelectionRectangleInset` and `ControllerSelectionRectangle` decode
`partial=True` (the TiledStyle variant-suffix partial-decode disclosed in
FR-C14 R9/R10 — `HasPartialDecode`); `SourceImageHandle`, padding and the
9-slice nature are intact.

## Acceptance

`ReadSelectionHighlight_surfaces_authored_tiledstyle_recipes` (asserts the
named recipes, the corrected shape→handle mapping, and that every surfaced
style resolves to a decodable TiledStyle). 51/51 Diablo4 tests green on live
build `3.0.2.71886`.

## Follow-up

FR-C19 #30 needs a `[CASC]` correction comment: the typed accessor shipped,
and the earlier shape labels were wrong (teardrop/circle swap) — use the
authored TiledStyle names.
