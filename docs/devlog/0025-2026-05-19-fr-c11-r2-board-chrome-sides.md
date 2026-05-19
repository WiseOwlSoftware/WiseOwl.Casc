# 0025 — FR-C11 R2: board chrome is a 5-piece composite; rim sides surfaced

*2026-05-19*

R1's CL-31 surfaced only the centre background widget for scene
657304 (`Template_Board_Background_Center → 0x2954DF0C`) and reported
the rim's animated fire as engine-internal. R2 root-causes that CL-31
missed the **four cardinal-side rim widgets** the scene actually
authors — a real omission, not just a documentation gap. CL-32
corrects the chrome to the 5-piece composite the scene records
describe and surfaces the rim handles honestly.

## What R2 settled

Deep scene-657304 widget probe (every widget with "vfx", "border",
"rim", "ember", "canvas", "vignette", "mist", "storyboard", or
"background" in its name; every widget binding any of the FR-C11 R1
candidate handles) turned up four widgets I'd dismissed in CL-31:

| Widget | Field | Bound handle |
|---|---|---|
| `Template_Board_Background_Top` | `0x0C152636` (type `0x6B1C5D9C`) | `0x900C7D87` |
| `Template_Board_Background_Bottom` | same | `0x900C7D87` |
| `Template_Board_Background_Left` | same | `0x225F2DA8` |
| `Template_Board_Background_Right` | same | `0x225F2DA8` |

The scene authors a 5-piece chrome: one centre widget + four cardinal
sides. Top/Bottom share one band texture, Left/Right share another.
**No corner widgets exist** — it is a 4-side band composite, not a
9-slice. All five widgets carry `Rect = default` (no authored
sub-rect): the engine positions them at native pixel size.

The two rim handles do **not** resolve via the existing
`Diablo4Storage.TryGetIconFrame` icon-catalog index (which maps the
multi-frame atlas frames from the `0x44CF00F5` combined-meta bundle).
Hashing the obvious texture-name candidates (`ui_paragon_glowLine`,
`ui_paragon_borderTop`, `Template_Board_Background_Top`, etc.) with
`Diablo4.GbidHash` / `TypeHash` / `FieldHash` did not produce a
match for either rim handle — so the resolution path is something
CASC does not yet index. Surfacing the bound handle as-is (with
`AtlasSno = 0` / native px `0`) is the honest disposition; a future
FR can add the non-icon-catalog index if the consumer needs CASC to
hand them the rim texture data.

The FR-C11 R1 "ember candidate" handles (`0x6CFA1668`, `0x749F8139`,
`0xAA7571AB`) remain not scene-bound to any board-chrome widget;
`0xB5C007F8` is bound to `Template_GlyphAura_Tile` (glyph aura);
`0xC1473C21` is bound to `Common_Node_BG_Black` / `_Revealed`
(per-node BG). The likely re-interpretation: the engine's "fire rim"
animation is a renderer effect on the scene-bound side bands above,
not a frame-swap of the ember candidates. CASC stays out of the
animation attribution — scene data has no blend mode, frame order,
or timing for any of this.

## CL-32 — what shipped

1. **`ParagonBoardChrome` reshaped** to a 5-piece composite (no
   NuGet release carried the old CL-31 shape, so this is a clean
   pre-1.0-alpha rename + extension):
   - `BackgroundCenter` (was `MainBoardBackground`).
   - `BorderTop`, `BorderRight`, `BorderBottom`, `BorderLeft` (new).
   - `BoardSelectChrome` unchanged.

2. **`ParagonRenderProjection.BoardChrome`** updated to find the 4
   side widgets by name and surface their scene-bound handles via a
   new helper that bypasses the icon-catalog filter (`SceneBinding`)
   — the catalog-resolvable path (`CatalogBinding`) stays in place
   for `BackgroundCenter` and the board-select layers.

3. **Acceptance test** `ReadParagonBoardChrome_surfaces_scene_bound_chrome`
   updated to assert the 5-piece composite shape:
   - `BackgroundCenter` is catalog-resolvable (handle / atlas SNO /
     native px present).
   - Each rim side has its expected handle (Top = Bottom =
     `0x900C7D87`; Left = Right = `0x225F2DA8`), `AtlasSno = 0`,
     `NativeWidth = 0`, `NativeHeight = 0`.
   - No fire-border R1 catalog handle leaks into the typed model.

4. **Scene-bind gate** `ReadParagonBoardChrome_layers_are_scene_bound`
   extended to cover the 4 rim sides — they don't appear in the
   icon-catalog-filtered `Scenes` view, so the gate cross-references
   them against the raw scene-657304 widget data via `ReadUiScene`.

5. **Spec §10.16** rewritten to the 5-piece composite + the
   non-icon-catalog rim-handle disclosure. Appendix A CL-31 amended
   (initial-shape note + "corrected by CL-32") and CL-32 added.

Acceptance (live `3.0.2.71886`): 36/36 Diablo4 integration tests
green.

## §3 — record-sourced answers (CL-28-grade)

- **Rim geometry:** 4-cardinal-side band composite, not 9-slice. Top
  and Bottom share one band handle; Left and Right share another. No
  corner widgets. No authored sub-rect on any side — the engine
  positions each side at native pixel size along its edge.
- **Blend / emissive mode:** not authored in scene data. The rim
  widgets carry the texture-handle field and nothing else relevant —
  the engine's renderer applies the animated fire look on top.
- **Animation cycle:** not authored. Engine-internal.

The R1 acceptance allowed "engine-driven say so with CL-28-grade
rigour + the cycle so the consumer can reproduce". CL-32 delivers
the cycle insofar as scene data authors it (the 4-side geometry);
the blend mode and animation cadence remain genuinely outside
scene-data scope — the engine renderer is the source. The consumer
either provides a texture-resolution path for the rim handles and
applies a procedural / shader animation on top, or keeps the
existing procedural orange border.

## Why CL-31 missed this

CL-31's BoardChrome projection filtered every candidate field
through `IsParagonTextureHandle`, which requires the handle to
resolve via the icon catalog. The 4 rim side widgets bind
catalog-unresolvable handles, so they were silently filtered out
of the projection — exactly the CL-29-class regression the gate
discipline is supposed to prevent. The gate I shipped in CL-31
cross-referenced against the icon-catalog-filtered `Scenes` view,
which has the same filter, so it didn't catch the omission. CL-32
fixes the projection (with a separate scene-binding helper that
doesn't filter by icon-catalog resolvability for the rim sides)
and extends the gate to assert via raw scene-657304 widget data
for those sides.
