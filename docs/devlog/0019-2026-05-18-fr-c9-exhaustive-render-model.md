# 0019 — FR-C9: exhaustive render-model + structural coverage gate

*2026-05-18*

The consumer's diagnosis was correct: FR-C8's nine rounds were one bug
— a lossy, non-self-validating scene decode — not unknown resources.
FR-C9 asked to make completeness structural so there is no R10/R11.

## Recon found a still-unmodelled binding class

A `rendercover` recon (every 4-aligned atlas-resolvable u32 in the raw
scene vs what `ReadUiScene` surfaces) showed **21 dropped in 657304,
15 in 964599** even after CL-23/24/25. The large/handle-magnitude ones
(grey ring `0x87A89F86`, `0x0863BBAD`, `0x2954DF0C`, …) were all the
`0x58` bound-layer block — but the CL-23 model over-fit two examples
(required `ownerClassId@+0x20` + `0xFFFFFFFF@+0x28`). Those words are
not universal, and a widget's last block straddles the next
`nameStart`. Same lesson as CL-24, ungeneralised.

## Fix (CL-26) — lossless, then gated

`UiScene.Parse` Pass-2c relaxed to the only stable marker
`tag==2, +4==0, value@+8`, bounded on the value field (no straddle
drop). After: every handle-magnitude atlas binding is surfaced
(remaining "dropped" are tiny field ints `≤0x683` that the catalog
leniently resolves — excluded structurally by a `≥0x10000`
handle-magnitude filter, since real D4 handles are 32-bit hashes).

Shipped:
- `Diablo4Storage.ReadParagonRenderModel()` → `ParagonRenderModel`
  (`Layout` + `Scenes` for 657304/964599; every binding widget with
  `{handle, rect, alpha}`). One-shot exhaustive manifest (#1/#3).
- `IsParagonTextureHandle` — one shared structural test (magnitude +
  catalog) used by the projection, the model, and the gate.
- `ParagonRenderModel_covers_every_bound_atlas_handle` — the **shape-
  agnostic coverage gate** (#2): scans every 4-aligned u32 in both raw
  scenes; asserts every structural texture-binding is in the model.
  Green; the grey ring is now among the recovered. A future
  projection/parse gap fails casc CI, not consumer eyeballs.
- Spec §10.14 (published binding-record schema — the two value shapes
  — + the losslessness guarantee) + Appendix A CL-26.
  `docs/fr-c9-response.md`. Full suite 39 green, 0 warnings; API docs
  regenerated.

Boundary unchanged (consumer owns role/state classification). The
consumer's planned `audit` tool now diffs against a contract the gate
guarantees complete — only classification deltas, never discovery
gaps. **Not released** — on `main`, batched into a future owner-cut
release per the owner (memory `feedback_release-cadence`).
