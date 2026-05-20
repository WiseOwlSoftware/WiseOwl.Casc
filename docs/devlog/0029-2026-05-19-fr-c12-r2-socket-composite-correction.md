# 0029 — FR-C12 R2: socket composite correction + row-completeness gate (CL-34)

*2026-05-19*

CL-33 §1's "the bead ring is the complete socket scene-bound art"
stood for the bead-ring axis but **was incomplete for the composite**.
The owner's R2 reopen flagged that the in-game socket node has
more than the two layers §7.2 carried (`Node_IconBase` +
`GlyphNodeGlow_Revealed`), and the broad probe + owner atlas-frame
oracle confirms: the socket draws three concentric atlas frames in
`2DUI_Paragon_transparentElements` plus the shared rarity-base disc.
The CL-31 → CL-32 lesson applies to the socket axis: the
binding-widget naming filter the prior probe used (`Glyph` /
`Socket` / `Ring` / `Pulse`) was too narrow, and the missing layers
turned out to be scene-bound under a non-socket-named widget
(`Usage_Slot_2`, the right-side equipped-glyph panel).

## What the broad probe surfaced

Re-probed scene 657304 with **no widget-name filter**, **no
icon-catalog filter**, and a histogram of every field TypeHash to
rule out a non-`DT_HANDLE` binding shape carrying the missing
layers. Inputs: `e:/tmp/scene-probe/Program.cs` (FR-C12 R2). Scene
has 211 widgets; 86 bind ≥1 atlas handle. Only TypeHash `0x6B1C5D9C`
(`DT_HANDLE`) carries atlas references (47 fields across the scene);
no other field-type binds textures.

The socket-composite atlas handles (per owner atlas-frame oracle
and CASC's own frame extraction — `socket-composite-stack.png`):

| Handle | Atlas | Native px | Role |
|---|---|---|---|
| `0x1D166DC7` | `2DUI_Paragon_transparentElements` (2061536) | 154² | Shared per-node grey-base disc (rarity-base; behind all socket art) |
| `0xF6443089` | same | 135² | Ornate outer socket disk (black frame + red gem inset, center opening) |
| `0xBED4CF21` | same | 135² | Red glowing bead ring — pulsing animation layer |
| `0x23F487F3` | same | 136² | Inner spike-frame with center depression — the per-node `HIconMask` glyph icon seats here |

`0xF6443089` and `0x23F487F3` are bound in scene 657304 on the
`Usage_Slot_2` widget's 0x58-block; `0xBED4CF21` is bound on
`GlyphNodeGlow_Revealed`'s `DT_HANDLE` field (and again on
`GlyphNodeGlow_Purchased` for the socketed state). The engine reuses
the same atlas frames for both the right-side equipped-glyph
side-panel and the on-board per-node socket render — surfacing the
layers requires checking *every* widget's bindings, not just the
ones whose names match per-node tokens.

## Verifying the recipe

CASC's own frame extraction (probe writes the four PNGs +
composite-stack):

```
socket-layer-base-greyDisc-0x1D166DC7-154x154.png
socket-layer-back-outerDisk-0xF6443089-135x135.png
socket-layer-mid-beadRing-0xBED4CF21-135x135.png
socket-layer-top-redPulse-0x23F487F3-136x135.png
socket-composite-stack.png        (200×200 composite)
```

The 200² stack draws the four layers concentric at native size
(no authored sub-rect — the socket layers carry no widget rect; the
engine composites at the disc anchor at native px). The owner's
verification — *"any of the glyphs should draw fitting nicely into
the top most disk center depression"* — checks out: `0x23F487F3`'s
center depression is sized to seat a per-node glyph icon.

The pulsing-red animation belongs to the bead ring `0xBED4CF21`,
not the top spike-frame `0x23F487F3` (the latter is dark with a
center well; the pulse is the visibly red layer underneath).

## Parallel finding: per-rarity row gaps

The broad probe also flagged scene-bound layers on the per-rarity
`Template_Node_<rarity>` widgets that the curated §10.15 rows
dropped:

- `Template_Node_Magic` 0x58-block first layer `0x621CB6FF`
  (153² in `2DUI_Paragon_transparentElements`) — magic base
  composite; previously dropped because the curated row only kept
  `MagicInteriorFill` + `MagicSelComposite`.
- `Template_Node_Legendary` 0x58-block has `0xCC3E3B25` (135² in
  **`2DUI_ParagonNodesIcons_Rogue`**) — the **first class-specific
  atlas surfaced in §10.15**. Class-specific paragon atlases exist
  in the catalog (one per playable class) but no prior round
  surfaced one on a per-rarity row.

