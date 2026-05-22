# 0055 — FR-C20 Catalog iteration 1: handle lookup, atlas facets, typed enumerator

2026-05-22 · CL-56 · branch `fr-c20-handle-facets-typed`

## Trigger

The Optimizer consume-tested CL-55 against the live mine (`Find(null)` →
34,268 assets / 14 kinds; decode-all + exception-safety confirmed) and returned
prioritized, usage-grounded feedback (casc-fr #32). Consensus build order:
**P1 handle reverse-lookup → P2 facets → P4 typed enumerator → P3 frame pixels
→ P5 relationships.** This increment ships P1, P2 (atlas), P4 — the cheap,
high-leverage set.

## Shipped

- **P1 — `Catalog.TryResolveHandle(uint handle, out AssetRef atlas, out int
  frameIndex)`.** The Optimizer's #1 recurring pain was holding raw texture
  handles with no way to discover what they are. This reverses a handle to its
  owning `TextureAtlas` asset + the frame index within it (built on the existing
  `TryGetIconFrame`). Sentinel `0`/`0xFFFFFFFF` return `false` (a `TryGetIconFrame`
  quirk matches a frame whose handle is 0 — guarded).
- **P2 — `Catalog.TryPeek(AssetRef, out AssetFacets)`** + a filterable
  `codec:<codec>` tag on `TextureAtlas` refs. `AssetFacets(int? Width, int?
  Height, int? FrameCount, TextureCodec? Codec)` comes from the **preloaded**
  combined-meta — no pixel decode — so the 4.7k atlases are filterable cheaply.
  Item/power/glyph categorical facets (slot/rarity/class) are **not** decode-free
  (they need the decoded definition or a yet-unidentified authored table); split
  to **P2b** rather than pretend they're cheap.
- **P4 — `Catalog.Find<T>(query)`** — discover-and-decode in one lazy pass,
  yielding decoded `T`, silently skipping non-matching kinds and undecodable
  blobs. The "give me every `TiledStyleDefinition`" shortcut.

## Notes / honesty

- "Decode-free facets" holds for atlases (combined-meta is preloaded). It does
  **not** hold for items/powers — flagged to the Optimizer; P2b will RE a cheap
  source (ItemType group / name convention / a balance table) before claiming a
  free item facet.
- This also folds the FR-T1 (#31) atlas-browser need toward the Catalog: P1 +
  the next P3 (frame pixels) give discover-atlas → peek-frames → retrieve-frame.

## Acceptance

`Catalog_discovers_and_retrieves_assets_by_kind_filter` extended: `Find<T>`
yields a decoded TiledStyle; `TryPeek` returns Bc3 + dims for
`2DUI_SelectionHighlight`; a `codec:` tag is present; `TryResolveHandle(0xBA7D2638)`
→ its atlas + a frame index whose `ImageHandle` round-trips; null handle →
`false`. 51/51 Diablo4 tests green on `3.0.2.71886`.

## Next (per consensus)

P3 frame-pixel retrieval (`TryGetFrameImage(handle/ref)`), then P2b item/power
facets + the Q1/Q2 tag/kind additions, then P5 relationships. Build-stable
`AssetRef` identity (Q4) to be addressed alongside P2b.
