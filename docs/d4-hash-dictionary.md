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
| `0x0204DBB8` | `hTooltipText` | FR-C16 R12 (EXE symbol extract) |
| `0x0789C1CD` | `hText` | FR-C14 R7 |
| `0x0B63D29B` | `hImageFrameMouseOver` | FR-C16 R11 (recombination crack) — per-state image-slot family |
| `0x0C152636` | `hImageFrame` | FR-C12 re-decode (brute force) — node disc-handle field (type `UIImageHandleReference`) |
| `0x0C2AFA21` | `dwAlpha` | FR-C12 re-decode (brute force) — per-widget opacity byte |
| `0x0D75128C` | `hImageFramePressed` | FR-C16 R11 (recombination crack) — per-state image-slot family |
| `0x0DAEFCAA` | `hImageFrameDisable` | FR-C16 R11 (recombination crack) — per-state image-slot family |
| `0x07DB38D3` | `snoTiledStyle` | **FR-C14 R8 — the FR-C14 missing field** (blizzhackers/d4data FieldChecksums) |
| `0x07F1EF79` | `nLeft` | pre-existing (CASC source) |
| `0x093CBAA8` | `eHorizontalAnchoring` | FR-C16 R14 — **corrected** from the FR-C14 mislabel "eGroupType" (real `FieldHash("eGroupType")`=`0x05862894`); caught by the field-hash sanity check |
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

## Class hashes (widget class style ids)

Cracked FR-C16/C17 R3 via `blizzhackers/d4data` `!!D4Checksums.yml`.
These are `UiWidget.ClassId` values (each = a `UI*Style` SNO type).

| Hash | Name | Role |
|---|---|---|
| `0x1E3077C7` | `UIWindowStyle` | the drawable textured-rect widget (most common) |
| `0x112661D5` | `UIStackPanelStyle` | layout/stack container |
| `0x093D303F` | `UIParagonBoardStyle` | the `ParagonNodes` grid container |
| `0x145F2056` | `UIBlinkerStyle` | pulsing glow (`*NodeGlow`, `NodeAvailableGlow`, `*_Tutorial_Highlight`) |
| `0x98D4E83A` | `UIRActorStyle` | 3D / VFX canvas |
| `0x079C2454` | `UITextStyle` | text |
| `0x64A23287` | `UIScrollBoxStyle` | `Glyph_Grid` |
| `0x8A5932F4` | `UIListBoxStyle` | `ParagonStats` |
| `0xC81DED6B` | `UIButtonStyle` | buttons |
| `0x4873BE59` | `UIWrapPanelStyle` | `Glyph_WrapPanel` |
| `0x999CA9A3` | `UIHotkeyStyle` | hotkey prompts |
| `0x0E1C5710` | `UIControlStyle` | base control style |
| `0x42965258` | (class-registry record magic) | FR-C14 R6 (engine RE) |

## Known-uncracked (high-priority targets)

Sorted by appearance frequency across recent FR scene probes. These
are where the next crack delivers the biggest re-decode payoff.

| Hash | Kind | Freq | Notes |
|---|---|---|---|
| `0x0C152636` | `UIImageHandleReference` | 136× | a texture-handle field (the field on `Board_Selector_BG` that *should* hold its bound texture — unbound on that instance; value comes via ExtraLayerValues). Still needs the field *name* cracked. |
| `0x0CDB00E9` | `DT_INT` | 22× | small **signed** ints (not a coordinate/rect field): scene 657304 `{4,5,6,10,15,−6,−4}`, scene 964599 `{0,3,10,15,20,30}` (re-read with the FR-C16 R7 complete grammar). Plausibly a layout offset / spacing / z-bias / anchor-offset; a blind candidate-name brute (≈9k UI-field permutations) did **not** hit it — needs the d4data `FieldChecksums` registry for a confident crack. |
| `0x0C2AFA21` | `DT_BYTE` | 25× | common byte-flag field |
| `0x03445DCD`, `0x08CF4C5D` | `DT_INT` | 1× | singletons in 657304 (value 8); blind brute no-hit |
| `0x0B63D29B`, `0x0D75128C`, `0x0DAEFCAA`, `0x0A2C2344` | `UIImageHandleReference` | 1–2× | rarely-bound texture-handle fields (mostly value 0; `0x0A2C2344` binds real handles `0x5620532A`/`0x7DFC4A3F` in 964599) — names uncracked |

