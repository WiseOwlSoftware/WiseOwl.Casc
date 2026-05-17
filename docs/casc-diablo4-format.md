# Diablo IV CASC layer — byte-format reference

The Diablo IV **SNO / container / record** formats that sit on top of the
generic CASC transport, as implemented by the `WiseOwl.Casc.Diablo4`
package. Self-contained and implementable from this document together with
its transport companion.

> **Depends on:** [`casc-format.md`](casc-format.md) (CASC/TACT/TVFS/BLTE
> transport). This document assumes a TVFS path can be resolved to bytes;
> it specifies how Diablo IV *uses* that transport.

---

## 1. Scope & status

- **Specifies:** the SNO file wrapper and addressing; the `0xBCDE6611`
  CoreTOC role; the `0xABBA0003` shared-payload mapping; the `0x44CF00F5`
  combined-meta container family and its two instantiations
  (TextureDefinition, StringList); the paragon record layouts
  (ParagonBoard/Node/Glyph/GlyphAffix); and the GameBalance
  `AttributeFormulas` table.
- **Status:** clean-room; every fact below was verified empirically
  against Diablo IV build `3.0.2.71886` (`.build.info` Build Key
  `522f2f30f1eb0e32af225966b8ac91d1`). Appendix D gives the re-verify
  trigger; Appendix B maps each item to its provenance.
- **Authority.** This document plus `casc-format.md` are jointly the
  canonical byte-format reference. The originating
  `e:\Paragon\docs\d4-binary-formats.md` §3–§8.15 is **superseded for
  byte layouts** (frozen, retained only as project history / article
  source).
- **Policy carve-out (referenced, never absorbed).** Consumer
  interpretation/policy stays authoritative in `e:\Paragon`: the 6
  calibrated engine-intrinsic power-budget multiplier *values*, the
  scoring/objective model, the icon relight/composite calibration, and
  the app's bundled-JSON schema. This library decodes **raw fields
  only** — it never evaluates formulas, scales, scores, or emits app
  resources (Appendix C).

## 2. Conventions

Inherits §2 of `casc-format.md` (endianness, integer notation). Adds:

- **SNO blob.** Content read for a SNO is a blob that begins with a
  16-byte `SNOFileHeader`; the **payload base is `0x10`**. *All* record
  field offsets and `DT_VARIABLEARRAY` `dataOffset`s in §§7–8 are
  measured from the payload base unless stated otherwise.

```
SNO blob
0x00 ┌────────────────────────────────────────────┐
     │ SNOFileHeader (16 bytes)                    │
     │  0x00 u32 dwSignature   (= 0xDEADBEEF)       │
     │  0x04 u32 dwFormatHash  (0 ⇒ CoreTOC group) │
     │  0x08 u32 dwDummy                            │
     │  0x0C u32 dwXMLHash                          │
0x10 ├────────────────────────────────────────────┤  ◄── payload base
     │ payload   (record fields; SNO Id at +0)      │
     └────────────────────────────────────────────┘
```

### 2.1 DT primitive encodings (record fields)

Field offsets in §§7–8 are payload-relative; read at `0x10 + offset`.

| DT type | Encoding |
|---|---|
| `DT_INT` / `DT_UINT` / `DT_DWORD` / `DT_SNO` / `DT_ENUM` / `DT_GBID` | 4-byte LE (`DT_GBID` `0xFFFFFFFF` = null) |
| `DT_FLOAT` | 4-byte IEEE-754 LE |
| `DT_CHARARRAY[n]` | inline NUL-terminated ASCII, `n` bytes reserved |
| `DT_STRING_FORMULA` | 32-byte struct: `i64 pad; i32 srcOffset@+8; i32 srcSize@+12; i32 compiledOffset@+16; i32 compiledSize@+20`. Text = ASCII at payload `srcOffset`, `srcSize` bytes (strip NUL/trim) |
| `DT_VARIABLEARRAY` (record form) | `i64 pad; i32 dataOffset@+8 (payload-relative); i32 dataSize@+12`. Element count = `dataSize / elementStride`; no count field |
| `DT_POLYMORPHIC_VARIABLEARRAY` | `i64 pad; i32 dataOffset@+8; i32 dataSize@+12; i32 count@+16; i32 pad2@+20`. An 8-byte type tag precedes the element struct |

