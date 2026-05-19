# 0023 — FR-C10 R2: Magic/Common selected attribution + scene-bind gate

*2026-05-19*

CL-29's R1 was owner-validated (the consumer's IconPipeline composites
the per-rarity recipe + sRGB OETF correctly; visual oracle "best it
ever has"). But the Optimizer's R2 root-causes a decode-level error in
CL-29 that the consumer's renderer happened to work around: Magic and
Common's *selected* recipe pointed at the standalone 96² red ring
`0xB732F921` rather than the in-scene composites the game actually
renders.

## What R2 settled

Compositing CL-29's Magic-selected layers at the stated native sizes
— `0x1D166DC7@154 + 0xFEC31E48@135 + 0xB732F921@96` — yields a small,
centred red ring. The game-correct Magic-selected disc is
`0x72C29402` (owner-validated, 154² blue disc + perimeter ring
composite), which sits in the inter-ridge channel — *not* centred.
The same misattribution applied to Common: the game-correct
Common-selected is `0xD3051CCA`, bound on the separate
`Node_Purchased` widget.

Both `0x72C29402` and `0xD3051CCA` are scene-bound: `0x72C29402` is
in `Template_Node_Magic`'s 0x58 block alongside the `0xFEC31E48`
interior fill I had already identified; `0xD3051CCA` is the single
binding on `Node_Purchased` (the "allocated/spent paragon point"
indicator). CL-29 had both in the exhaustive `Scenes` view but the
per-rarity recipe never consumed them.

## Root cause

CL-29 took a uniform decomposition path for Magic/Common-selected
("grey base + interior + standalone red ring") because the per-rarity
0x58 block partition was incomplete. For Rare and Legendary I
correctly identified the in-block selected-variant composite
(`0x03EDABAB`, `0xBD27FB7C`) and swapped the ornate on selected. For
Magic, `0x72C29402` *was* in `Template_Node_Magic`'s block all along
— I dismissed it as "alt versions used in other contexts" without
inspecting whether it was actually the selected-state composite.

The visual cue would have been clear from frame inspection:
`0x72C29402` is a full-disc composite at 154² (matching the base
disc) with the red ring at the *perimeter*, in the inter-ridge
channel. CL-29's standalone ring (`0xB732F921`, 96²) is a centred
ring at a different scale entirely. Same visual oracle test I should
have run for every per-rarity selected handle, not just Rare/Leg.

For Common: I correctly classified `Node_Purchased`'s binding
(`0xD3051CCA`, 153² composite with perimeter ring) as a node-overlay
binding under SceneModel, but never connected it to the
Common-selected state in the per-rarity recipe.

## CL-30 — what shipped

1. **Magic/Common-selected recipe corrected.** Magic-selected now
   surfaces `0x72C29402` (Template_Node_Magic's 0x58-block selected
   composite); Common-selected surfaces `0xD3051CCA` (Node_Purchased
   widget binding). The standalone `0xB732F921` is no longer
   referenced by any per-rarity recipe.

2. **`NodeElement.EngineInternal` field removed.** Added in CL-29
   to flag the standalone red ring layer as engine-internal; with
   CL-30 removing that usage, the field had no remaining data behind
   it. The structural distinction was a side-effect of the
   mis-attribution; removing the field per "no abstractions beyond
   what the task requires".

3. **New gate `ParagonRenderLayout_per_rarity_layers_are_scene_bound`:**
   every per-rarity (rarity 0/2/3/4) layer's `TextureHandle` must
   appear in scene 657304's per-widget bindings (the exhaustive
   `Scenes` view). Catches a recipe layer that references an atlas
   frame no scene widget binds — the CL-29-class regression. The
   gate uses the existing `ReadParagonRenderModel().Scenes` view
   rather than a new typed flag.

4. **Spec §10.11 / §10.13 / §10.14 / §10.15** rewritten to current
   truth: every per-rarity selected state is scene-bound; the
   standalone `0xB732F921` is in the catalog but bound to no scene
   widget and isn't used by any per-rarity recipe. Appendix A CL-28
   amended (the smooth red ring attribution to `0xB732F921` was
   incomplete — CL-30 superseded) and CL-29 amended (Magic/Common
   selected mis-attribution corrected). Appendix A CL-30 added.

5. **Codebase pass:** removed stale "engine-internal" / `0xB732F921`
   references from in-method comments, XML doc comments, and test
   docstrings — the recipe reads as current truth, no
   change-history accreted into the public API surface.

Acceptance (live `3.0.2.71886`): 34/34 Diablo4 integration tests
green, including the new scene-bind gate and the updated
recipe/per-record/handle gates.

## What `0xB732F921` actually is

A smooth red ring atlas frame (96² in
`2DUI_Paragon_transparentElements`, SNO 2061536), present in the
icon catalog, bound to **no** scene widget in 657304 or 964599. CL-28
called it the "engine-internal selection ring" on the assumption
that the engine references it directly for the selected-state visual.
CL-30's R2 root-cause shows that's incorrect for paragon node
selection — the per-rarity selected composites carry the actual
ring. `0xB732F921` may serve a different in-game role we have not
identified; absent evidence, the row stays out of the recipe and
`overlay.selectionRing` remains `Unresolved = true` (no scene widget
binds it as a separate overlay).

## Audit trail

CL-29's owner-validated R1 consumption is real and stays consumed —
the consumer's renderer composites the correct per-rarity disc art
because it uses the owner-identified composites directly (FR-C7 §6
classification). CL-30 corrects CASC's *projection* of those
composites in `ReadParagonRenderLayout().States` so the recipe is
decode-true even for downstream consumers who haven't done the
owner's classification pass.
