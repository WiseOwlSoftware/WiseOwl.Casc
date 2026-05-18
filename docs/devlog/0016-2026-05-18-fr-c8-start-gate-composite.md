# 0016 — FR-C8: start/gate composite layers (FR-C7 decode gap)

*2026-05-18*

The consumer's runtime compositor worked for rarity/socket nodes off
the FR-C7 typed render layout, but `start.*`/`gate.*` States decoded as
disc-only while the game clearly composites an ornate medallion. FR-C8
asked: locate the per-node-type composite in the data, or confirm it
data-silent.

## Recon

The consumer's read was that none of the six confirmed handles appear
in `ReadUiScene(657304/964599)`. A **raw-byte scan** of SNO 657304 told
a different story: the four *frame* handles **are physically present** —
`0xA0F996FE` ×2, `0xF8312CA8` ×2, `0x0E6B6249`, `0xC2DF4786` — at
offsets inside the `Template_Node_Starter` (0x15720) and
`Template_Node_Quest` (0x15FE0) widget spans. The two *symbol* handles
(`0x35B6E536`, `0xE1316816`) are absent — correct, those are the
per-node `HIconMask`.

So **#2 located, not data-silent**, and an FR-C7 correction. Decoding
the surrounding bytes showed the start/gate layers bind via a fixed
**0x58-byte block** (tag@+0=2, value@+8, ownerClassId@+0x20,
`0xFFFFFFFF`@+0x28) reached through a ~0x28-stride descriptor table —
**not** the 56-byte `0x22` instance record §10.3/FR-C7 modelled. The
`0x22` scan never matched it, so `Project()` fell back to the neutral
disc and the raw graph showed the templates near-empty (the consumer's
"1 of 17 fields"). Decoded ordered handles match the owner-verified
atlas oracle exactly: Start = filigree `0xA0F996FE` → hexagon
`0xF8312CA8`; Gate = filigree → ornate square `0xC2DF4786` (selected) /
`0x0E6B6249` (unselected).

## Delivery

- `UiScene.Parse` Pass-2c: per widget, capture the ordered 0x58-block
  values (`UiWidget.ExtraLayerValues`) — lossless raw, scope-B.
- `ParagonRenderProjection`: `start.*`/`gate.*` `States.Layers` built
  from `Template_Node_Starter`/`_Quest` `ExtraLayerValues`, validated
  against the texture catalog (via `ReadParagonRenderLayout`) so the
  0x58 int params (e.g. `20`) are excluded — no fabricated layers.
- Honest residual: per-layer rect/scale, the shader brightness pass,
  and the exact unselected↔selected ornate-square state binding are
  located-not-pinned → left default, consumer-owned (FR-C7 §6
  precedent). The symbol stays the per-node `HIconMask`.
- Spec §10.12 + Appendix A CL-23 (an FR-C7 correction); test
  `ReadParagonRenderLayout_decodes_start_gate_composites` vs live
  `3.0.2.71886`; full suite green (37 pass, 0 skipped, 0 warnings).
  Response: `docs/fr-c8-response.md`.

**Release status.** `0.2.0-alpha` was published to nuget.org on
2026-05-18 from PR #15 (commit `ce9f778`) — it does **not** contain
FR-C8 (PR #16 landed after the tag). That published surface (FR-C7,
FR-D1/D2/D3, FR-14, C6) is now frozen by NuGet immutability. FR-C8
stays on `main`, unreleased; per the owner it is **not** released on
its own — it batches into a future owner-cut release (no single-fix
packages). Its contract is amendable until that release.
