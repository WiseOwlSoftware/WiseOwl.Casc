# 0036 — FR-C14: engine-actual background canvas (CL-38)

*2026-05-20*

FR-C14 R1 ([casc-fr#24](https://github.com/WiseOwlSoftware/casc-fr/issues/24))
asks: why does the consumer-rendered `ParagonBoardChrome.BackgroundCenter`
(`0x2954DF0C`) show wide horizontal wood-plank bands across the
board field that the game never shows? Three hypotheses from R1:
specific placement / scaling, engine post-processing, or a different
atlas frame is the actual on-screen field.

**Hypothesis 3 confirmed.** The engine renders a *different* atlas
than the scene-bound chrome.

## The smoking gun

Two atlases in the texture catalog whose names suggest paragon
backgrounds:

| SNO | Name | Dimensions | Codec | Frames | Handle |
|---|---|---|---|---|---|
| 447106 | `2DUI_Paragon` | 1200×1200 | BC1 | many (incl. `0x2954DF0C` 1200²) | per-frame |
| **1447773** | **`2DUI_ParagonBackground`** | **2400×1200** | BC1 | 1 full-blob | **`ImageHandle = 0`** |

The scene 657304 widget `Template_Board_Background_Center` binds
handle `0x2954DF0C` (= the wood-plank frame in atlas 447106). The
1447773 atlas is NOT bound by any widget in scene 657304 — the
engine references it directly by SNO id.

## Visual confirmation

Extracted both atlases to PNG via `TextureDefinition.DecodeMip0` +
ImageSharp (artifacts in `e:/tmp/scene-probe/bin/Debug/net10.0/fr-c14-*.png`):

- **`0x2954DF0C` / 2DUI_Paragon @ 1200×1200**: clearly visible
  horizontal wood-plank bands.
- **`2DUI_ParagonBackground` @ 2400×1200**: smooth organic red-black
  hellish field with no bands. This IS what the game shows.

The wider 2400-pixel canvas (vs the 1920-canvas board field) hints
the engine canvas covers the entire viewing area including rim
extension.

## Why ImageHandle = 0 matters

`TexFrame.ImageHandle` is the catalog key that scene-widget bindings
look up. The `2DUI_ParagonBackground` atlas has exactly one frame
with `ImageHandle = 0`, so it's effectively a "lookup-keyless" full
texture rather than a sliced atlas. This means:

- Scene-widget bindings (which carry a 32-bit handle) cannot reference
  it by handle.
- The engine must select it by SNO id directly (or by name).
- CASC's normal handle→atlas resolution path doesn't reach it
  (`Diablo4Storage.IsParagonTextureHandle(0)` returns false by the
  magnitude guard `>= 0x10000`).

This is structurally similar to FR-C11 R3 §3's rim-fire animation
("engine-internal, no scene widget binds the atlas frames") and the
CL-31→32 board-rim sides ("scene-bound but resolve through a
non-icon-catalog path"). Both required surfacing a non-handle-keyed
texture reference. CL-38 does the same — surfaces the SNO id +
dimensions so the consumer can read the payload directly.

## API surface

```csharp
public sealed record ParagonBoardChrome(
    NodeElement BackgroundCenter,        // scene-bound 0x2954DF0C (audit; not engine-rendered)
    NodeElement BorderTop,
    NodeElement BorderRight,
    NodeElement BorderBottom,
    NodeElement BorderLeft,
    IReadOnlyList<NodeElement> BoardSelectChrome,
    int EngineBackgroundCanvasSno,       // NEW: 1447773 (2DUI_ParagonBackground)
    int EngineBackgroundCanvasWidth,     // NEW: 2400
    int EngineBackgroundCanvasHeight);   // NEW: 1200
```

Resolved by `Diablo4Storage.ReadParagonRenderModel` via
`CoreToc.TryGetId(SnoGroup.Texture, "2DUI_ParagonBackground", out
var sno)` — robust to a future season that renames or reassigns
the SNO id, since the name-keyed lookup is canonical. If the lookup
fails (atlas absent), all three fields are 0 (honest sentinel).

## Why both BackgroundCenter and EngineBackgroundCanvas

The scene-bound `BackgroundCenter` is preserved per the no-drop /
no-fabrication discipline (CL-26 / CL-27 / CL-30 / CL-31 / CL-32 /
CL-34 / CL-35). The handle IS in the scene; surfacing only the
engine canvas would drop a real decoded fact. Future RE may discover
an engine state (e.g. a specific paragon view mode) where
`0x2954DF0C` IS the rendered art — keeping it surfaced avoids that
regression risk.

Consumer guidance: compose against `EngineBackgroundCanvasSno` for
default rendering parity with the game; the
`BackgroundCenter.TextureHandle` is for audit / state-overlay paths
the FR-C14 acceptance doesn't currently exercise.

## Acceptance

`ReadParagonBoardChrome_surfaces_scene_bound_chrome` extended with:

```csharp
Assert.Equal(1447773, chrome.EngineBackgroundCanvasSno);
Assert.Equal(2400, chrome.EngineBackgroundCanvasWidth);
Assert.Equal(1200, chrome.EngineBackgroundCanvasHeight);
```

40 / 40 tests green on `D:\Diablo IV` build `3.0.2.71886`.

## Deliverable

- `src/WiseOwl.Casc.Diablo4/ParagonRenderLayout.cs`: 3 new fields on
  `ParagonBoardChrome`; `BoardChrome()` projection signature extended.
- `src/WiseOwl.Casc.Diablo4/Diablo4Storage.cs`: SNO-name resolution
  in `ReadParagonRenderModel`.
- `tests/.../Diablo4StorageIntegrationTests.cs`: anchor on the 3
  new field values.
- `docs/casc-diablo4-format.md` §10.16 narrative + CL-38 appendix.
- 40 / 40 tests green.
- PR forthcoming.

## Boundary preserved

FR-C7 §6: library = complete faithful decode + no-drop / no-phantom
discipline; consumer = compositing. CL-38 extends "complete" to
include engine-internal atlas references that aren't scene-bound
via the standard handle path — surfaced via SNO id alongside the
existing handle-based scene bindings. The consumer composes per
the recipe + the engine canvas; CASC neither fabricates an
animation timeline (rim fire) nor an alternate-state mapping (the
preserved `BackgroundCenter` handle stays as decoded).
