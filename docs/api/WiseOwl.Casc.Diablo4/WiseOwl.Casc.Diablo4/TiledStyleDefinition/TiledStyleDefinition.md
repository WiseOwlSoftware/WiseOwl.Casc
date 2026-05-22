# TiledStyleDefinition constructor

A decoded Diablo IV UI tile-style record (`.uis`, SNO group UiStyle = 103) — the engine's recipe for rendering a tiled UI overlay (vignette, inner-shadow, bag background, frame chrome, …) as a composition of texture pieces with a scale factor and padding. FR-C14 Phase 4 (CL-42).

```csharp
public TiledStyleDefinition(int SnoId, uint TypeTag, string VariantName, float ImageScale, 
    uint SourceImageHandle, uint SliceStyle, int TileCenter, int TileHorizontalBorders, 
    int TileVerticalBorders, uint NPadding, bool HasPartialDecode, IReadOnlyList<uint> WindowPieces)
```

| parameter | description |
| --- | --- |
| SnoId | The record's own SNO id (self-reference at +0x10). |
| TypeTag | The polymorphic-variant tag at +0x50. `0xBC0D579E` is the most-common variant observed (7 of 8 dumped records); `0x02E46583` appears on at least one record ([`SnoId`](./SnoId.md) 603760 BagBackground). The exact variant name for each tag is a future-cracked TypeHash. |
| ImageScale | The `flImageScale` field at +0x58 — the engine's per-record scale factor for the composed tile. Observed values 1.0, 0.5, 0.9 across the dumped records. |
| HasPartialDecode | `true` when the record's trailing variant-specific bytes (additional piece handles, sub-rects) were not fully decoded by the current [`Parse`](./Parse.md) implementation. Consumers that need the full composition should treat the absent fields as "unknown" rather than "zero". |
| VariantName | The decoded polymorphic-variant name for [`TypeTag`](./TypeTag.md) — `"NSlice"`, `"TiledWindowPieces"`, `"HorizontalTiledWindowPieces"`, `"VertTiledWindowPieces"`, `"WindowPieces"`, or `"0xHHHHHHHH?"` for an unrecognised tag. Cracked from the `blizzhackers/d4data` `!!D4Checksums.yml` type registry (FR-C14 R10). |
| SourceImageHandle | The `hSourceImage` field (NSlice +0x18) — the texture handle the engine slices/tiles. Resolves via the icon-frame index. `0u` when absent. |
| SliceStyle | The `eSliceStyle` enum (NSlice +0x1c) — the engine's slicing mode. Surfaced raw (its enum-value names are not yet cracked); `0` is the common "default" mode. |
| TileCenter | The `fTileCenter` flag (NSlice +0x40) — non-zero ⇒ the centre region is *tiled* (repeated) rather than stretched. This is the field that decides whether the interior pattern repeats across the rect. `-1` when the variant suffix wasn't decoded (see [`HasPartialDecode`](./HasPartialDecode.md)). |
| TileHorizontalBorders | The `fTileHorizontalBorders` flag (NSlice +0x44) — non-zero ⇒ the top/bottom border strips are tiled rather than stretched. `-1` when undecoded. |
| TileVerticalBorders | The `fTileVerticalBorders` flag (NSlice +0x48) — non-zero ⇒ the left/right border strips are tiled rather than stretched. `-1` when undecoded. |
| NPadding | The `nPadding` field (NSlice +0x14) — inter-piece padding in the composition. |
| WindowPieces | FR-C14 R10 / FR-C19 — for the `TiledWindowPieces` variant, the 9 piece handles of the 9-slice, at blob `+0x60..+0x80` in row-major 3×3 order: `[TL, T, TR, L, C, R, BL, B, BR]` (index 4 = the centre fill). Each is a texture frame handle (resolve via the icon-frame index); compose the 9-slice by drawing the corners at native size (scaled by [`ImageScale`](./ImageScale.md)) in the cell corners, the edges stretched between them, and the centre filling the interior. Empty for non-window-pieces variants (use [`SourceImageHandle`](./SourceImageHandle.md) + the NSlice fields). |

## Remarks

The mechanism: a UI widget can carry a `DT_SNO` field named `snoTiledStyle` ([`FieldHash`](../Diablo4/FieldHash.md)(`"snoTiledStyle"`) = 0x07DB38D3) pointing to one of these records. At render time the engine consults the bound tile-style and composes the overlay from the style's piece handles + image-scale + padding. Distinct from the per-widget `hImage` texture handle, which is the primary content; `snoTiledStyle` defines the *framing/composition* applied to the widget's rect.

Record layout (verified across 8 dumped SNOs from scene 657304's `snoTiledStyle` bindings: 843662, 1309282, 872641, 787949, 603760, 792649, 1841254, plus 20/3/1 sentinel values):

```csharp
+0x00  uint32   magic        = 0xDEADBEEF
+0x04  byte[12] header pad   = 0
+0x10  int32    SnoId        (self-reference)
+0x14  byte[12] pad          = 0
+0x20  uint32   field_off    (typically 0x30 — payload start)
+0x24  uint32   field_size   (typically 0x58 — payload size)
+0x28  uint32   field_count  (typically 1)
+0x2C  byte[36] zero
+0x50  uint32   TypeTag      (variant identifier — 0xBC0D579E for
                              HorizontalTiledWindowPieces-like;
                              0x02E46583 for a different variant
                              observed on BagBackground 603760)
+0x54  uint32   pad          = 0
+0x58  float32  flImageScale (the documented scaling factor —
                              observed values 1.0f, 0.5f, 0.9f)
+0x5C  uint32   pad          = 0
+0x60  uint32   PrimaryHandle (the primary texture handle —
                               hPieceMiddle in the 3-slice variant,
                               observed varying per record)
+0x64..  variable-length per-variant suffix (sub-rects, padding
         spec, additional piece handles for 9-slice — only
         partially decoded as of FR-C14 R9; honest sentinel via
         )
```

The full multi-piece structure (hPieceLeft/hPieceRight/hPieceTop/ hPieceBottom plus per-piece sub-rects) is encoded in the trailing bytes per the `HorizontalTiledWindowPieces` / `VerticalTiledWindowPieces` schemas published by `blizzhackers/d4data`. The variant is selected via [`TypeTag`](./TypeTag.md). FR-C14 R9 surfaces the verified primary handle + image scale + type tag; future iterations will decode the variant suffixes per the cumulative-hash-decode principle (memory `feedback_cumulative-hash-decode`).

## See Also

* record [TiledStyleDefinition](../TiledStyleDefinition.md)
* namespace [WiseOwl.Casc.Diablo4](../../WiseOwl.Casc.Diablo4.md)

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.Diablo4.dll -->