> The `0x44CF00F5` combined-meta container uses a *different*
> variable-array descriptor — see §6, not this table.

## 3. SNO addressing

A Diablo IV SNO is addressed through TVFS by the path:

```
<prefix>\<Folder>\<id>[-<subId>]
  prefix : "Base"
  Folder : Meta | Payload | PayLow | PayMed | Child
  id     : the decimal SNO id (no group folder, no name, no extension)
  subId  : appended as "-<subId>" for child sub-blobs
```

Empirically: `Base\Meta\2458674` resolves; the name-path
`Base\Meta\108\Paragon_Warlock_00.pbd` and the colon form
`base:meta\2458674` both miss (Appendix A, CL-4). An empty/absent
`Payload` follows the shared-payload alias (§5).

`CoreTOC` (§4) supplies the name↔id↔group mapping and the per-group
format hash; it is **not** needed for addressing (id-only).

## 4. CoreTOC (`0xBCDE6611`)

The master directory. Magic `0xBCDE6611`. It supplies, per SNO:
name ↔ id ↔ group, and a per-group format hash (the value used when a
blob's `dwFormatHash` is 0, §2). The full TVFS tree (1,759,690 entries on
the verified build; all 37 nested `vfs-N` sub-manifests descended)
resolves without it; CoreTOC is consulted only for name/group lookups.

> Detailed CoreTOC record layout is implemented in the library
> (`CoreToc`) and is **not duplicated here**; only the facts the format
> reference depends on (magic, role) are stated, per the "do not
> invent" rule.

## 5. Shared-payload mapping (`0xABBA0003`)

`CoreTOCSharedPayloadsMapping.dat`. When a SNO's own `Payload` is
empty/absent, its bytes physically live under another SNO; this maps the
requesting id to the holder id.

| Offset | Type | Field |
|---|---|---|
| 0 | i32 | magic (`0xABBA0003`) |
| 4 | i32 | `count` |
| 8 | entry[`count`] | `{ i32 snoId ; i32 sharedSnoId }` (8 bytes each) |

Verified build: `count` = 35,616. Resolution is a **transparent
fallback**: read `Base\Payload\<id>`; if empty/absent and the mapping
has `id`, read `Base\Payload\<sharedSnoId>` instead.

`CoreTOCReplacedSnosMapping.dat` is **not** implemented — not needed on
the verified build (every paragon/board/node/GameBalance id and the
paragon atlases resolve without it); gated on a future need (Appendix A,
CL-6).

## 6. The `0x44CF00F5` combined-meta container family

A consolidated container holding many per-SNO definition bodies. **Two
instantiations** share the same index but differ in per-entry placement:
**Texture** (§6.2) and **StringList** (§6.3).

### 6.1 Container index

```
+0          u32  magic = 0x44CF00F5
+4          u32  count
+8          count × { i32 sno ; u32 size }      ; index, in body order

prevEnd = 8 + count*8                            ; end of index
for i in 0 .. count-1:
    (placement depends on instantiation — see §6.2 / §6.3)
    advance prevEnd by size[i]
```

```
0x00 ┌───────────────┐
     │ magic u32      │ 0x44CF00F5
0x04 │ count u32      │
0x08 ├───────────────┤
     │ index:         │  count × { i32 sno ; u32 size }
     │  {sno,size} ×n │
     ├───────────────┤  ◄── prevEnd starts here (= 8 + count*8)
     │ body[0]        │  placement & per-body layout per §6.2/§6.3
     │ body[1]        │
     │ …              │
     └───────────────┘
```

### 6.2 Texture instantiation (`Base\Texture-Base-Global.dat`)

Texture *pixel payloads* are addressable by SNO id (§3); the
`TextureDefinition` *meta* is consolidated here (not per-SNO).

