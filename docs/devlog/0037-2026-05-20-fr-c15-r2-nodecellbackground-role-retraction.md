# 0037 — FR-C15 R2: `NodeCellBackground` role retraction (CL-39)

*2026-05-20*

Consumer plumbed the CL-33 `ParagonRenderLayout.NodeCellBackground`
field end-to-end (commit `BrentRector/Paragon@d01f772`): bake reads
the field → `EmitLayer(0xC1473C21)` writes a 135×135 PNG. Visual
inspection of the baked PNG and the raw atlas frame revealed
**horizontal ember-strip / glow-band content**, NOT the clean
rounded darker square owner sees in-game as the persistent
per-node cell tile.

The CL-33 binding traversal was **structurally correct** — the
`Common_Node_Revealed` widget IS in scene 657304, IS bound through
the standard `0x6B1C5D9C` texture-handle field, IS catalog-resolvable
in 2DUI SNO 447106 at the authored 94×94 footprint. The mistake
was in the role assertion: assuming "widget named `_Revealed` + bound
on a per-cell-pitch rect" implies "the persistent per-node tile."

The widget's actual visual role is more likely a **transient
cell-reveal animation** (engine animates the ember glow as a cell
becomes visible during board exploration), consistent with the
widget name `_Revealed`. The persistent per-node tile owner
describes (the subtly-darker rounded square the lighter board field
shows through between cells) is **bound elsewhere** OR **engine-
procedural** — currently unidentified.

## Lesson captured in memory

`feedback_widget-name-not-role.md`: a widget's name is not
authoritative evidence of its visual role. Binding traversal can be
correct while the resolved texture's content doesn't match the
proposed role. State binding-claim and role-claim separately; never
ship a typed API field whose NAME asserts a role until visually
verified.

Pattern parallels the CL-38 retraction (FR-C14, 2026-05-20):
atlas-name `2DUI_ParagonBackground` matched the role on intuition,
owner ruled wrong. CL-33 / CL-39 is the same shape applied to
widget naming.

## What changes

1. **Field rename**: `ParagonRenderLayout.NodeCellBackground` →
   `ParagonRenderLayout.CommonNodeRevealedLayer`. Binding-derived
   name; no role assertion.

2. **Doc comment rewrite**: drop "per-node cell background tile"
   language. Describe ONLY the binding facts (the widget, the
   handle, the rect, the atlas) + the empirical visual content
   (horizontal ember-strip) + the role-retraction context.

3. **Test rename**:
   `ReadParagonRenderLayout_surfaces_per_node_cell_background` →
   `ReadParagonRenderLayout_surfaces_common_node_revealed_binding`.
   Test now asserts ONLY the binding facts (handle, rect, atlas) —
   no role assertion.

4. **Row-completeness gate (CL-34) reference**: code comment
   updated to refer to `CommonNodeRevealedLayer` (binding-only) +
   note the role retraction.

5. **§10.17 spec narrative**: rewritten as binding-facts +
   role-retraction note. The persistent per-node cell tile owner
   sees is documented as **unidentified** in CASC's current decode.

6. **CL-39 Appendix A entry**: documents the retraction + the
   widget-name-not-role lesson + the parallel to CL-38.

## Boundary preserved

FR-C7 §6: library = complete faithful decode + no-fabrication
discipline. The binding IS in the scene (decoded faithfully); the
role-claim was the fabrication. Removing it is the discipline-
correct fix. The library now surfaces the binding facts; the
consumer + owner determine the visual role via the oracle.

## What's unchanged

- The binding itself is still in `ParagonRenderLayout` — renaming
  the field doesn't drop the decoded fact. The consumer that
  previously read `NodeCellBackground` now reads
  `CommonNodeRevealedLayer` for the same `NodeElement` — pure
  rename.

- The FR-C9 exhaustive widget-binding view
  (`ParagonRenderModel.Scenes`) is unaffected; the
  `Common_Node_Revealed` widget appears in that catalog as ever
  with its handle + rect.

- The row-completeness gate (CL-34) continues to cover the binding
  through the renamed field.

## Phase 2 / next-round expectations

The "where is the persistent per-node tile bound" question remains
open. Three viable RE paths (any one would unblock):

1. **Owner-direction on a candidate widget** beyond the
   `Common_Node_*` family the FR-C12 R2 broad probe enumerated.

2. **Non-DT_HANDLE field-type sweep** — re-probe scene 657304 for
   any field carrying a texture-resolving value through a type
   other than `0x6B1C5D9C` (the FR-C11 R2 / R3 §1 rim-side lesson
   suggested non-icon-catalog texture paths). My field-type
   histogram from FR-C12 R2 ruled this out at the time but it was
   focused on socket-row layers; cell-background may differ.

3. **Engine-procedural disposition** (CL-31 §3 / FR-C11 R3 §1
   precedent) — the cell tile is rendered procedurally without a
   scene-data atlas, like the rim-fire animation. Owner accepts the
   consumer maintains a procedural fallback (currently
   `#3A3A3A` flat fill or similar). Documented as engine-internal.

Standing by at FR-C15 R2 awaiting owner direction. No new
typed-API field proposed; the existing binding-only field
(`CommonNodeRevealedLayer`) remains as the honest decoded fact.

## Deliverable

- `src/WiseOwl.Casc.Diablo4/ParagonRenderLayout.cs`: field rename +
  Project() comment rewrite.
- `tests/.../Diablo4StorageIntegrationTests.cs`: test rename +
  doc-comment update + row-completeness-gate code-comment update.
- `docs/casc-diablo4-format.md` §10.17 rewrite + new CL-39 entry.
- 40 / 40 tests green on `D:\Diablo IV` build `3.0.2.71886`.
- Memory: `feedback_widget-name-not-role.md` (new).
- PR forthcoming.
