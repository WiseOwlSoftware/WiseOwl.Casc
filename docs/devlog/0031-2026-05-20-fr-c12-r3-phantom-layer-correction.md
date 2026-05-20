# 0031 — FR-C12 R3: socket-row phantom-layer correction + no-phantom gate (CL-35)

*2026-05-20*

FR-C12 R3 ([casc-fr#22](https://github.com/WiseOwlSoftware/casc-fr/issues/22))
is a delivery correction on CL-34. Owner ran the rebuilt app
(consumer commit `ab2acc0`) and ruled definitively that the game
never draws the shared per-rarity grey-base disc `0x1D166DC7` on
socket cells — in any state. CL-34's 4-layer socket row was wrong on
its bottommost layer.

## What went wrong in CL-34

When FR-C12 R2 expanded the socket row from 2 layers to 4, I kept
the original 2-layer row's leading `disc` (`Node_IconBase` /
`0x1D166DC7`) and added the three socket-specific frames on top:

```csharp
states.Add(new StateElements(-1, "socket.unselected",
    L(disc, socketOuterDisk, pulse, socketInnerWell),   // disc is the phantom
    null, null, Animation: null));
```

The implicit assumption: the shared rarity-base disc is universal —
every node-class draw composites it as the bottom layer. That holds
for the per-rarity rows (Common/Magic/Rare/Legendary all start with
`disc`) but the socket node-class has its OWN ornate outer disk
(`0xF6443089`, 135² in `2DUI_Paragon_transparentElements`) and does
NOT composite the shared grey-base.

## Visual evidence (owner game-vs-app oracle)

The 154² grey base (`0x1D166DC7`) is geometrically larger than the
135² ornate outer disk (`0xF6443089`). Composited concentrically,
the grey base would project ~9.5 px beyond the ornate disk's
silhouette as a thin grey ring around the socket. The game NEVER
shows that ring on any socket in any state (unselected / selected /
socketed). The phantom is geometrically visible in the consumer's
test render (CL-34 integration) and ABSENT in the in-game render —
a clean A/B oracle.

## Fix

Drop `disc` from all three `socket.*` rows. The corrected recipe is
the three game-visible layers only:

```csharp
states.Add(new StateElements(-1, "socket.unselected",
    L(socketOuterDisk, pulse, socketInnerWell),
    null, null, Animation: null));
states.Add(new StateElements(-1, "socket.selected",
    L(socketOuterDisk, pulse, socketInnerWell),
    null, null, null));
states.Add(new StateElements(-1, "socket.socketed",
    L(socketOuterDisk, pulseSocketed),
    null, null, null));
```

## Row no-phantom gate (the new CI invariant)

Owner's R3 ask explicitly: *"every row layer corresponds to an
engine draw call, not just a widget binding"*. CASC can't observe
engine dispatch directly — but we can encode the AUTHORIZED widget
set per state class (which widgets the engine instantiates for the
draw of a node in that class), and assert every row layer's
source widget is in the authorized set.

For socket states: authorized widgets are
`{GlyphNodeGlow_Revealed, GlyphNodeGlow_Purchased, Usage_Slot_2}`.
`Node_IconBase` is NOT in this set — so a row containing
`0x1D166DC7` (bound only on `Node_IconBase`) fails the gate.

New test: `ParagonRenderLayout_socket_rows_have_no_phantom_layers`.

This is the dual of CL-34's row-completeness gate:

| Gate | Direction | Catches |
|---|---|---|
| `ParagonRenderLayout_per_rarity_layers_are_scene_bound` (CL-30) | existence | per-rarity layers fabricated outside scene |
| `ParagonRenderLayout_special_node_layers_are_scene_bound` (CL-33) | existence | special-node layers fabricated outside scene |
| `ParagonRenderLayout_row_layers_cover_every_scene_bound_row_widget_handle` (CL-34) | completeness | scene-bound row-widget handles missing from rows |
| `ParagonRenderLayout_socket_rows_have_no_phantom_layers` (CL-35) | no-phantom | row layers from widgets the engine doesn't dispatch for the state |

Completeness + no-phantom together = the row exactly equals the
engine's actual dispatch (modulo the per-state activation conditions
that still need owner direction).

## Why the existing gates didn't catch this

- The per-rarity / special-node scene-bind gates (CL-30, CL-33) run
  in the EXISTENCE direction: `0x1D166DC7` IS scene-bound (on
  `Node_IconBase`), so the gates passed.
- The row-completeness gate (CL-34) runs in the COMPLETENESS
  direction: every scene-bound row-widget handle is in SOME row, so
  including `0x1D166DC7` in the socket rows satisfied it.
- Neither gate could catch the phantom — a layer correctly
  scene-bound, correctly in some row, but in the WRONG row for what
  the engine actually dispatches. The no-phantom gate plugs that
  hole.

## Per-state pulse/glyph activation — still `needs:owner`

The R3 ask is owner-facing only on the phantom layer. The bigger
open question — whether the bead-ring pulse animation stays on
`.selected` (currently row is identical to `.unselected`), and
whether the inner spike-frame stays on `.socketed` (currently
dropped) — still awaits the next visual oracle on the rebuilt
3-layer row.

## Sharpened "follow the recipe" directive

The consumer-side `feedback_follow-full-game-recipe` memory now has
a precise meaning: *the* recipe is **what the engine actually
composites**, not a §7.2 row that includes layers the engine
doesn't dispatch. CL-35 is the first library-side enforcement of
this — the row no-phantom gate fails CI if the projection
contaminates a row with a non-dispatched layer.

## Deliverable

- `src/WiseOwl.Casc.Diablo4/ParagonRenderLayout.cs`: socket rows
  drop the leading `disc` layer; all three socket states.
- `tests/WiseOwl.Casc.Diablo4.Tests/Diablo4StorageIntegrationTests.cs`:
  new gate `ParagonRenderLayout_socket_rows_have_no_phantom_layers`.
- `docs/casc-diablo4-format.md` §10.17 narrative + new CL-35 entry.
- 40 / 40 tests green on `D:\Diablo IV` build `3.0.2.71886`.
- PR [#27](https://github.com/WiseOwlSoftware/WiseOwl.Casc/pull/27)
  amended (rebased + R3 commits pushed).