Per-entry placement: `descStart = alignUp8(prevEnd) + 8`; the SNO id is
stored at `descStart+0`; the `TextureDefinition` body base is
`descStart+4`. (This `+8` / in-body-snoId convention is what differs
from StringList — §6.3, Appendix A CL-7.)

`TextureDefinition` body (payload-base = body base):

| Offset | Field |
|---|---|
| 8 | `eTexFormat` |
| 16 | `dwWidth` |
| 18 | `dwHeight` |
| 64 | `serTex` (combined-meta variable-array form) |
| 80 | `ptFrame` (combined-meta variable-array form) |

Combined-meta variable-array descriptor form: `i32 pad; i32 off@+4
(blob-relative from descStart); i32 size@+8`.

- Paragon atlases are **BC3** (`eTexFormat 49`); mip0 is at payload
  offset 0; decode at row width `align(W,64)` then crop.
- `ptFrame` element = `TexFrame`, 36 bytes:
  `u32 ImageHandle; f32 U0; f32 V0; f32 U1; f32 V1; …`. Atlas sub-rect
  pixel rectangle = `floor(U·W) … ceil(U·W)` (and V·H).
- **Node↔icon link is first-party:** `ParagonNode.hIconMask` / `hIcon`
  (§7) equals a `TexFrame.ImageHandle` (no correlation needed).
- BCn decode is image-library-agnostic (raw straight-alpha RGBA;
  Appendix C).

### 6.3 StringList instantiation (localized text)

#### 6.3.1 Addressing

StringList content is **not** per-SNO path-addressable (`Base\Meta\<id>`
does not resolve for SNO group 42). It is delivered through per-locale
consolidated bundles:

| TVFS path | Contents |
|---|---|
| `base/StringList-Text-<locale>.dat` | consolidated catalog for one locale (every table) — what a consumer reads |
| `base/StringList-Text-<locale>-0x<16-hex>.dat` | per-locale content shards |
| `base/StringList-Text-Global.dat` | locale-independent container (tiny; usually one placeholder table) |

