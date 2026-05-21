# 0045 — #28: DecodeMip0 BC row-pitch is texture-specific (CL-49)

*2026-05-21*

A library transport bug the Optimizer split out of #26 and gave its own
turn. `DecodeMip0` garbled atlas 447106 (`2DUI_Paragon`, 1208×1464, BC1)
— slanted banding, the classic BC row-stride drift signature.

## Root cause

`DecodeMip0` assumed the stored BC block-row pitch is `Align(width, 64)`.
That holds for most atlases but **not** ones stored 128-aligned:

| atlas | W×H | codec | stored pitch | `Align(W,64)` | bug? |
|---|---|---|---|---|---|
| 447106 | 1208×1464 | BC1 | **1280** (128-aligned) | 1216 | **yes** |
| 2061536 | 1504×1720 | BC3 | 1536 | 1536 | no |
| 1208406 | 4224×192 | BC3 | 4224 | 4224 | no |

For 447106 the decoder read 304 blocks/row when the data has 320 → the
row pointer fell 16 blocks (64 px) short each row → cumulative horizontal
drift = the slant.

## The exact-pitch fix

The stored pitch is **recoverable exactly** from the mip0 byte count,
which the metadata already carries (`SerTex[0].SizeAndFlags`):

```
blocksPerRow = mip0Size / (blockRows × blockSize)
storedPitch  = blocksPerRow × 4
```

For 447106: `936960 / (366 × 8) = 320` blocks → 1280 px (the unique
integer fitting the byte count and ≥ the logical width). The 64-aligned
atlases derive *identically* (their size matches `Align(W,64)`), so the
change fixes 447106 with **zero regression**. Fallback to `Align(W,64)`
+ a `pitch ≥ width` guard when the size is unavailable or doesn't divide
cleanly.

## Corrects an earlier CASC claim (#26)

On #26 (CL-46 era) CASC decoded `0xC1473C21` cleanly and concluded the
consumer's garble was a **consumer-side** decode bug. That was wrong: it
was **this** library bug. CASC's per-frame probe missed it because
`0xC1473C21` is a near-uniform dark square — row drift is invisible on
uniform content (every row is the same dark pixels). The Optimizer's
full-atlas decode, with varied content, exposed the slant. Lesson:
validate a decode-correctness claim on a frame with **structured**
content, not a flat one.

## Verification

`DecodeMip0_uses_stored_row_pitch_for_non_64_aligned_bc1`: 447106 is BC1
with `width % 64 ≠ 0`; the mip0 size implies pitch 1280 ≠ the 1216 the
legacy guess gives; the shipped decode differs from a forced-1216
reference decode (catches a revert) and is more row-coherent. **44/44
green** on build `3.0.2.71886`.
