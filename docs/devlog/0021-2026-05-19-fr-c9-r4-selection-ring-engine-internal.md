# 0021 — FR-C9 R4: selection ring is engine-internal / per-rarity composite

*2026-05-19*

CL-27 surfaced `Node_SearchResultHighlight` (`0x49FDA722`) under
`overlay.selectionRing` — the only widget-name candidate from a pure
record-name search of scene 657304. R4's visual oracle disproved that
mapping: the emitted atlas frame is a spiked corona (search-result
decoration), not the smooth red selected-state ring the in-game oracle
shows.

## What the records (and atlas frames) actually settled

Decoded every candidate handle's atlas frame and inspected them
directly. The smooth red selected-node ring is **`0xB732F921`**: 96×95
in `2DUI_Paragon_transparentElements` (SNO 2061536), a clean red ring
hugging disc-diameter. It is **not bound to any scene widget** in
657304 or 964599 — the catalog resolves it, but no 0x22 field or 0x58
block in either scene references it.

For rarity 2/3/4 the same ring shape lives **composited inside the
rarity's selected-variant disc** — the per-rarity `Template_Node_*`
0x58 block carries:

| Widget | Unselected | Selected (red ring composite) |
|---|---|---|
| `Template_Node_Magic` | `0x621CB6FF` (plain blue disc) | `0x72C29402` (blue + red ring) |
| `Template_Node_Rare` | `0xB71BD068` (yellow ornate) | `0x03EDABAB` (yellow ornate + red ring) |
| `Template_Node_Legendary` | `0x232DF7F9` (orange ornate) | `0xBD27FB7C` (orange ornate + red ring) |

(Visually verified frame-by-frame.)

So:

- **Common** (rarity 0): no per-rarity selected disc variant — the
  engine renders `0xB732F921` directly. No scene-widget binding.
- **Magic / Rare / Legendary** (rarity 2/3/4): the red ring is part of
  the rarity-selected disc composite, bound on
  `Template_Node_{rarity}` via the 0x58 block (§10.12). The ring is
  *not* a separate overlay layer.

In neither case is `overlay.selectionRing` a scene-widget-bound row.
CL-27's `Node_SearchResultHighlight` mapping was the search-result
decoration, a different in-game feature (red spiked corona on
glyph-search-matched nodes, not the selected-state ring).

## CL-28 — what shipped

1. **Revert** the `overlay.selectionRing` → `Node_SearchResultHighlight`
   binding. The widget is correctly surfaced by
   `ReadParagonRenderModel().Scenes` (the exhaustive view) as a
   `Node_SearchResultHighlight` binding; it is not under any `States`
   row.

2. **New record field `StateElements.Unresolved`** — `bool`, default
   `false`. `true` means the row is enumerated by the §7.2 schema for
   completeness but no scene widget binds its art (engine-internal, or
   the art is composited inside another row's bindings).
   `overlay.selectionRing` now emits `Layers = []`, `Unresolved = true`.

3. **Per-binding-record gate honors `Unresolved`.** The CL-27 gate
   asserted "every enumerated state row carries ≥1 layer or is
   structurally unresolved"; the unresolved branch is now a concrete
   field check rather than a contract-only allowance. The gate also
   asserts the inverse — `Unresolved = true` rows must in fact have
   empty `Layers`.

4. **Spec §10.11 / §10.13 / §10.14** updated to reflect current truth
   (the selected-state red ring is per-rarity composite for rarity
   2/3/4 and engine-internal `0xB732F921` for Common; the
   `Node_SearchResultHighlight` row is the search-result decoration,
   not the selection ring). Appendix A CL-28 added.

5. **Tests:** `ReadParagonRenderLayout_decodes_directional_arrows`
   asserts the new selectionRing shape (empty `Layers`,
   `Unresolved = true`); the per-record gate asserts the
   `Unresolved` semantics in both directions.

Acceptance (live `3.0.2.71886`): 32/32 Diablo4 integration tests
green.

## Per-rarity selected/unselected differentiation — separately tracked

Frame inspection also showed that the existing per-rarity rows
(`r2|3|4 × unselected|selected`) currently use the SAME `Layers` for
both `unselected` and `selected` — i.e. the rarity-template's full
0x58-block handle list, undifferentiated. The visual oracle implies a
proper split (unselected → `0x621CB6FF` / `0xB71BD068` / `0x232DF7F9`;
selected → `0x72C29402` / `0x03EDABAB` / `0xBD27FB7C`). This is a
separate structural correction outside R4's scope ("name the state
that carries the ring") and is **not** addressed by CL-28. If the
Optimizer's next visual check reveals the rarity-selected discs aren't
rendering correctly, that surfaces as a new round / FR.

## Boundary kept

CASC's contribution is the **decoded answer to "where does the smooth
red ring come from"**: `0xB732F921` engine-internal for Common; the
per-rarity selected disc composites for the others — both
record-sourced (one from the catalog, one from the 0x58 block).
Role/state classification (whether the consumer wants to render Common
nodes with their own emulation of `0xB732F921` or omit the ring) stays
consumer-owned per FR-C7 §6.
