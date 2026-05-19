# 0020 — FR-C9 R3: selectionRing binding surfaced + per-record gate

*2026-05-19*

R2 (CL-26) made the *handle set* exhaustive and gated it: every
handle-magnitude atlas-resolvable u32 in the raw paragon scenes had to
appear somewhere in the exhaustive model. R3 reproduced a defect of
exactly the class FR-C9 exists to eliminate: the enumerated state
`overlay.selectionRing` returned `Layers=[0]` while its three siblings
(`availableGlow`, `connectorBar`, `pointerTriangle`) carried their
authored bindings. The CL-26 gate stayed green anyway because the
dropped binding's handle was shared with another widget — a
handle-level gate structurally cannot catch that.

## What the records actually settled

Probed scene 657304 widget-by-widget for handle/rect/alpha and for
shared-handle pairs. There is **no widget** in 657304 with `Selection`
or `Selected` in its name. The only widget that fits the
record-sourced profile of "ring overlay on a node, binding shared with
an already-surfaced widget" is `Node_SearchResultHighlight`:

- Binds handle `0x49FDA722` via the standard `0x6B1C5D9C`-typed
  texture-handle field (the same 0x22 path the other three overlays
  use; class `0x1E3077C7`, alpha `0xFF`).
- Atlas SNO `1332563`, frame **180×180 px** — the node-overlay ring
  size class (start/gate/availableGlow are also `NodeTemplate`-
  inherited; selectionRing follows that pattern — no authored rect).
- Handle is shared with `Glyph_GridItem_SearchResultHighlight` (panel
  chrome) — *exactly* R3's "handle shared with an already-classified
  widget", which is why the CL-26 gate stayed green even though the
  binding-record was dropped.

The previous code (`ParagonRenderLayout.cs`) hardcoded the row empty
with the comment "`overlay.selectionRing` has no scene widget →
genuinely engine-drawn"; that conclusion was wrong. Removed.

## CL-27 — what shipped

1. **`overlay.selectionRing` now binds `Node_SearchResultHighlight`**
   (handle `0x49FDA722`, rect default ⇒ template-inherited, alpha
   `0xFF`). Same `{handle, rect, order, tint, alpha}` fidelity as the
   three sibling overlays. Role/state classification (whether the
   consumer renders this as the in-game red ring on selected nodes,
   the search-result highlight, or both) stays consumer-owned per
   FR-C7 §6 — CASC surfaces the decoded binding; the consumer decides
   how to use it.

2. **Per-binding-record gate**
   (`ParagonRenderLayout_every_enumerated_state_has_layers`) —
   shape-agnostic complement to the CL-26 handle gate. Asserts that
   every enumerated state in `ReadParagonRenderLayout().States`
   carries at least one bound layer (or is structurally unresolved —
   none currently are). A future projection that enumerates a state
   row but leaves it empty fails casc CI regardless of whether the
   handle appears under another widget. The two gates together form
   the structural completeness story FR-C9 was about: CL-26 catches
   *handle-level* drops, CL-27 catches *record-level* drops.

3. **Spec §10.14** updated with the strengthening (the per-record gate
   and its complementary relationship to CL-26). Appendix A CL-27
   added. The §10.13 line that said `overlay.selectionRing` had no
   scene widget was corrected to record the
   `Node_SearchResultHighlight` binding.

4. **Updated `ReadParagonRenderLayout_decodes_directional_arrows`** to
   assert the new selectionRing binding shape (single layer, handle
   `0x49FDA722`, default rect, alpha `0xFF`).

Acceptance (live `3.0.2.71886`): all 32 Diablo4 integration tests
pass, including both gates (CL-26 handle + CL-27 per-record).

## Boundary kept

This is decode-only — CASC surfaces the record that's actually in the
scene; the consumer reclassifies their `WidgetRoles` (currently
`Node_SearchResultHighlight` is under "node-art (consumer FR-C7 §6
recipe)") and decides whether to render it as the selection ring. If
the consumer's visual oracle shows this isn't the right binding for
the in-game red ring on selected nodes, that surfaces as an R4
counter-round, not a CASC-side guess.
