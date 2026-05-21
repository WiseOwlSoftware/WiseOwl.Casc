# D4 Hash Dictionary

> Cumulative DJB2 hash crack registry. Per
> [`feedback_cumulative-hash-decode`](../../../../C:/Users/brent/.claude/projects/E--Casc/memory/feedback_cumulative-hash-decode.md):
> every cracked hash is added here, and every new crack triggers a
> re-decode pass over previously-opaque blob/scene data. Don't wait for
> a specific search — the multiplicative win is automatic.

All hashes are seed-0 DJB2. `FieldHash` is 28-bit (lower 28 bits of
the 32-bit DJB2); `TypeHash` is the full 32-bit. See `Diablo4.cs` for
the implementations.

## Field hashes (28-bit)

| Hash | Name | Source / first observed |
|---|---|---|
| `0x003DC5C1` | `nTop` | FR-C14 R7 |
| `0x00957CB7` | `rgbaForeground` | FR-C14 R7 |
| `0x02330CBF` | `hImageFrameIcon` | FR-C14 R8 (blizzhackers/d4data FieldChecksums) |
| `0x02D88AE7` | `nHeight` | FR-C14 R7 |
| `0x03D55658` | `eVerticalAnchoring` | FR-C14 R8 (blizzhackers/d4data FieldChecksums) |
| `0x056F24F5` | `hImageFrameIconPressed` | FR-C14 R8 (blizzhackers/d4data FieldChecksums) |
| `0x05A90F13` | `hImageFrameIconDisable` | FR-C14 R8 (blizzhackers/d4data FieldChecksums) |
| `0x0594CC83` | `nBottom` | FR-C14 R7 |
| `0x069EA64C` | `nRight` | FR-C14 R6 (brute force) |
| `0x06AB76DE` | `bActive` | FR-C14 R7 |
| `0x06F9158E` | `nWidth` | pre-existing (CASC source) |
| `0x0789C1CD` | `hText` | FR-C14 R7 |
| `0x0C152636` | `hImageFrame` | FR-C12 re-decode (brute force) — node disc-handle field (type `UIImageHandleReference`) |
| `0x0C2AFA21` | `dwAlpha` | FR-C12 re-decode (brute force) — per-widget opacity byte |
| `0x07DB38D3` | `snoTiledStyle` | **FR-C14 R8 — the FR-C14 missing field** (blizzhackers/d4data FieldChecksums) |
| `0x07F1EF79` | `nLeft` | pre-existing (CASC source) |
| `0x093CBAA8` | `eGroupType` | FR-C14 R8 (blizzhackers/d4data FieldChecksums) |
| `0x09A3F17B` | `rgbaTint` | pre-existing (CASC source) |

## Type hashes (full 32-bit)

| Hash | Name | Source / first observed |
|---|---|---|
| `0x02E46583` | `TiledWindowPieces` | FR-C14 R10 (blizzhackers/d4data D4Checksums) |
| `0x02F5672C` | `TiledStyleDefinition` | FR-C14 R10 (blizzhackers/d4data D4Checksums) |
| `0x1332C78D` | `DT_BINDABLEPROPERTY` | pre-existing (CASC source) |
| `0x3D4646AB` | `DT_BYTE` | FR-C14 R7 |
| `0x3D47BD2C` | `DT_ENUM` | FR-C14 R6 (brute force) |
| `0x5943238D` | `HorizontalTiledWindowPieces` | FR-C14 R10 (blizzhackers/d4data D4Checksums) |
| `0x6B1C5D9C` | `UIImageHandleReference` | FR-C14 R10 (blizzhackers/d4data D4Checksums) |
| `0x6BFED904` | `VertTiledWindowPieces` | FR-C14 R10 (blizzhackers/d4data D4Checksums) |
| `0x8E00F391` | `WindowPieces` | FR-C14 R10 (blizzhackers/d4data D4Checksums) |
| `0xA4C42E02` | `DT_INT` | pre-existing (CASC source) |
| `0xA4C45887` | `DT_SNO` | FR-C14 R6 (brute force) |
| `0xBC0D579E` | `NSlice` | **FR-C14 R10 — the TiledStyle 9-slice variant** (blizzhackers/d4data D4Checksums) |
| `0xC5A830EC` | `WindowPiecesBase` | FR-C14 R10 (blizzhackers/d4data D4Checksums) |

`NSlice` struct fields (from `!NSlice.bc0d579e.yml`, all added to
`Diablo4.KnownFieldNames` via `FieldHash`): `flImageScale`,
`nPadding`, `hSourceImage`, `eSliceStyle`, `fTileCenter`,
`fTileHorizontalBorders`, `fTileVerticalBorders`. `TiledStyleDefinition`
fields: `ptWindowPiece`, `hOptionalTiledStyleFields`.

## Class hashes (widget classes)

| Hash | Inferred role | Source / first observed |
|---|---|---|
| `0x1E3077C7` | "draw textured rect" — used by `Template_Board_Background_*`, `Background`, `Framing`, `Vignette`, `Divider` (12,682 instances) | FR-C14 R6 (engine RE) |
| `0x112661D5` | Stack/Layout container — used by `Layout_Stack`, `ControllerStack`, `Board_Selector_BG`, `Spirit_Selector` (3,717 instances) | FR-C14 R6 (engine RE) |
| `0x42965258` | Class-registry record magic | FR-C14 R6 (engine RE) |

## Known-uncracked (high-priority targets)

Sorted by appearance frequency across recent FR scene probes. These
are where the next crack delivers the biggest re-decode payoff.

| Hash | Kind | Freq | Notes |
|---|---|---|---|
| `0x0C152636` | `UIImageHandleReference` | 136× | a texture-handle field (the field on `Board_Selector_BG` that *should* hold its bound texture — unbound on that instance; value comes via ExtraLayerValues). Still needs the field *name* cracked. |
| `0x0CDB00E9` | `DT_INT` | 32× | a coordinate-ish field (not nTop/nBottom/nWidth/nHeight) |
| `0x0C2AFA21` | `DT_BYTE` | 25× | common byte-flag field |

Cracked since the table above was first written (FR-C14 R8/R10),
moved to the field-hash table: `0x093CBAA8 = eGroupType`,
`0x03D55658 = eVerticalAnchoring`, `0x07DB38D3 = snoTiledStyle`.

## Known-uncracked types (high-priority)

| Hash | Notes |
|---|---|
| `0xE549F591` | Less common; 2 fields observed |
| `0x2B0285C0` | 1 field observed |

(`0x6B1C5D9C` cracked R10 = `UIImageHandleReference`.)
