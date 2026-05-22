# 0059 — FR-C12 #22: Starter node base disc renders disk-sized

2026-05-22 · CL-61 · branch `fr-c12-start-node-disc`

## Trigger

Owner visual-close on the rebuilt app (relayed via #22): exit/gate node OK,
socket confirmed correct, but the **Start node** reads **disk oversized,
filigree oversized, symbol too small**. The Optimizer flagged it as the FR-C18
rarity-disc rect class applied to `Template_Node_Starter`.

## Ground truth (`SnoScan widgetdump` + new `framesize`)

```
Template_Node_Starter children:
  [0] 0xA0F996FE filigree [L=-18..,W=140]     (explicit 140² overscan)
  [1] 0xF8312CA8 base     [all-zero]          ← the problem
  [3] 0xF8312CA8 base     [all-zero]
Node_IconBase            [L7R7T7B7] vAnc=3     (canonical disc = inset-7 86²)
```

`framesize` (new recon, dogfoods `Catalog.TryResolveFrame`+`TryPeek`):
`0xF8312CA8` native **135²**, `0x1D166DC7` (common disc) native **154²** — yet
the common disc renders **86²**. So **rendered size comes from the authored
rect, not the native frame size**: the all-zero Starter base fell through
`ResolvePlacement` to **full-cell 100²**, ~16% larger than every other node's
86² disc → "oversized."

## Fix (interpretation, general)

A template base child with an **unspecified (all-zero) rect** inherits the
**base-disc inset** — the rarity pair's co-sized inset where present (FR-C18),
else `Node_IconBase`'s inset (7 → 86²) — instead of stretching full-cell.
Generalises FR-C18's co-sizing beyond the rarity pair to the Starter (and any
future template) base.

- **No regression:** explicitly-sized children keep their own rect. The gate
  (`Template_Node_Quest`) handle children are all non-empty (ornate inset-3,
  locator inset-22..26, filigree 140²) → untouched (owner says gate is OK). The
  socket is non-template (separate path). The Starter filigree (`0xA0F996FE`,
  explicit 140²) is unchanged — if it still reads oversized, that's a separate
  authored-140² question for the owner's eye, flagged on #22.
- **No fabrication:** the inset is the authored `Node_IconBase` value, not a
  number picked to match the oracle.

Result: `Template_Node_Starter[1]` `0xF8312CA8` now `rect=(7,7,86,86)`.

## Acceptance

`ReadParagonNodeRecipe_surfaces_flat_zordered_components` asserts the Starter
base at inset-7 / 86². Render sent to the owner for visual-close. 51/51 Diablo4
tests green on `3.0.2.71886`.