Both added to their respective `RarityComposite()` rows. Same shape
of gap as the socket: an honest decode of the 0x58-block
enumerates every value, not just the hand-curated subset.

## Two new state overlays

The broad probe also caught two state-overlay widgets the prior
narrow probe missed (widget names without `Glyph`/`Socket`/`Ring`/
`Pulse` tokens):

| State row | Handle | Native px | Source widget |
|---|---|---|---|
| `overlay.locatedHighlight` | `0x87A89F86` | 135² | `Node_Located` (0x58-block) |
| `overlay.equipGlow` | `0xFC806F42` | 91×90 | `Node_EquipGlow` (0x58-block) |

Surfaced as new rows so the consumer can audit and the
row-completeness gate covers them. In-game roles are
state-dependent (likely a "search-result / located" highlight and
an equipped-glyph indicator); per-state activation conditions are
not yet decoded and await owner direction.

§7.2 matrix grows 19 → 21 rows. FR-C8/C12 is unreleased so the
contract is amendable (pre-publish, per the CL-25 precedent).

## Row-completeness gate

The existing per-rarity / special-node scene-bind gates run in the
**existence direction**: every layer in a row must be scene-bound.
They could not catch a row that *omitted* a scene-bound layer —
the exact CL-34 §1 / CL-31 → CL-32 class of gap.

The new gate
`ParagonRenderLayout_row_layers_cover_every_scene_bound_row_widget_handle`
runs in the **completeness direction**: every catalog-resolvable
atlas handle bound on a row-bearing widget in scene 657304 must
appear in some row's Layers (or on a documented exclusion list).
"Row-bearing widget" = the per-node / per-rarity / per-socket /
per-overlay widgets the engine composites into the per-node draw
(`Template_Node_*`, `Node_*`, `GlyphNodeGlow_*`, `Common_Node_*`,
`NodeAvailableGlow`, `Connector_*`, `Arrow_*`, `Usage_Slot_2`).

Documented exclusion: `0x3084D186` (the 25² gem-icon tile bound on
the `Usage_Slot_*` widgets — owner atlas-frame oracle confirms
this is the side-panel gem icon for an equipped glyph, not part of
the on-board socket composite). Documented in the gate source.

## Per-state variations — `needs:owner` for next round

The library surfaces the decode-true scene-bound layer **inventory**
for the socket. Per-state variations between unselected / selected /
socketed (whether the bead-ring pulse stays on selected, whether
the inner spike-frame stays on socketed in favour of the per-node
glyph icon, whether `Node_Located` / `Node_EquipGlow` activate on
specific states) are not decoded from scene data alone — they need
the owner's visual oracle on the rebuilt app's state-by-state
render. Surfacing the inventory is the FR-C12 R2 scope; per-state
refinement awaits the next round.

## Why CL-33 §1 missed this

CL-33 §1 deep-probed widgets whose names matched
`Glyph`/`Socket`/`Ring`/`Pulse` and correctly reported that the
bead ring `0xBED4CF21` *is* the complete scene-bound socket *ring*
art under that filter. But the broader composite (outer disk +
inner spike-frame) is bound under `Usage_Slot_2` — a widget whose
name doesn't match any of those tokens but whose 0x58-block scene-
binds the textures the engine then reuses for the on-board socket
render. Same shape of miss as CL-31 → CL-32 board rim (where the
icon-catalog filter dropped scene-bound rim handles that bind via
a non-icon-catalog texture path). The fix is a structural broader
probe + a row-completeness gate that runs in the reverse direction
so the next class of this miss surfaces at CI time, not at the
next visual oracle.

## Deliverable

- `src/WiseOwl.Casc.Diablo4/ParagonRenderLayout.cs`:
  - socket rows surface `[disc, outerDisk, beadRing, innerWell]` per state (.socketed uses `GlyphNodeGlow_Purchased`'s bead-ring binding)
  - new state-overlay rows `overlay.locatedHighlight` + `overlay.equipGlow`
  - per-rarity rows add `MagicBaseComposite` + `LegClassOverlay`
- `tests/WiseOwl.Casc.Diablo4.Tests/Diablo4StorageIntegrationTests.cs`:
  - new gate `ParagonRenderLayout_row_layers_cover_every_scene_bound_row_widget_handle`
  - existing pinned tests updated (row-count 21, magic layer counts 3/4)
- `docs/casc-diablo4-format.md` §7.2 / §10.15 / §10.17 + CL-34 appendix entry
- 39 / 39 tests green on `D:\Diablo IV` build `3.0.2.71886`

Probe artifacts (`socket-composite-stack.png` + per-layer PNGs) live
in `e:/tmp/scene-probe/bin/Debug/net10.0/` for owner visual review.
