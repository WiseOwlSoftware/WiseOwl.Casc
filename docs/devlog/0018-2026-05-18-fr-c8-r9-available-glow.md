# 0018 — FR-C8 R9: NodeAvailableGlow + FR-C7 ornate correction

*2026-05-18*

Consumer reopened FR-C8 R9: the game draws a yellow pulsing perimeter
outline around every *unselected, selectable* node (any rarity); raw
`ReadUiScene(657304)` has widget [105] `NodeAvailableGlow` but it isn't
surfaced typed — the same projection gap R6 fixed for the pointer.

## Decode

`NodeAvailableGlow` (ClassId `0x145F2056`; the consumer's
`0x14606016`/`341778518` — `341778518 = 0x145F2056`, a transcription
slip) binds handle **`0x4A901508`** (unique in the scene) + an authored
rect, one perimeter frame.

The cross-check overturned the premise. FR-C7's `Project()` used
`Elem("NodeAvailableGlow")` = `0x4A901508` as the r3/r4 "gold ornate" —
the **same projection gap CL-23 fixed for start/gate**: it never read
`Template_Node_Rare`/`_Legendary`'s OWN `0x58`-bound layer.
`Template_Node_Rare` has a textbook tag-2 `0x58` block (tag@+0=2,
handle@+8 = **`0xB71BD068`**, ownerClassId@+0x20, sentinel@+0x28) — the
genuine Rare static ornate, distinct from the glow. So `0x4A901508` was
never a per-rarity ornate; it is the **selectable/available glow**
(state-driven, any rarity — owner oracle).

## Delivery (CL-25, an FR-C7 correction)

- New **`overlay.availableGlow`** State (19th row) — `0x4A901508` +
  decoded Rect, same contract shape as `overlay.pointerTriangle`.
- r3/r4 corrected: `disc` + `Template_Node_Rare`/`_Legendary`'s own
  catalog-validated ornate (Rare → `0xB71BD068`); `0x4A901508` removed
  from the baked rarity rows. Rare medallion and selectable glow are
  now cleanly distinct (the conflation the consumer flagged).
- §7.2 matrix → **19 rows** (pre-publish contract amendment; FR-C8
  unreleased, so amendable). Spec §10.11/§10.13 + Appendix A CL-25;
  `..._decodes_proven_structure` updated + verifies it. Suite 38
  green, 0 warnings; API docs unchanged (no public shape change).
- `docs/fr-c8-response.md` R9 round (incl. the ⚠ R10 consumer action:
  this revises FR-C7 r3/r4 they validated in R3/R4 — switch the
  rare/leg ornate to the corrected `States.Layers` and add the new
  glow overlay).

**Not released** — on `main`, batched into a future owner-cut release
per the owner (memory `feedback_release-cadence`); contract amendable
until then.
