# 0022 — FR-C10 R1: paragon node composite recipe

*2026-05-19*

FR-C10 overturns FR-C7 §6 R14/15 "keep our code" on node art. The
consumer's owner-validated visual oracle proved their synthetic
recipe (shared disc + whole-disc shader tint + grey beaded ring +
symbol) is not how the game composes a node. The game composites
atlas-frame layers: grey metal base with two raised concentric ridges,
a per-rarity interior fill in the recessed centre, optional ornate
outer frame (Rare/Legendary), and on the selected state a red ring
either swapped into the ornate (Rare/Legendary) or added standalone
in the inter-ridge channel (Common/Magic).

## What the records (+ visual oracle) settled

Atlas-frame inspection of every handle bound on `Template_Node_*`
0x58 blocks in scene 657304, cross-referenced with the owner's
calibration screenshots in `e:\Paragon\Screenshots\calibration\`,
yields the per-rarity role assignments:

| Rarity | Interior fill (135²ish, recessed centre) | Ornate (full-size or extended) | Selected ornate (with red ring) |
|---|---|---|---|
| 0 Common | — | — | — (engine-internal `0xB732F921` instead) |
| 2 Magic | `0xFEC31E48` (135² blue) | — | — (engine-internal `0xB732F921`) |
| 3 Rare | `0xF8373491` (135²) | `0xB71BD068` (154² yellow ornate) | `0x03EDABAB` (153² ornate + red ring) |
| 4 Legendary | `0x006ED182` (136²) | `0x232DF7F9` (189² orange spikes) | `0xBD27FB7C` (189² ornate + red ring) |

Plus the shared base: `0x1D166DC7` (154² grey ridged disc, bound on
`Node_IconBase`). Magic is owner-oracle-confirmed via
`Magic node A unselected from game.png` vs `… from app.png`;
Rare/Legendary are CASC-inferred from atlas-frame visual inspection
+ matching against `Rare node selected+unselected.png` and
`Legendary unselected.png` / `Legendary selected caught mid pulsing.png`.

## CL-29 — what shipped

1. **`NodeElement` extended** with `AtlasSno`, `NativeWidth`,
   `NativeHeight`, `EngineInternal`. Consumers composite at the
   engine's authoritative native pixel scale without a second catalog
   walk; `EngineInternal` flags the standalone red ring (catalog-
   resolvable but unbound by any scene widget — the exhaustive
   §10.14 scene-binding gate cannot see it).

2. **`Project()` rewrites the per-rarity rows** to the new recipe:
   - Common unselected: `[disc]`; selected: `[disc, engine-internal ring]`.
   - Magic unselected: `[disc, interior fill]`; selected: `[disc, fill, engine-internal ring]`.
   - Rare unselected: `[disc, fill, unselected-ornate]`; selected: swap ornate for selected-variant (red ring composited).
   - Legendary: same shape as Rare with the larger spike ornate.
   - Decode-true: only surface a per-rarity handle when its
     `Template_Node_*` 0x58 block actually contains it. A future
     season that drops or renames a handle leaves the row honestly
     shorter — the consumer's classification layer (FR-C7 §6) makes
     the call.

3. **`SceneModel`** also extended to populate `AtlasSno` + native
   size for every widget binding in the exhaustive `Scenes` view.

4. **Spec §10.11 / §10.15** updated to reflect the recipe + the
   "per-rarity colour comes from the authored interior fill, not a
   shader tint" finding. Appendix A CL-29 added.

5. **Tests:**
   `ReadParagonRenderLayout_decodes_node_composite_recipe` asserts
   per-rarity layer counts, handles, swap-on-select for
   Rare/Legendary, separate engine-internal ring for Common/Magic,
   and that every emitted layer carries non-zero `AtlasSno` and
   native size. Existing CL-25/26/27/28 gates and assertions remain
   green (33/33).

## Boundary

FR-C10 deliberately moves the composite *recipe* from the consumer
side (FR-C7 §6 R14/15) to CASC — CASC now owns "what authored layers
the game composites, in what order, at what native px". Positioning
beyond native size (the inter-ridge channel size for the engine-
internal red ring; the centred-on-disc convention for the fill) is
not authored in the scene; CASC documents the convention (native px,
centred on disc anchor) and surfaces the records. The consumer
continues to own actual ImageSharp compositing, scale-to-runtime-
resolution, and any engine-internal residuals (FR-C8 R8 / CL-28).

## Pending owner per-rarity oracle confirmation

The Magic recipe is owner-oracle-confirmed. Rare and Legendary
mappings are CASC's best record-sourced + screenshot-match inference;
they are documented as such in the spec / CL log. If the consumer's
next visual check shows a different per-rarity handle or layer order
for Rare/Legendary, that surfaces as R2 — record-sourced, not
eyeballed.
