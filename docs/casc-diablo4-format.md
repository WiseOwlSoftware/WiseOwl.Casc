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

## 10. Diablo IV UI-scene format (group 46, `0xE4825AB8`) — FR-C7

> **Authoritative reference for the D4 UI-scene SNO** (the paragon
> render layout the consumer requested as FR-C7). Status: **format,
> hashes, data-binding model, and schema vocabulary fully decoded —
> standalone and clean-room. Open:** the per-widget *bound instance
> values* (the rect ints) and their reproduction of the §10.7 67.7
> px/grid anchor → the typed `ParagonRenderLayout`. No
> pitch/scale/anchor number is asserted until the bound values are read
> and reproduce the anchor; nothing here is guessed.
>
> Spec-authority note: the consumer ask
> (`e:\Paragon\docs\fr-c7-paragon-render-layout.md`) references the
> pre-split `casc-format.md`; per the split this D4-layer format is
> owned **here**, with its own `CL-*` log (CL-9, CL-10). Not re-merged.

### 10.1 Location and container

D4 UI screens/scenes are SNO **group 46** (CoreTOC type name **`UI`**),
format hash **`0xE4825AB8`** — peers `ActionBar`, `Armory`,
`BuildViewer`, `BrightnessDialog` (286 entries; all UI screens). The
paragon render layout:

| SNO | id | Meta size |
|---|---|---|
| `ParagonBoard` | 657304 | 145,550 B |
| `ParagonBoardSelect` | 964599 | 34,481 B |

Container (proven, common across `0xE4825AB8`): `0xDEADBEEF` + 16-byte
SNO header → root header at `0x20` (`0x70` root offset; type/version
word; offset/size/count fields) → embedded root-widget name
`ParagonBoard_main` at `0x80` → the widget graph.

Eliminated with evidence (do not re-investigate): group 63
`Paragon_*Nodes` = 113-byte tutorial triggers; group 29
`Paragon_*_Legendary_*` = node powers; groups 1/9/14/27 = art
(mesh/anim/VFX); group 42 = strings; group 44 `2DUI_Paragon*` = the
texture atlases (already decodable, §6).

### 10.2 The D4 identifier hashes (decisive — reusable library-wide)

All D4 serialization ids are the DJB2 core `h = h*33 + ch` with
**seed 0** (standard DJB2 seeds 5381 — D4 does **not**; a seed-5381
test misses, which is why eight common algorithms initially appeared to
fail). Self-verified: `gbidHash("ParagonNodeCoreStat_Normal") =
0x42C16A1B`, the project's independently-known-good GBID.

| name | lowercase input | final mask | identifies |
|---|---|---|---|
| `fieldHash` | no | `& 0x0FFFFFFF` (28-bit) | struct **field** names |
| `typeHash` | no | none (u32) | **type / class / struct** names |
| `gbidHash` | **yes** | none (u32) | GBIDs — this **is** `Diablo4.GbidHash` |

The 28-bit `fieldHash` mask is why all field-ids cluster `<0x10000000`.
This applies to **every** D4 SNO meta format, not only FR-C7.

### 10.3 Reflection / data-binding model

`0xE4825AB8` is a reflection-serialised, hash-addressed widget graph:

```
widget := inline name (NUL-terminated, zero-padded)
          name+0x28  u32  class id = typeHash(widget-class name)
          name+0x30  u32  0xFFFFFFFF sentinel + zero run
          name+0x60  [ self-anchor ][ block size ][ field count ]
                     then  count × schema entry
schema entry := ( fieldHash(name) , typeHash("DT_BINDABLEPROPERTY") ,
                  typeHash(underlying DT_* type) )
```

Every widget field is a **`DT_BINDABLEPROPERTY`** of an underlying
`DT_*` type — D4's UI data-binding system. `0x1332C78D` (the ubiquitous
"separator" of earlier passes) **is** `typeHash("DT_BINDABLEPROPERTY")`.
Widgets reference children by name-hash, not file offset (hence the
recurring-constant-heavy layout and few pointers). `ParagonBoard_main`
(root) lists 6 child refs.

### 10.4 Standalone clean-room vocabulary recovery

Field/type **names are not stored in any SNO/CASC data file** (the
format is hash-keyed by design — one-way 28-bit `fieldHash`). They
**are** embedded in the **D4 client binary** for the engine's own
reflection registry. Recovery procedure (first-party; no dependency on
any third-party JSON):

