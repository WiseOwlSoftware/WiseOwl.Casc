# 0010 — 2026-05-17 — FR-C7: the paragon UI-layout format, located

> Narrative source for the wiseowl.com session. Continues 0009.

A new consumer FR arrived: **C7** — reverse-engineer the D4 UI-definition
SNO that drives paragon board/node rendering (cell pitch, disc/symbol/
ornate-frame sizes, per-rarity/per-state texture bindings, anim) and
expose a typed `ParagonRenderLayout`, so the optimizer composites
pixel-faithfully with zero calibrated constants. This deliberately
un-freezes the "B1–B6 + existing" scope — a new owner-directed FR round,
same loop as B1–B6.

Three boundary calls were made up front rather than silently absorbed:
the ask references the pre-split `casc-format.md`; per the spec split
this D4-layer format is documented in `casc-diablo4-format.md` (its own
`CL-*`), and the split is not being re-merged — recorded back to the
consumer. `e:\Paragon` stays strictly read-only, so the RE uses our own
library through a new throwaway `build/SnoScan`, not the consumer's
`snoscan` (which would write build output into `e:\Paragon`). And the
scope un-freeze is logged as intentional, not creep.

The empirical hunt (the CL-4 lesson — probe, don't theorize): enumerate
all 181 CoreTOC groups + format hashes, then eliminate wrong leads with
evidence. Group 63 `Paragon_*Nodes` looked promising by name but the
records are 113-byte tutorial/help triggers. Group 29 `Paragon_*_
Legendary_*` are node *powers*. Groups 1/9/14/27 are the *art* (mesh /
animation / VFX) the consumer already had. Group 42's 1,236 paragon
entries are localized strings. The render *metric* is none of these.

It is a **group-46 UI-scene record** — format hash `0xE4825AB8`, the
family that also holds `ActionBar`, `Armory`, `BuildViewer`,
`BrightnessDialog` (286 UI screens/dialogs). Exactly the "different
UI-definition format we could not identify a typed reader for". The
target is `ParagonBoard` SNO 657304 (145,550 B) and `ParagonBoardSelect`
964599. The container header is proven and common across the family
(0xDEADBEEF + 0x10 SNO header → 0x20 root header with offset/size/count
fields → embedded root name `ParagonBoard_main` → a nested widget tree
of 32-bit texture/style handles delimited by `0xFFFFFFFF`).

The honest beat: that is the FR's *explicit first task* ("locate the
layout SNO group + format") — done and verifiable — but it is **not** C7
delivered. Fully decoding 145 KB of nested UI widget-tree to the field
level is a B1–B6-scale round of its own. §10 + CL-9 state precisely that:
format located and container characterised, field decode in progress,
nothing guessed. Writing a plausible-looking layout now would be exactly
the "fake a pass" failure this project exists to refuse — the same
through-line as the analyzer-honesty correction one devlog earlier.
Next: decode the `0xE4825AB8` widget-node struct + anchor/size encoding
+ per-state binding table, then the typed `ParagonRenderLayout` reader
with a verbatim acceptance matrix.