Cracked since the table above was first written (FR-C14 R8/R10),
moved to the field-hash table: `0x093CBAA8 = eHorizontalAnchoring`
(corrected from the FR-C14 "eGroupType" mislabel — FR-C16 R14),
`0x03D55658 = eVerticalAnchoring`, `0x07DB38D3 = snoTiledStyle`.

## Known-uncracked types (high-priority)

| Hash | Notes |
|---|---|
| `0xE549F591` | Less common; 2 fields observed |
| `0x2B0285C0` | 1 field observed |

## Gbid hashes (lowercased DJB2)

`GbidHash` is the lowercased-input DJB2 (`Diablo4.GbidHash`). Used for
the per-attribute `ParamPlus12` field on `NodeAttribute` /
`GlyphAffixAttributeRef` when the attribute is tag-conditional
(`AttributeId 259` = `DamageBonusTag`, `AttributeId 290` =
`CritDamageBonusTag`, etc.). The cracked-name convention is
`Skill_<TagName>` — every cracked entry below was discovered via FR-C28
in-process brute-force against the affix / node tuple data, anchored to
the FR-C24 oracle (Demonology = `0x32ABA6FB` on `Warlock_Rare_006`).

| Hash | Name | Source / first observed |
|---|---|---|
| `0x0F479E53` | `Skill_Incarnate` | FR-C28 CL-85 brute-force; `Druid` |
| `0x12674CDC` | `Skill_Cutthroat` | FR-C28 CL-85 brute-force; `Rogue` |
| `0x32ABA6FB` | `Skill_Demonology` | FR-C28 CL-85 — FR-C28 anchor (`Warlock_Rare_006`) |
| `0x5FCEB9D4` | `Skill_Grenade` | FR-C28 CL-85 brute-force; `Demon Hunter` |
| `0x6625AC6B` | `Skill_Shapeshifting` | FR-C28 CL-85 brute-force; `Druid` |
| `0x6A1F0A80` | `Skill_Abyss` | FR-C28 CL-85 brute-force; `Warlock` |
| `0x6A3673AE` | `Skill_Blood` | FR-C28 CL-85 brute-force; `Necromancer` |
| `0x6B67A5C3` | `Skill_Shade` | FR-C28 CL-85 brute-force |
| `0x6D657409` | `Skill_Hellfire` | FR-C28 CL-85 brute-force; `Warlock` |
| `0x8C8DF55A` | `Skill_Juggernaut` | FR-C28 CL-85 brute-force |
| `0xCEAEA388` | `Skill_Occult` | FR-C28 CL-85 brute-force; `Warlock` |
| `0xD5D1FA40` | `Skill_Recast` | FR-C28 CL-85 brute-force |
| `0xE4303EA2` | `Skill_Bone` | FR-C28 CL-85 brute-force; `Necromancer` |
| `0xE43A2895` | `Skill_Trap` | FR-C28 CL-85 brute-force; `Rogue` |
| `0xE43BC256` | `Skill_Wolf` | FR-C28 CL-85 brute-force; `Druid` |
| `0xE4B9B478` | `Skill_Marksman` | FR-C28 CL-85 brute-force; `Rogue` |
| `0xE87A54CD` | `Skill_Zealot` | FR-C28 CL-85 brute-force |
| `0xF4EE66C7` | `Skill_Mobility` | FR-C28 CL-85 brute-force; `Rogue` |
| `0xFFFA158B` | `Skill_Disciple` | FR-C28 CL-85 brute-force |

**Empirically named (uncracked GBID, name derived from affix /
node sno-name)**: the curated CL-85 `LabelByCompoundKey` map covers
~30 additional tags whose engine-internal hash key was not cracked
(Archfiend / Conjuration / Companion / Corpse / Earthquake / etc. —
their GBIDs do **not** match the `Skill_<Name>` pattern, so the key is
something the brute-force grid didn't cover). The display label is
still trustworthy — every affix's sno-name carries the tag name as
a leading prefix and every `Generic_Magic_Damage<Tag>` node confirms
the same name. Future hash-decode passes may crack the engine-internal
keys and migrate those rows in.

(`0x6B1C5D9C` cracked R10 = `UIImageHandleReference`.)

## Skill-tree node-name hash (g39 class-board records)

The node id in the g39 class skill-tree graph (e.g. `Rogue`=199278, node record
field `[1]`) is **seed-0 DJB2 of the lowercased node name** — the same
`SkillTreeRewards` (g20/547685) node name (`rogue_unlock_bladeshift`, …). Distinct
from `GbidHash` (seed-5381 lowercased). Verified: maps 192/270 Rogue nodes; the
78 unmapped are connector/hub nodes with no reward record. Recon: devlog 0105.