Locales observed on the verified build: `enUS deDE esES esMX frFR itIT
jaJP koKR plPL ptBR ruRU trTR zhCN zhHM` (the install also carries
`zhTW`). Resolve through TVFS (path normalized `/`→`\`, upper-cased at
hash time, like all paths).

#### 6.3.2 Per-entry placement (differs from Texture)

```
prevEnd = 8 + count*8
for i in 0 .. count-1:
    B = alignUp8(prevEnd)         ; body base — NO "+8"
    prevEnd = B + size[i]
    ; the SNO for body i is index[i].sno  (POSITIONAL — not in the body)
```

The Texture `+8` / in-body-snoId convention yields all-zero bodies for
StringList; `B = alignUp8(prevEnd)` decodes every table (Appendix A,
CL-7).

#### 6.3.3 `StringListDefinition` body (relative to `B`)

| Offset | Type | Field |
|---|---|---|
| B+0 … B+15 | byte[16] | header / pad |
| B+16 | u32 | `blockSize` (not needed to read strings) |
| B+20 | u32 | `infoLength` (byte length of the entry table) |
| B+24 … B+31 | byte[8] | pad |
| B+32 | entry[`infoLength / 40`] | the entries (40-byte stride) |

Entry (40 bytes):

| Offset | Type | Field |
|---|---|---|
| +0 | i64 | pad |
| +8 | u32 | `keyOffset` (B-relative) |
| +12 | u32 | `keyLen` (bytes, includes trailing NUL) |
| +16 | i64 | pad |
| +24 | u32 | `valOffset` (B-relative) |
| +28 | u32 | `valLen` (bytes, includes trailing NUL) |
| +32 | i64 | pad |

```
label = UTF-8 at  B + keyOffset , keyLen bytes  (strip trailing NUL)
text  = UTF-8 at  B + valOffset , valLen bytes  (strip trailing NUL)
```

Strings are UTF-8; values carry D4 markup (`{c_important}…{/c}`,
`{VALUE}`, `[{VALUE2} * 100|1%|]`, `{s1}`/`{s2}` substitution tokens).
Labels are unique only **within a table**; a table (SNO) is a domain
bucket (`AttributeDescriptions`, `Bnet_Chat`, skill/affix/item tables,
…) — resolve the table by SNO (name via CoreTOC group 42), then the
label.

#### 6.3.4 Verified anchors (build 3.0.2.71886, enUS)

- `count = 58286`; full walk `finalPrevEnd = 20,207,724`,
  `blobLen = 20,207,728` (4-byte trailing pad).
- table SNO `4080` = `AttributeDescriptions`, 646 entries.
- table SNO `4087` = `Bnet_Chat`: label `ChatLink_WhisperedTo` →
  `"{s1} whispers: {s2}"`.
- last table SNO `2646845` =
  `DungeonAffix_Positive_Torment_AncestralElites`: `AffixName` →
  `"{c_white}Dungeon Delve{/c}"`.

## 7. Paragon record layouts (SNO groups 106 / 108 / 111 / 112)

All offsets payload-relative (base `0x10`).

### 7.1 `ParagonBoardDefinition` (group 108, `.pbd`)

| Offset | Type | Field |
|---|---|---|
| 0 | DT_INT | `snoId` |
| 12 | DT_UINT | `nWidth` |
| 16 | DT_VARIABLEARRAY[DT_SNO] | `arEntries` (`dataOffset@+8` payload-rel, `dataSize@+12`) |

Cells = `dataSize / 4` LE `u32` SNO ids, row-major
(`index = row*Width + col`); `0xFFFFFFFF` = empty. Cell count equals
`Width*Width` (21×21 = 441 on the verified build).

### 7.2 `ParagonNodeDefinition` (group 106, `.pgn`)

| Offset | Type | Field |
|---|---|---|
| 0 | DT_INT | `snoId` |
| 8 | DT_UINT | `hIcon` |
| 12 | DT_UINT | `hIconMask` |
| 20 | DT_ENUM | `eRarityOverride` (0=Common, 2=Magic, 3=Rare, 4=Legendary) |
| 24 | DT_SNO (group 29) | `snoPassivePower` |
| 32 | DT_VARIABLEARRAY[AttributeSpecifier] | `ptAttributes` (`dataOffset@+8`, `dataSize@+12`) |
| 80 | DT_INT | `bHasSocket` |
| 84 | DT_INT | `bIsGate` |

`AttributeSpecifier` — stride **88**:

| Offset | Type | Field |
|---|---|---|
| +0 | DT_ENUM | `eAttribute` |
| +4 | DT_INT | `nParam` |
| +12 | (int) | the distinct value at +12 |
| +24 | i32 | inline-formula `srcOffset` (payload-relative; used when GBID is null) |
| +28 | i32 | inline-formula `srcSize` |
| +48 | DT_GBID | `gbidFormula` (`0xFFFFFFFF` ⇒ use the inline text) |

### 7.3 `ParagonGlyphDefinition` (group 111, `.gph`)

| Offset | Type | Field |
|---|---|---|
| 0 | DT_INT | `snoId` |
| 104 / 108 / 112 | DT_SNO ×3 | up to three affix SNO ids (`0` / `0xFFFFFFFF` slots omitted) |

Some group-111 SNOs are short placeholder records — bounds-check before
reading `+104`.

### 7.4 `ParagonGlyphAffixDefinition` (group 112, `.gaf`)

formatHash `353797140`.

| Offset | Type | Field |
|---|---|---|
| 0 | DT_INT | `snoId` |
| 24 | DT_ENUM | `eAffectedNodeRarity` (1=Normal, 2=Magic, 3=Rare) |
| 48 | DT_ENUM | `eBonusOperation` (1/2/4/5) |
| 76 | DT_FLOAT | `flStartingBonusScalar` (== Maxroll `base`) |
| 80 | DT_FLOAT | `flAddedBonusScalarPerLevel` (== Maxroll `perLevel`) |

## 8. GameBalance `AttributeFormulas` (group 20, SNO 201912)

Only `eGameBalanceType == 22` (AttributeFormulas) is in scope; other
GameBalance table types have different element structs (deferred).

```
GameBalanceDefinition:
  snoId@0 ;  eGameBalanceType@8 (= 22)
  ptData  DT_POLYMORPHIC_VARIABLEARRAY @16  → dataOffset@+8 (payload-rel)
  tableBase = dataOffset + 8                ; 8-byte polymorphic type tag
  AttributeFormulaEntry_Table:
    tEntries DT_VARIABLEARRAY @ tableBase+16 → dataOffset@+8, dataSize@+12
    ENTRY STRIDE = 280

AttributeFormulaEntry (280):
  szName    DT_CHARARRAY[256] inline @ +0
  gbid      DT_GBID            @ +256        ; in-record value is 0xFFFFFFFF (null)
  arRanges  DT_VARIABLEARRAY   @ +264        ; dataOffset@+8, dataSize@+12
  RANGE STRIDE = 48

AttributeFormulaRange (48):
  nItemPowerRangeStart i32 @ +0
  rangeValue1          f32 @ +4
  rangeValue2          f32 @ +8
  tFormula DT_STRING_FORMULA @ +16           ; FormulaOffset@+8, FormulaSize@+12
  formula text = ASCII @ payload FormulaOffset, FormulaSize bytes (trim)
```

**Identity.** The in-record `gbid` is null; an entry's identity is
`GbidHash(szName)` — a case-insensitive DJB2; `GbidHash` of
`ParagonNodeCoreStat_Normal` is `0x42C16A1B`. A node's `gbidFormula`
(§7.2, `AttributeSpecifier+48`) equals `GbidHash(formulaName)`; resolve
`gbid → name → arRanges[0] formula text`. The library returns **text +
name/GBID indices only**; evaluation and the 6 calibrated intrinsics are
the consumer's (Appendix C).

Verified build: SNO 201912 has 1038 entries;
`ParagonNodeCoreStat_Normal` → text `"5"`,
`ParagonNodeCoreStat_Magic` → `"7"`.

## 9. Read path (Diablo IV)

```
SNO (group, id, Folder) → path  Base\<Folder>\<id>[-<subId>]
                        → CascPathHash → TVFS → EKey
                        → (transport: local index → envelope → BLTE)
                        → SNO blob (payload base 0x10)

Payload empty/absent → shared-payload alias (§5) → retry as
                        Base\Payload\<sharedSnoId>
```

---

## 10. Paragon UI render layout (group 46, `0xE4825AB8`) — FR-C7

> **Status: format LOCATED + container characterized; field-level
> decode IN PROGRESS.** This section states only what is empirically
> proven against build `3.0.2.71886`. It is deliberately *not* a
> finished layout — the nested widget-tree fields are not yet decoded,
> and nothing here is guessed. (Consumer ask: `e:\Paragon\docs\
> fr-c7-paragon-render-layout.md`. Note: that ask references the
> pre-split single `casc-format.md`; per the spec split this D4-layer
> format is documented **here**, in `casc-diablo4-format.md`, with its
> own `CL-*` log — the split is not being re-merged.)

### 10.1 The format and target record (proven)

Diablo IV UI screens/scenes are a distinct SNO record family in
**group 46**, format hash **`0xE4825AB8`** — peers include `ActionBar`,
`Armory`, `BuildViewer`, `BrightnessDialog`, `Achievements` (286
entries; all UI screens/menus/dialogs). This is the "different
UI-definition SNO format" the consumer could not previously identify.

The paragon node/board render layout is:

| SNO | id | Meta size | role |
|---|---|---|---|
| `ParagonBoard` | 657304 | 145,550 B | the board screen — node grid placement + per-state texture binding |
| `ParagonBoardSelect` | 964599 | 34,481 B | the board-selection screen |

Wrong leads, eliminated with evidence (so they are not re-investigated):
group **63** `Paragon_*Nodes` are 113-byte tutorial/help triggers;
group **29** `Paragon_*_Legendary_*` are node *powers*; groups
**1/9/14/27** are the *art* (mesh / animation / VFX); group **42**
paragon entries are localized strings; group **44** `2DUI_Paragon*` are
the texture atlases (already decodable). The render *metric* is none of
these — it is the group-46 UI-scene record.

### 10.2 Container header (proven, common across `0xE4825AB8`)

```
0x00  u32  0xDEADBEEF              SNO signature
0x00..0x10                        16-byte SNO header (zero after sig)
0x10  u32  SNO id                 (657304 for ParagonBoard)
0x20  u32  0x70                   root-struct offset (constant observed)
0x24  u32  type/version word      0x12 (ParagonBoard) vs 0x11 (BrightnessDialog)
0x30  u32  offset (0x88)          \ root descriptor offset/size/count
0x34  u32  size   (0x58)          / triple
0x38  u32  0x01                   count/flag
0x80  ASCII root widget name      "ParagonBoard_main\0"
~0xA8 u32  hash/handle, FFFFFFFF  start of the nested widget tree
```

The body is a **nested widget tree**: 32-bit texture/material/style
handles (the same `TexFrame.ImageHandle` space as §6 — e.g. the
`2DUI_Paragon_transparentElements` frame handles the consumer
catalogued) interleaved with per-widget anchor/size float structs and
`0xFFFFFFFF` child/sentinel delimiters. Decoding the widget-node struct,
the anchor/size encoding, and the per-rarity/per-state binding table to
the field level is the remaining FR-C7 work (tracked CL-9); it is a
B1–B6-scale round and will be delivered with the same acceptance rigour,
not approximated.

### 10.3 Decoded so far (verified) and the open field decode

Verified against build `3.0.2.71886` (`build/SnoScan strings|scan|f32`):

- **It is a named widget-tree / serialized object graph.** Widget names
  are inline ASCII landmarks: `ParagonBoard_main` (root) → `Content` →
  `ParagonNodes` → `ParagonNodes_BaseLayer` / `_TopLayer` /
  `_BoardRotationLayer` (+ `_VFX_Canvas` siblings), `Storyboard_ScaleTest`,
  `GlyphAuras`, the `SidePanel_*` / `Paragon_Points_*` chrome, etc. The
  node-grid scale and board-rotation widgets the FR needs **exist and are
  named** (`_BoardRotationLayer`, `Storyboard_ScaleTest`).
- **Texture-binding micro-struct (proven):**
  `… tag(u32 0x22|0x02|0x03)  0x00000000  textureHandle(u32 LE)
  0x00000000 …`, where `textureHandle` is the same `TexFrame.ImageHandle`
  space as §6. Bound in `ParagonBoard`: base disc `1D166DC7`, grey rim
  ring `87A89F86`, glyph pulse ring `BED4CF21` (**4 occurrences** = the
  multiple node states), gold ornate frame `4A901508`. Widget records
  carry offset back-references (e.g. `→0xE9A8`, `→0x1E88`) and a
  recurring shared handle pair `012FC68B` / `A4C42E02` co-located with
  node-element bindings — a **shared node-element style/material
  template** (the next RE target).
- **FR §2.3 answered (evidence-based):** the rarity fill swatches
  (`33A11FA6`, `A09D0667`, …) and the orange ornate `A54E0DD1` are
  **absent** from `ParagonBoard`. The render layout binds only the
  *neutral* disc + rings + gold ornate. Therefore per-rarity colour is a
  **shader tint applied to the neutral disc, not a per-rarity texture** —
  the consumer's shader-recipe model (§2.3) is the correct one; a "give
  me the per-rarity disc texture" API would be wrong because no such
  asset is referenced.

**Still open (NOT decoded — no guesses emitted):** the widget-node
struct field layout, the anchor/size encoding (authored px ↔ board-cell
pitch), the per-rarity/per-state ordered layer list, and the animation
params. Sparse floats are present near widgets (e.g. `0.049` repeats —
plausibly a normalised anchor) but the struct framing is not yet pinned,
so no `CellPitch`/size numbers are asserted. The typed
`ParagonRenderLayout` ships only when these are pinned with a verbatim
acceptance matrix — consistent with the FR's zero-guessed-constants
contract.

### 10.4 Reconnaissance instrument

`build/SnoScan` (in `e:\Casc`, not shipped, not in the solution —
same throwaway posture as `build/TileIcon`) drives the real
`WiseOwl.Casc.Diablo4` decoder against the live install:
`groups` (all 181 groups + format hashes + counts), `find <substr>`
(named entries → group/hash/id), `dump <gid> <id> [folder]`
(SNO header fields + hex/ascii). This keeps the RE on our own library
and leaves `e:\Paragon` strictly read-only (the consumer's `snoscan`
tool would have written build output there).

---

## Appendix A — correction log (Diablo IV errata)

What was found wrong/omitted during empirical implementation, and the
true value (the sections above already state the corrected truth).

- **CL-4 — per-SNO addressing.** The TVFS walk was never the problem
  (it is complete; 1,759,690 entries; all 37 nested `vfs-N`
  sub-manifests descended). Diablo IV addresses SNO content by
  `Base\<Folder>\<id>` (§3) — no group folder, name, or extension; a
  child sub-id appends `-<subId>`. `Base\Meta\2458674` resolves; the
  name-path and the `base:meta\<id>` colon form both miss.
- **CL-5 — texture payloads are mostly direct.** With the complete
  TVFS, `Base\Payload\<textureId>` resolves directly for the paragon
  atlases (including per-class ones an earlier, narrower view reported
  as "no direct entry"). The `0xABBA0003` mapping (§5; 35,616 entries
  on the verified build) is applied only as a transparent fallback.
- **CL-6 — `CoreTOCReplacedSnosMapping.dat` not implemented.** Not
  needed on the verified build (everything resolves without it).
  Implement only if a seasonal patch makes a known SNO 404 and it is
  found in the replaced map.
- **CL-7 — StringList container vs Texture.** StringList uses
  `B = alignUp8(prevEnd)` with **no `+8`** and **no SNO id in the
  body** (positional, from the index); the Texture `+8`/in-body-snoId
  convention produces all-zero StringList bodies. The body is
  `infoLength@B+20`, 40-byte entries at `B+32`
  (`keyOffset@+8, keyLen@+12, valOffset@+24, valLen@+28`), UTF-8
  strings at `B+offset`. Validated across all 58,286 tables (the walk
  lands exactly at EOF; §6.3.4).
- **CL-8 — typed record readers + spec authority.** The library owns
  typed *record decoding* (raw fields only; **no formula evaluator at
  all**, by decision). §§7–8 were re-derived and verified here; the §8
  acceptance matrix passes verbatim (board 2458674 → W21/441; node
  678776 → sig `0xDEADBEEF`; GameBalance 201912 → 1038 entries,
  `ParagonNodeCoreStat_Normal` → `"5"`, `_Magic` → `"7"`). The
  `AttributeSpecifier` exposes both `nParam` (+4) and the distinct
  value (+12) so the consumer never re-parses the specifier. Spec
  authority transferred to this document set; upstream
  `d4-binary-formats.md` §3–§8.15 frozen for layouts.

- **CL-9 — paragon UI render layout (FR-C7), format located.** The
  paragon render metric is **not** in the paragon record groups, the
  art groups, or the texture atlases — it is a **group-46 UI-scene
  record**, format hash **`0xE4825AB8`** (peers: `ActionBar`, `Armory`,
  `BuildViewer`…), specifically `ParagonBoard` SNO 657304 (145,550 B)
  and `ParagonBoardSelect` 964599. Container header proven (§10.2);
  widget-tree is a named serialized object graph and the texture-binding
  micro-struct is decoded (§10.3). **FR §2.3 answered with evidence:**
  the rarity swatches + orange ornate are absent from `ParagonBoard`, so
  per-rarity colour is a shader tint on the neutral disc, not a
  per-rarity texture (the consumer's recipe model is correct). Still
  open and explicitly NOT guessed: the widget-node struct field layout,
  anchor/size↔cell-pitch encoding, per-state ordered layer list, anim
  params — the typed `ParagonRenderLayout` ships only with a verbatim
  acceptance matrix. The consumer ask's document-target reference
  (`casc-format.md`) predates the spec split; this D4-layer format is
  owned here.

## Appendix B — provenance & migration map

Auditable mapping of every upstream `d4-binary-formats.md §3–§8.15`
byte-format item to its destination here, so the spec-authority handoff
loses nothing. Status = RE-verified against build `3.0.2.71886`.

| Upstream §/topic | Destination | Status |
|---|---|---|
| §1 CoreTOC `0xBCDE6611` | §3 / §4 (+ CL-4) | verified |
| §1 `0xABBA0003` shared-payload mapping | §5 (+ CL-5) | verified |
| §3 SNO addressing + `SNOFileHeader` (base `0x10`) | §2 / §3 | verified |
| §4 DT primitive encodings | §2.1 | verified |
| §5 ParagonBoard/Node/Glyph/GlyphAffix layouts | §7 | verified (B1–B4) |
| §5.1 / §8.13 node↔icon (`hIconMask==ImageHandle`) | §6.2 (+ B6) | verified |
| §7 / §7.3-VERIFIED GameBalance AttributeFormulas | §8 | verified (B5) |
| §8.1–§8.2 `TextureDefinition` / `eTexFormat` / BCn | §6.2 | verified |
| §8.12–§8.14 `0x44CF00F5` bundle / `ptFrame` slice | §6.1 / §6.2 | verified |
| §8.5 StringList per-locale bundles | §6.3 | verified (CL-7) |
| §7 the **6 calibrated intrinsic VALUES** | NOT absorbed → `e:\Paragon` policy | carve-out |
| §8.14/§8.15 relight & disc+symbol composite | NOT absorbed → `e:\Paragon` policy | carve-out |
| §3–§8 investigation narrative / dead-ends | `docs/devlog/*` + `ARTICLE-SOURCE.md` | history |
| §3–§7 CASC/TACT/TVFS/BLTE transport | `casc-format.md` | verified |

The upstream file is frozen for layouts (the demotion banner is the
ParagonOptimizer session's edit to its own repo; `e:\Paragon` is
read-only here).

## Appendix C — library boundary (FR-5 / FR-16)

`WiseOwl.Casc.Diablo4` owns: SNO read by id, CoreTOC (incl. name↔id
index), the `0xABBA0003` shared-payload resolution, the `0x44CF00F5`
combined-meta family (`TextureDefinition` + StringList), image-library-
agnostic BC1/BC3 decode (`DecodeMip0` → raw straight-alpha RGBA32; the
caller crops with `TexFrame.PixelRect` and owns any imaging/PNG/
compositing), the game-wide `GbidHash`, the per-locale StringList
catalog, and the typed paragon/GameBalance **record decoders** (§§7–8) —
**raw fields only**.

It does **not** own (consumer policy, authoritative in `e:\Paragon`):
formula evaluation/recursion, the 6 calibrated engine intrinsics, the
scoring/objective model, the relight/disc+symbol composite calibration,
or the app's bundled-JSON schema. The library ships **no formula
evaluator at all**, by decision.

**FR-16.** Item / Affix / Power / Class / GameBalance → stat-effect
*modeling* is a ParagonOptimizer **domain spec** built on the library's
id-keyed read + record decoders + `GbidHash` + StringList. The library
will not grow scoring/evaluation APIs. Round-2/3 feature requests and
their disposition are tracked in `docs/feature-backlog.md`.

## Appendix D — source & re-verification

- Clean-room; cross-checked against the permissively-licensed references
  in `THIRD-PARTY.md` (incl. `alkhdaniel/diablo-4-string-parser` for the
  standalone `.stl` cross-check). No third-party source incorporated.
- Verified against Diablo IV build `3.0.2.71886` (`.build.info` Build
  Key `522f2f30f1eb0e32af225966b8ac91d1`).
- **Re-verify trigger:** the `.build.info` Build Key changes (seasonal
  patch). Re-run the integration tests; update Appendix A on any drift.
