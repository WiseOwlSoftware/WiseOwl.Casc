# 0017 — FR-C8 R5/R6: arrows+connectors, per-layer rect, animation

*2026-05-18*

Consumer reopened FR-C8 with R5 (per-layer rect/scale/tint for the
start/gate layers — currently `default`, eyeballed) and R6 (the
directional-arrow asset is in the atlas — use the real art, not a
polygon). Owner also folded in: decode the legendary/socket **animation
order + timing**.

## R6 — pointer & connectors are bound art (FR-C7 §6 correction)

Raw scan: all four arrow handles (`0xD51CAB25`/`0x6D3CB8DE`/
`0x8EEAC178`/`0xB6D8C741`) are in ParagonBoard 657304, inside the
`Arrow_{Top,Right,Bottom,Left}` widget spans — bound via the **standard
0x22 texture-handle field**, not a 0x58 block. FR-C7 missed them
because (a) it hardcoded the `overlay.*` rows empty and (b) a widget's
*last* 0x22 record straddles the next widget's `nameStart`, and
`UiScene.Parse`'s `p + RecordSize <= to` bound dropped it — and the
texture handle is precisely that last record. Surgical fix: collect the
straddling tail record's value (`+0x08`) when it fits; the full-record
scan for every other record is byte-identical (no FR-C7 regression).

The same fix revealed `Connector_*` also bind art (`0x77ECA3A8`/
`0x288DE11F`) — so FR-C7's "connector bars procedural / not in data"
was wrong too. `overlay.pointerTriangle` / `overlay.connectorBar` now
carry the T/R/B/L bound layers (handle + decoded `Rect`);
`overlay.selectionRing` has no scene widget → genuinely engine-drawn
(stays empty — honest). The `..._decodes_proven_structure` test's
obsolete `Assert.Empty(connectorBar)` was corrected.

## R5 — start/gate per-layer rect: definitively not authored

The §10.12 0x58 layer blocks are **handle-only** — the full 88 bytes
are zero except tag/handle/ownerClassId/sentinel; the descriptor points
at a Common node child, so the frame layers inherit the `NodeTemplate`
(100-ref) box. There is no per-layer rect/scale/tint to surface →
`Rect`/`Alpha` stays `default` (the decoded answer). Consumer sizes the
frames to `NodeTemplate`; no authored fraction exists. The
arrow/connector rects, by contrast, are authored and now surfaced.

## Animation — engine-driven (reaffirmed, definitive #3)

The pulse layer **order** is delivered (`States.Layers`). The
**timing** is not in the scene: no period/min/max on the glow widgets;
the `Storyboard_*` widgets are UI transitions (fades/expand/rotate),
not a per-node pulse loop (48 DT_FLOAT fields, none bind glow timing).
Reaffirms FR-C7 — `AnimSpec=null` is the evidence-backed answer; the
pulse is an engine shader loop, bake a static frame (FR-C7 §6). Reopen
with an in-game oracle if a build shows authored timing.

## Delivery

Spec §10.13 + Appendix A CL-24; `ParagonRenderProjection` projects
`Arrow_*`/`Connector_*` into the overlay rows;
`ReadParagonRenderLayout_decodes_directional_arrows` + corrected
`..._proven_structure`; suite 38 green, 0 warnings; API docs
unchanged (no public shape change). `docs/fr-c8-response.md` R5/R6
rounds. **Not released** — on `main`, batched into a future owner-cut
release per the owner (memory `feedback_release-cadence`); FR-C8
contract amendable until then.
