# 0032 — FR-C12 R5: socket.socketed inner-well restored (CL-36)

*2026-05-20*

One-item follow-up to FR-C12 R3 / CL-35. The R3 ask had three
visual-oracle items pending; R5 carries the consumed owner answers
back. Two answers landed consumer-side (bead-ring pulse amplitude
tuning + `.selected` static-confirm) without a library delta; the
third — whether the inner spike-frame `0x23F487F3` stays on
`.socketed` — required a library row correction.

## Owner visual oracle (relayed by Optimizer)

> *"Socketed looks exactly like a selected node — with the placed
> glyph icon additionally overlaid."*

CL-35 had dropped `0x23F487F3` from `socket.socketed` "pending
visual-oracle confirmation of socketed-state inner-frame behavior".
Confirmation is now in: the inner well stays. The three-layer
composite (`outerDisk → beadRing → innerWell`) is **identical
across all three socket states**; what differs is per-state
activation policy (consumer-side), not the layer inventory
(library-side).

## Fix

```csharp
states.Add(new StateElements(-1, "socket.socketed",
    L(socketOuterDisk, pulseSocketed, socketInnerWell),  // +socketInnerWell
    null, null, null));
```

The bead ring on `.socketed` continues to bind via
`GlyphNodeGlow_Purchased` (the per-state widget — same `0xBED4CF21`
handle as `GlyphNodeGlow_Revealed`'s binding on `.unselected` /
`.selected`).

## Per-state policy stays consumer-side

Boundary preserved (FR-C7 §6): library = decode-true layer
inventory; consumer = per-state activation policy. The three socket
states share the same inventory; per-state animation/overlay
behaviour is the consumer's:

| State | Layers (library) | Animation (consumer) |
|---|---|---|
| `.unselected` | outerDisk + beadRing + innerWell | bead-ring pulse 0.15↔1.0 sine, 4 s |
| `.selected` | outerDisk + beadRing + innerWell | bead-ring static @ opacity 1.0 |
| `.socketed` | outerDisk + beadRing(Purchased) + innerWell | static @ 1.0 + placed-glyph icon overlay at inner-well centre depression |

The placed-glyph-icon overlay for `.socketed` (sourcing the glyph
sprite from `ParagonNodeDefinition.HIconMask` per the optimizer's
"which glyph in which socket" allocation state) is downstream
consumer work — not blocked on CASC.

## Gate hygiene

The row no-phantom gate (CL-35) accepts the change unchanged:
`0x23F487F3` is bound on `Usage_Slot_2`'s 0x58-block, and
`Usage_Slot_2` is in the socket-authorized widget set
(`{GlyphNodeGlow_Revealed, GlyphNodeGlow_Purchased, Usage_Slot_2}`).
Same widget already authorized the layer on `.unselected` /
`.selected`; CL-36 just lets it appear on `.socketed` too. The row-
completeness gate (CL-34) is also satisfied — the handle already
appeared in two rows.

## Deliverable

- `src/WiseOwl.Casc.Diablo4/ParagonRenderLayout.cs`: one-line
  addition to `socket.socketed` row + comment refresh.
- `docs/casc-diablo4-format.md` §10.17 narrative consolidated
  (the three socket rows now share a row in the table) + new
  CL-36 entry.
- 40 / 40 tests green on `D:\Diablo IV` build `3.0.2.71886`. The
  row no-phantom gate (CL-35) passes the addition.