1. String-extract printable identifiers from the *locally-installed*
   `Diablo IV.exe` (+ `diablo_iv_loader.dll`) — the user's own
   legally-obtained binary, processed in-tool, never shipped (same
   posture as reading the user's own game data).
2. Hash each candidate with `typeHash` / `fieldHash` (§10.2).
3. Match against the observed ids; expand the residue with D4 naming
   conventions (`n`/`fl`/`h`/`e`/`b`/`dw`/`sno`/`rgba`/`pt` prefixes ×
   layout/widget terms). Collisions/misses tracked.

Executed: 300k+ unique tokens, the FR-critical vocabulary resolved
(below). This makes D4 name recovery a permanent library capability.

### 10.5 Recovered type enum

| id (`typeHash`) | type |
|---|---|
| `0x1332C78D` | `DT_BINDABLEPROPERTY` (the per-field binding marker) |
| `0xA4C42E02` | `DT_INT` |
| `0xE65047AD` | `DT_FLOAT` |
| `0x3D4646AB` | `DT_BYTE` |
| `0x3D47BD2C` | `DT_ENUM` |
| `0xE549F591` | `DT_CSTRING` |
| `0x8E266332` | `DT_RGBACOLOR` |
| `0xA4C45887` | `DT_SNO` |
| `0x2B0285C0` | `StringLabelHandleEx` |
| `0x6B1C5D9C` | DT_* not yet named (struct/vector-like; residual) |

### 10.6 `ParagonBoard` widget schema (decoded)

Field id → recovered name → type (count = occurrences across the SNO).
Names blank where the residual candidate set has not yet matched; the
**type is known for every field** (so unnamed fields are still
classified):

| field id | name | type | n |
|---|---|---|---|
| `0x06F9158E` | **`nWidth`** | DT_INT | 86 |
| `0x02D88AE7` | **`nHeight`** | DT_INT | 74 |
| `0x07F1EF79` | **`nLeft`** | DT_INT | 66 |
| `0x069EA64C` | **`nRight`** | DT_INT | 67 |
| `0x003DC5C1` | **`nTop`** | DT_INT | 76 |
| `0x0594CC83` | **`nBottom`** | DT_INT | 69 |
| `0x0C2AFA21` | **`dwAlpha`** | DT_BYTE | 11 |
| `0x09A3F17B` | **`rgbaTint`** | DT_RGBACOLOR | 6 |
| `0x00957CB7` | (DT_RGBACOLOR field) | DT_RGBACOLOR | 26 |
| `0x0789C1CD` | **`hText`** | StringLabelHandleEx | 52 |
| `0x0204DBB8` | **`hTooltipText`** | StringLabelHandleEx | 1 |
| `0x07DB38D3` | (texture/SNO ref — primary) | DT_SNO | 27 |
| `0x01844A00` `0x0219D52D` `0x0C43C17C` `0x0CCBA90F` | (SNO refs) | DT_SNO | 1 each |
| `0x06AB76DE` | (int) | DT_INT | 124 |
| `0x0CDB00E9` | (int) | DT_INT | 14 |
| `0x093CBAA8` | (enum) | DT_ENUM | 132 |
| `0x03D55658` | (enum) | DT_ENUM | 122 |
| `0x0C152636` | (DT_? `0x6B1C5D9C`) | DT_? | 93 |
| `0x02509B49` `0x008AB8D6` | (cstring) | DT_CSTRING | 3 |
| `0x03445DCD` `0x08CF4C5D` `0x05A28796` `0x0D6B1ED2` | (int) | DT_INT | 1 |
| `0x0B63D29B` `0x0D75128C` `0x0DAEFCAA` `0x02330CBF` `0x056F24F5` `0x05A90F13` `0x0A2C2344` | (DT_? `0x6B1C5D9C`) | DT_? | 1 |

The widget **layout rect is `nLeft / nRight / nTop / nBottom /
nWidth / nHeight` as `DT_INT` bindable properties**; appearance is
`rgbaTint` (+ a second DT_RGBACOLOR), `dwAlpha`, `hText`/`hTooltipText`;
textures are the five `DT_SNO` fields (primary `0x07DB38D3`).

### 10.7 FR-C7 geometry conclusions

- **Premise correction (consumer-endorsed):** the rect fields are
  **bindable**, not literal constants — a full 145 KB scan finds no
  authored pixel cluster at any texture-native size, screen
  resolution, or node pitch. Node geometry is *not* authored px data;
  it is `ParagonBoardDefinition` grid (§7.1) + bound rect ints +
  texture-native sizes (§6), composed at runtime resolution. A literal
  `CellPitch` does not exist as stored data; `ParagonRenderLayout`
  exposes the normalised model + derivation, not px constants. The
  global px scale (render resolution / zoom) is permanently
  consumer-owned — same pattern as the 6 intrinsics / §3 relight.
- **Rarity tint (FR §2.3):** confirmed shader/colour-driven on the
  *neutral* disc. The rarity fill-swatches and orange ornate are absent
  from `ParagonBoard`; the bound colour is the `rgbaTint`
  `DT_RGBACOLOR` field on the neutral disc — the consumer's recipe
  model is correct, and the tint is a readable bound colour, not a
  per-rarity texture.

### 10.8 Acceptance anchor (consumer oracle, dual-validated)

> **≈ 67.7 px / grid-step**, provenance **{zoom = 0 (smallest),
> render = 7680×2160, *Warlock Start* board, nothing selected}**;
> dual-validated ≤ 0.4 px (lattice autocorrelation 67.59/67.81 square;
> landmark span gate(10,0)→start(10,14) = 951.5 px ÷ 14 = 67.96).
> A known-grid-distance reference capture (two identified-`(X,Y)`
> nodes, same tag) is also being supplied (`Δpx ÷ Δgrid`).

Decoded `bound-rect → screen` at this provenance **must reproduce
≈67.7** (±~0.4) and be cross-widget consistent — the mapping is
*over-determined* (proof, not inference). `IconCellFactor` on delivery =
C7-normalised-ratio × this consumer-owned pitch basis.

**CL-10:** the *Warlock Start* view is **axis-aligned**, not rotated
~45° (the FR §2.4 assumption fails here; the lattice autocorrelation is
a clean square). `BoardRotationDegrees` is **decoded** from
`ParagonNodes_BoardRotationLayer`, never assumed, and must resolve to 0°
at this provenance.

### 10.9 Reconnaissance instrument

`build/SnoScan` (in `e:\Casc`, not shipped, not in the solution — same
posture as `build/TileIcon`) drives the real `WiseOwl.Casc.Diablo4`
decoder against the live install; commands: `groups`, `find`, `strings`,
`scan`, `f32`, `members` (schema enumeration), `dh` (the D4 hashes),
`crack` (wordlist → id matching), `dump`. Keeps the RE on our own
library; `e:\Paragon` stays read-only.

### 10.10 Open work (well-defined, no external dependency)

1. Decode the **instance-data section** (separate from the schema) and
   read each widget's bound `nLeft/nRight/nTop/nBottom/nWidth/nHeight`,
   `rgbaTint`, and `DT_SNO` values.
2. Resolve the residual unnamed field-ids (widen the candidate set) —
   type is already known for all; names are a refinement.
3. Reproduce the §10.8 67.7 anchor from the node/grid widgets' bound
   rect ints → derive the normalised pitch/scale/anchor model and the
   `nodeCentre = canvasRef × normPitch(gridXY)` rule.
4. Implement `Diablo4Storage.ReadParagonRenderLayout()` per the agreed
   contract (`docs/fr-c7-response.md` §8) + the verbatim acceptance
   matrix. Until step 3 proves it, no pitch number is asserted.


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

- **CL-9 — D4 UI-scene format (FR-C7) decoded; the D4 hash cracked.**
  The paragon render metric is a **group-46 UI-scene SNO** (type `UI`),
  format hash **`0xE4825AB8`**, `ParagonBoard` SNO 657304 — *not* the
  paragon record/art/atlas groups (all eliminated with evidence, §10.1).
  Decoded standalone & clean-room (full reference: §10): D4's
  serialization hash is **DJB2 with seed 0** (`fieldHash` 28-bit-masked,
  `typeHash` full, `gbidHash` lowercased = the existing
  `Diablo4.GbidHash`) — self-verified vs the known GBID `0x42C16A1B`,
  and **reusable across every D4 SNO meta format, not just FR-C7**.
  The format is a reflection / data-binding widget graph: each field is
  `DT_BINDABLEPROPERTY` of a `DT_*` type; the `ParagonBoard` schema is
  fully type-classified with the FR-critical fields named — the layout
  rect `nLeft/nRight/nTop/nBottom/nWidth/nHeight` (DT_INT), `rgbaTint`
  (DT_RGBACOLOR), `dwAlpha`, the `DT_SNO` texture fields — recovered by
  string-extracting the *locally-installed* `Diablo IV.exe` (no
  third-party-JSON dependency; names are absent from SNO data by design
  but embedded in the client binary's reflection registry). **FR §2.3
  answered with evidence:** per-rarity colour is the bound `rgbaTint`
  on the neutral disc (rarity swatches + orange ornate absent) — the
  consumer's recipe model is correct. Still open and explicitly NOT
  guessed: the per-widget *bound instance values* (the rect ints) and
  their reproduction of the §10.8 67.7 px/grid anchor → the typed
  `ParagonRenderLayout`; no pitch number asserted until proven. The
  consumer ask's document-target reference (`casc-format.md`) predates
  the spec split; this D4-layer format is owned here.
- **CL-10 — FR-C7 board rotation is NOT a fixed ~45° (decode it).** The
  FR §2.4 "in-game the board is rotated ~45°" does not hold for the
  *Warlock Start* calibration view: the consumer's 2D lattice
  autocorrelation at `{zoom 0, 7680×2160, nothing selected}` resolves a
  **clean square axis-aligned lattice**, ≈67.7 px/grid-step
  (dual-validated ≤0.4 px; §10.4). `BoardRotationDegrees` must be
  **read from the `ParagonNodes_BoardRotationLayer` widget**, not
  assumed; it must resolve to 0° at this provenance for the acceptance
  anchor to hold. Recorded before vocabulary mapping so a phantom
  rotation cannot be baked into the decode.

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
