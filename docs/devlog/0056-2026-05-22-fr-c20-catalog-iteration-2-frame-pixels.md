# 0056 — FR-C20 Catalog iteration 2: frame-pixel retrieval (P3)

2026-05-22 · CL-57 · branch `fr-c20-frame-pixels`

## Trigger

The Optimizer consume-verified CL-56 (P1/P2/P4) against the probe — no blocking
friction — and named **P3 its critical path**: it unblocks the #30
selection-cursor dogfood (resolve handle → atlas/frame → pixels → draw topmost)
*and* completes the #31 atlas-browser loop (discover → peek → retrieve pixels).
It also asked for one small convenience: a `TexFrame` directly from a handle.

## Shipped

- **P3 `Catalog.TryGetFrameImage(uint handle, out DecodedImage)`** — resolve the
  handle to its atlas + frame, decode the atlas mip0, crop to the frame's
  `PixelRect`. The "what does this handle look like?" path.
- **`Catalog.TryGetAtlasImage(AssetRef, out DecodedImage)`** — whole atlas mip0
  (decode-once; the browser crops frames itself via `Frames[i].PixelRect`),
  avoiding a per-frame re-decode.
- **`Catalog.TryResolveFrame(uint handle, out AssetRef atlas, out TexFrame frame)`**
  — the requested convenience: the `TexFrame` (UV rect) directly, no
  `TryGet(atlas)`+index step.

All exception-safe: only BC1/BC3 decode today (≈99% of UI atlases per the
codec scan); unsupported codec / absent payload / non-atlas ref → `false`
(never throws). Built on the existing `TextureDecoder.DecodeMip0` +
`DecodedImage.Crop` + `TryGetIconFrame`.

## Probe corroboration (from the Optimizer)

P1 reverse-lookup resolved the handles that cost FRs this session
(`0x95DA4E78`→585030#6, `0xBA7D2638`→337357#10, `0xF6443089`/`0x1D166DC7`→
2061536). The **rim handles `0x900C7D87`/`0x225F2DA8` correctly returned
`false`** — confirming #24's fire-rim border is genuinely non-atlas /
engine-internal, not catalog-pullable. P2 classified all 4,726 atlases by codec
(bc1 2936 / bc3 1753 / rgba8 10 / bc7 9 …) with zero pixel decode.

## Acceptance

`Catalog_discovers_and_retrieves_assets_by_kind_filter` extended:
`TryGetFrameImage(0xBA7D2638)` → RGBA of length `w*h*4`; `TryGetAtlasImage` →
atlas-width image; sentinel handle → `false`; `TryResolveFrame` round-trips the
handle. 51/51 Diablo4 tests green on `3.0.2.71886`.

## Next (per consensus)

P2b (item/power/glyph categorical facets — needs a cheap authored source: the
ItemType group / name convention / a balance table) + Q2 sort/valid-only + Q4
build-stable `AssetRef` identity. The non-BC1/BC3 codecs (bc7/bc2/bc4/bc6h/
rgba8/rgba16f tail, ≈0.8% of atlases) remain undecoded — surface only if a
consumer hits one.
