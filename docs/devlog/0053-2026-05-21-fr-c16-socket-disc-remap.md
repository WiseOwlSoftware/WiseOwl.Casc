# 0053 — FR-C16 #26.4: socket node disc remapped to the base-disc band

2026-05-21 · CL-54 · branch `fr-c16-socket-disc-remap`

## Trigger

Optimizer (during owner visual validation of CL-52): the **socket node
renders wrong** — no cardinal arrows / connector bar, and a stray white dot
upper-left. Its on-board disc kept the `Usage_Slot_2` side-panel geometry/z
and painted over the purchased add-on; the side-panel usage-pip beads leaked
on-board. Asked: apply the same base-disc remap the rarity discs get.

## Ground truth (`SnoScan widgetdump 657304`)

```
Node_IconBase            img=0x1D166DC7 [L=7,R=7,T=7,B=7] vAnc=3  (inset-7 86² centred = base-disc slot)
Template_Node_Socketable img=0          (empty: no handle, no children)
Usage_Slot_1             img=0x3084D186 [W=12,H=12]  children=0   (side-panel usage-pip bead)
Usage_Slot_2             img=0x3084D186 [W=12,H=12]  children=9:
   [0] 0xF6443089 vAnc=3 tint=FF8A8A8A   (grey socket disk, centred)
   [1] 0xF6443089 [W=100,H=100]          (same disk, absolute side-panel)
   [5] 0x23F487F3                          (inner spike-frame)
   [7] 0xBED4CF21 [L2R2T2B2 W=90 H=95] vAnc=3   (ring)
   [8] 0xBED4CF21 [L2R2T2B2] vAnc=3
```

So the socket disc art is **not** on `Template_Node_Socketable` (empty) — it
lives on the `Usage_Slot_*` equipped-glyph side-panel widgets, whose frames
the engine reuses on-board (FR-C12 / CL-34). CL-52's recipe builder sent these
through the generic non-template branch → emitted at the widget's scene-z
(z47–54, above the symbol and the arrows/connectors at z31–44) and emitted the
widget's own 12² pip.

## Fix (interpretation, not a patch)

Per `feedback_oracle-corrections-fix-interpretation`: the bug is that the
projection doesn't recognise `Usage_Slot_*` as the **`KindSocket` type-disc
carrier** (the socket-kind analogue of a rarity `Template_Node_*`). The fix is
one branch in `ParagonRenderLayout.NodeRecipe`:

- For `Usage_Slot_*`: emit only the **handle-bearing disc children**, remapped
  into the base-disc band (`baseZ` = `Node_IconBase`'s index), gated
  `[KindSocket]` + `bActive`-finalized — so the socket disc composes below the
  symbol/arrows/connectors exactly like any base disc.
- The widget's **own** `hImageFrame` (`0x3084D186`, the 12² side-panel
  usage-pip bead) is not part of the on-board node → not emitted.
- Removed the now-dead `Usage_Slot_*` case from `BaseActivation`.

Disc geometry stays **authored** (centred, ~100²) — not refit to 86² (the rule
forbids fabricating rects to match an oracle assumption; the disc is anchored,
so it centres in the cell).

## Result (recipe dump)

```
z 3 Node_IconBase      0x1D166DC7   (base, [KindCommon,Unpurchased])
z 7 Usage_Slot_2[0]    0xF6443089 [KindSocket] tint=8A8A8A   ← socket disc now here…
…
z29 Usage_Slot_2[5]    0xBED4CF21 [KindSocket]
z36 Node_Icon          (symbol)                              ← …below the symbol
z37 Arrow_Top          [Purchased,NeighbourPurchasableTop]   ← …and below the arrows
z41 Connector_Top      [Purchased,NeighbourPurchasedTop]     ← …and connectors
```

`0x3084D186` no longer appears. **Owner visually validated: "socket looks
good."**

## Acceptance

`ReadParagonNodeRecipe_surfaces_flat_zordered_components` extended: socket disc
z above `Node_IconBase`, below `Node_Icon`/`Arrow_Top`/`Connector_Top`;
`0x3084D186` absent; no `Usage_Slot_*` own-img component. 2/2 recipe tests
green on live build `3.0.2.71886`.

## Observation for the Optimizer

`Usage_Slot_2[0]` (grey-tinted, centred) and `[1]` (untinted, absolute
full-cell) are the **same** handle `0xF6443089`; both are `bActive=1`, so both
draw (the untinted absolute one over the grey centred one). No decoded trigger
distinguishes them — flagged for the owner's eye; emitted faithfully rather
than pruned on a guess.
