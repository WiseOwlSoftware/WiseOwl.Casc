# 0026 — FR-C11 R3 + FR-C12 R1: per-cell bg, NodeAvailableGlow extent, special-node recipes & gate, glyph socket honest report

*2026-05-19*

Two FRs in one round. FR-C11 R3 closes the per-node-cell background
loop the owner game-vs-app oracle opened (the apparent grey grid is
actually the lighter field showing through inter-tile gaps —
**no drawn border**) and clarifies the `NodeAvailableGlow` extent
question. FR-C12 R1 brings the special-node (-1 rarity) composites
under the same scene-bind discipline as the per-rarity recipe and
the board chrome, plus answers the glyph-socket "missing perimeter
decorations" question honestly. Both delivered in this single round
because they share the same lessons (CL-30 / CL-32 scene-bind
gate, no fabrication, raw `ReadUiScene` for non-icon-catalog
verification).

## FR-C11 R3 — record-sourced findings

**§2: per-node-cell background.** Probe of `Common_Node_BG_Black` /
`Common_Node_Revealed` in scene 657304:

```
[Common_Node_BG_Black]  cls=0x1E3077C7  fields=10
    rect L=R=T=B=3, W=H=100, alpha=0xFF
    field 0x0C152636 (TexHandle) = 0xC1473C21
[Common_Node_Revealed]  cls=0x1E3077C7  fields=10
    rect L=R=T=B=3, W=H=100, alpha=0xFF
    field 0x0C152636 (TexHandle) = 0xC1473C21
```

Both bind the same handle `0xC1473C21` (135² in 2DUI atlas SNO
447106 — catalog-resolvable). The widget records `dwAlpha = 0xFF`,
so the semi-transparency the FR notes comes from the atlas frame
itself, not a widget multiplier. Authored rect is 3-ref inset on
each side of the 100-pitch `NodeTemplate` → 94×94 tile centred in
the 100×100 cell → 6-ref inter-cell gap. The "lighter field showing
through" is exactly that gap — no drawn border. CL-33 surfaces this
as `ParagonRenderLayout.NodeCellBackground` (single `NodeElement`).
The `_Black` widget is the hidden-state sibling (same handle, same
rect) — pre-revelation; surfacing the `_Revealed` variant is the
typed surface for the visible state.

**§3: `NodeAvailableGlow` authored extent.** The widget's authored
rect is genuinely all-zero — `NodeTemplate`-inherit (a 100×100 cell).
But the bound atlas frame `0x4A901508` is **325 × 326 px** in the
2DUI atlas — over 3× the cell pitch. The engine draws the frame at
native pixel size centred on the cell, so adjacent yellow
selectable-frames nearly touch — exactly what the in-game oracle
shows. The data is already in `NodeElement.NativeWidth / NativeHeight`
(populated by `FrameSize` since CL-29). The consumer needs to
compose at native pixel size, not at the cell rect — drawing at 1
cell under-draws by 3× linear (~10× area). Documentation answer; no
new code.

## FR-C12 R1 — special-node delivery

**§1: glyph socket.** Probe of every "Glyph"/"Socket"/"Ring"/"Pulse"
widget in scene 657304 — only `GlyphNodeGlow_Revealed` and
`GlyphNodeGlow_Purchased` bind socket art, both via the same
handle `0xBED4CF21` (135² in 2DUI_Paragon_transparentElements SNO
2061536, catalog-resolvable). Visual inspection of `0xBED4CF21`'s
atlas frame: it is the **complete** socket ring with the bead-like
perimeter decorations the FR asked about — they are baked into the
texture itself. There is no separate scene widget for additional
perimeter art, and the icon-catalog-filter pattern that dropped the
board-rim sides in CL-32 does not apply here (`0xBED4CF21` IS
catalog-resolvable and has been surfaced since CL-7-era texture
support). If the consumer's rendered socket ring is missing the
perimeter decorations, the issue is in the consumer composite
(crop / scale / alpha blend), not a CASC decode gap. Honest
CL-28-grade report.

**§2: selected-node red ring re-verify.** No change from CL-30 — the
selected ring is part of each per-rarity selected composite
(`0x72C29402` Magic / `0x03EDABAB` Rare / `0xBD27FB7C` Legendary /
`0xD3051CCA` Common via `Node_Purchased`). The standalone
`0xB732F921` from CL-28 remains in the icon catalog but bound to no
scene widget and not referenced by any recipe.

**§3: start + gate composite recipes.** Recorded in §10.17 as a
recipe table for parity with the §10.15 per-rarity table. The
existing CL-23/24 layers are surfaced as-is in `States` rows; CL-23
mapped the gate ornate-squares (`0xC2DF4786` selected /
`0x0E6B6249` unselected) from visual inspection — the 0x58-block
state-flag bytes have not been RE'd, so a state-specific render
difference is unverified beyond CL-23's mapping. Re-verifying on
owner visual oracle is in scope of a future FR if a state-specific
render is required.

**§4: special-node scene-bind gate.** Added
`ParagonRenderLayout_special_node_layers_are_scene_bound` — parity
with the per-rarity gate (CL-30) and the board-chrome gate
(CL-31/32). Cross-references every `RarityOverride < 0` row's
layers against the **raw** scene 657304 widget data via
`ReadUiScene`, not the icon-catalog-filtered `Scenes` view (the
CL-31 → CL-32 lesson, applied to special nodes). Currently green
on `3.0.2.71886`.

## What did NOT ship in CL-33 (deliberately)

- **FR-C11 R3 §1: rim texture lookup subsystem.** Phase 2 work —
  resolving `0x900C7D87` / `0x225F2DA8` to texture data via a
  non-icon-catalog path. Substantial R&D; tackled in a follow-on
  branch / CL after this round lands.

- **Gate selected vs unselected differentiation.** CL-23's mapping
  (`0xC2DF4786` selected / `0x0E6B6249` unselected) is preserved.
  RE'ing the 0x58-block state-flag bytes is non-trivial and outside
  this round's record-sourced scope.

## Acceptance

38/38 Diablo4 integration tests green on live `3.0.2.71886`. New:
`ReadParagonRenderLayout_surfaces_per_node_cell_background` (FR-C11
R3 §2), `ParagonRenderLayout_special_node_layers_are_scene_bound`
(FR-C12 §4). Existing CL-25 → CL-32 gates and assertions all remain
green.

## API surface

`ParagonRenderLayout` gains a positional `NodeCellBackground` field
(additive, pre-1.0-alpha). `docs/api/` regenerated.

## Spec / audit trail

§10.17 added (per-cell BG + special-node addendum); Appendix A CL-33
added.
