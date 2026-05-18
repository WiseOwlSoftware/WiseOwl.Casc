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

### 6.4 Sibling-table convention: ParagonBoard localized name (FR-D1)

A `ParagonBoardDefinition` (group 108, §7.1) carries **no** name,
name-string-id, or GBID. A board's **localized display name** ("Start",
"Dynamism", "Pyrosis", …) lives in the board's **sibling StringList
table** (group 42), resolved strictly by **CoreTOC name**:

```
boardName = CoreToc.GetName(108, boardSnoId)        ; e.g. "Paragon_Warlock_00"
tableName = "ParagonBoard_" + boardName             ; "ParagonBoard_Paragon_Warlock_00"
tableSno  = CoreToc.GetId(42, tableName)            ; group-42 StringList SNO
text      = GetStrings(locale).TryGet(tableSno, "Name")
```

- The label within the sibling table is **`Name`** (the table holds
  exactly one entry on the verified build).
- Resolution is **name-keyed only**. The two SNO ids have **no fixed
  offset**: Warlock's table happens to be `boardSnoId − 1`
  (`Paragon_Warlock_00` 2458674 → `ParagonBoard_Paragon_Warlock_00`
  2458673) but Sorcerer's is not (`Paragon_Sorc_00` 939773 →
  `ParagonBoard_Paragon_Sorc_00` 1111181). Never derive the table SNO
  arithmetically.
- Holds for **every** class stem on the verified build (`Paragon_Barb`,
  `_Druid`, `_Necro`, `_Paladin`, `_Rogue`, `_Sorc`, `_Spirit`,
  `_Warlock`).
- Locale-aware end to end (the StringList catalog is per-locale).

Verified anchors (build `3.0.2.71886`):

| Board SNO (108) | Board name | Table SNO (42) | `Name` (enUS) | `Name` (deDE) |
|---|---|---|---|---|
| 2458674 | `Paragon_Warlock_00` (IsStart) | 2458673 | `Start` | — |
| 2458680 | `Paragon_Warlock_03` | 2458679 | `Dynamism` | `Dynamismus` |
| 2458682 | `Paragon_Warlock_04` | 2458681 | `Pyrosis` | — |
| 2458692 | `Paragon_Warlock_10` | 2458691 | `Dominion` | — |

Shipped surface: `Diablo4Storage.TryReadParagonBoardName(int boardSnoId,
out string name, string locale = "enUS")` and the throwing
`ReadParagonBoardName`. Raw decoded value only — **no fallback policy**
(an unknown board / absent sibling table / missing `Name` label returns
`false` / `string.Empty`; the consumer owns the SnoName fallback).
`SnoGroup.StringList = 42` is now a named group (still not per-SNO
path-addressable; meaningful for CoreTOC name↔id resolution). See
Appendix A CL-15.

### 6.5 Character-class roster + localized names (FR-D2)

The canonical playable-class roster is **SNO group 74**
(`SnoGroup.PlayerClass`), independent of paragon. On build
`3.0.2.71886` group 74 holds the eight classes plus one non-class junk
entry:

| PlayerClass SnoId | SnoName | `General` `PlayerClass<SnoName>Male` (enUS) |
|---|---|---|
| 169776 | `Barbarian` | Barbarian |
| 131966 | `Druid` | Druid |
| 199277 | `Necromancer` | Necromancer |
| 2079084 | `Paladin` | Paladin |
| 199275 | `Rogue` | Rogue |
| 131965 | `Sorcerer` | Sorcerer |
| 1206232 | `Spiritborn` | Spiritborn |
| 2207749 | `Warlock` | Warlock |
| 159433 | `Axe Bad Data` | *(no label — filtered)* |

- **Localized name source:** the **`General`** StringList table (SNO
  **4118**, §6.3), label **`"PlayerClass" + SnoName + "Male"`**. This
  gendered label is the markup-free display string; the base
  `PlayerClass<SnoName>` label carries D4 `|5sing:plur` pluralization
  markup, and `…Male`/`…Female` are identical display strings on the
  verified build. Locale-aware (per-locale catalog).
- **Membership filter (data-driven, no hardcoded list):** a group-74
  entry is a real playable class **iff** that label exists. `Axe Bad
  Data` has none → excluded. New seasonal classes appear automatically.
- **Stable key:** the PlayerClass **SNO id** (never an array position).
- Shipped surface: `Diablo4Storage.ReadCharacterClasses(string locale =
  "enUS")` → ordered `IReadOnlyList<CharacterClass>` (`SnoId`,
  `SnoName`, `DisplayName`), sorted by `SnoId`, cached per locale. Raw
  decoded values only. See Appendix A CL-17.

### 6.6 ParagonBoard class/index from the name convention (FR-D1)

A `ParagonBoardDefinition` record carries **no** class or index field
(§7.1 is the whole record — verified: 1820 B = header + `snoId` +
`nWidth` + `arEntries` descriptor + 441 cells, nothing else). The only
first-party source of a board's owning class and ordinal is the **SNO
name convention** `Paragon_<ClassToken>_<Index>`. Per the durable
opaque-id principle (Appendix C) this is decoded **once, library-side**,
documented here with a re-verify trigger, and exposed typed — it is
**never** a consumer regex.

Decode rule:

- `ClassToken` = substring between the `Paragon_` prefix and the
  **final** `_`.
- `BoardIndex` = the trailing integer after the final `_`. Width
  varies: most start boards are `_00` but Spiritborn's is the
  single-digit `_0`; parse as an integer, not fixed-width
  (`Paragon_Warlock_03`→3, `Paragon_Spirit_0`→0).
- `Class` = the **unique case-sensitive prefix match**: the one §6.5
  roster `SnoName` that `ClassToken` is a prefix of. On the verified
  build the eight tokens map 1:1 and unambiguously:

  | Board token | → PlayerClass SnoName |
  |---|---|
  | `Barb` | `Barbarian` |
  | `Druid` | `Druid` |
  | `Necro` | `Necromancer` |
  | `Paladin` | `Paladin` |
  | `Rogue` | `Rogue` |
  | `Sorc` | `Sorcerer` |
  | `Spirit` | `Spiritborn` |
  | `Warlock` | `Warlock` |

  Resolution is data-driven against the §6.5 roster (not a hardcoded
  abbreviation map). Zero matches **or** ambiguity **throws**
  (`CascFormatException`) — the re-verify signal (Appendix D), never a
  silent drift.

Shipped surface: `Diablo4Storage.ReadParagonBoard(int)` resolves and
populates `ParagonBoardDefinition.ClassSnoId` (the §6.5 stable key),
`.ClassSnoName`, `.BoardIndex`. The byte-only
`ParagonBoardDefinition.Parse(blob)` leaves them `0`/`""`/`-1` (identity
derives from the name, not the bytes — honest sentinels, documented).
See Appendix A CL-16.

### 6.7 Generalized sibling-StringList convention (CL-20)

§6.4 (ParagonBoard name) is one instance of a **general D4 convention**:
a record's localized text lives in a group-42
(`SnoGroup.StringList`) SNO whose CoreTOC name is
`"<TypePrefix>_" + recordSnoName`, resolved **strictly name-keyed** via
`CoreToc` (the two SNO ids are unrelated — never an offset). Verified
prefixes/labels on build `3.0.2.71886`:

| Record group | Type prefix | Label(s) | Anchor |
|---|---|---|---|
| 108 ParagonBoard | `ParagonBoard_` | `Name` | `Paragon_Warlock_00` → `Start` (§6.4) |
| 73 Item | `Item_` | `Name`, `Flavor`, `TransmogName`, `Description` | `1HAxe_Unique_Generic_001` (223287) → `Item_1HAxe_Unique_Generic_001` (941704) → Name `The Butcher's Cleaver` |
| 104 Affix | `Affix_` | `Desc` | `Talisman_Charm_Affix_1HAxe_Unique_Generic_001` (2586362) → `Affix_…` (2586361) → Desc `Your attacks Critically Strike …` |
| 29 Power | `Power_` | `name`, `desc` (lowercase) | `Paragon_Warlock_Legendary_001` (2521393) → `Power_…` (2521392) → name `Fathomless` |

(Character-class names are the parallel §6.5 case — the `General`
table, not a per-SNO sibling.) Raw decoded text only — D4 markup
intact; an absent sibling table / label returns empty (honest
sentinel; the consumer owns any fallback). The library exposes this via
the typed C6 readers (§11) and `Diablo4Storage.TryReadParagonBoardName`;
all share one internal resolver. Per the durable opaque-id principle
(Appendix C) this convention is decoded once, library-side. See
Appendix A CL-20.

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
| 0x24 | DT_FIXEDARRAY[DT_INT] | `fUsableByClass` — per-class boolean (non-zero ⇒ usable); slot = the class's **eClass rank** (see below). On the verified build slots 0–7 carry the 8 classes; 8–10 are pad. |
| 0x50 | u32 | affix array `dataOffset` (== 104 for a well-formed glyph) — the structural well-formed guard |
| 104 / 108 / 112 | DT_SNO ×3 | up to three affix SNO ids (`0` / `0xFFFFFFFF` slots omitted) |

Some group-111 SNOs are short placeholder records — bounds-check before
reading `+104`.

**Glyph→class membership (FR-D3).** The slot index of a class in
`fUsableByClass` is its **eClass rank**: the position of the class when
the §6.5 PlayerClass roster is ordered ascending by the class's
`eClass` ordinal, read from the **PlayerClass record payload `+16`**.
On build `3.0.2.71886` the eClass ordinals are sparse and rank-compact
to 0–7:

| Rank (glyph slot) | Class | eClass (PlayerClass +16) | PlayerClass SNO |
|---|---|---|---|
| 0 | Sorcerer | 0 | 131965 |
| 1 | Barbarian | 1 | 169776 |
| 2 | Rogue | 3 | 199275 |
| 3 | Druid | 5 | 131966 |
| 4 | Necromancer | 6 | 199277 |
| 5 | Spiritborn | 7 | 1206232 |
| 6 | Paladin | 9 | 2079084 |
| 7 | Warlock | 10 | 2207749 |

This ordering is **data-driven** (computed from live eClass ordinals,
never hardcoded) and **over-determined**: it is independently
corroborated by (a) the explicitly-named `*_Necro` glyphs setting
exactly rank-4 (= Necromancer) and (b) the consumer's
empirically-validated Warlock = index 7 (= rank 7). Membership is keyed
to the **PlayerClass SNO id** — the shared class key with §6.5 / §6.6
(FR-D1/D2). A malformed/placeholder glyph (affix `dataOffset` at
`+0x50` ≠ 104, e.g. the `Axe Bad Data` junk SNO 732443, which
otherwise reads a spurious all-8 pattern) yields an **empty** set —
honest sentinel, never a silently-wrong class. Shipped surface:
`ParagonGlyphDefinition.UsableByClassSnoIds`, populated by
`Diablo4Storage.ReadParagonGlyph(int)` (byte-only `Parse(blob)` leaves
it empty — the ordering needs `CoreToc`). See Appendix A CL-18.

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

## 10. Diablo IV UI-scene format (group 46, `0xE4825AB8`)

The format behind the paragon render layout requested as **FR-C7**.
This is D4's generic UI-scene/data-binding SNO; the paragon board is one
instance of it. The byte format here is complete and was recovered
**standalone and clean-room** (no third-party data); the only work
outstanding is mechanical assembly + the over-determined pixel
verification described in §10.11.

> Spec authority: this D4-layer format is owned here (its own `CL-*`
> log: CL-9..CL-12). The consumer FR
> (`e:\Paragon\docs\fr-c7-paragon-render-layout.md`) references the
> pre-split `casc-format.md` only for historical reasons; the split is
> not re-merged. The converged public API is `docs/fr-c7-api-proposal.md`
> §7 (Round-11; amendable via the FR loop until the next NuGet publish, then frozen by release immutability).

### 10.1 Location and container

D4 UI screens are SNO **group 46** (CoreTOC type name `UI`), format
hash **`0xE4825AB8`** — peers `ActionBar`, `Armory`, `BuildViewer`,
`BrightnessDialog`. The paragon layout:

| SNO | id | Meta size |
|---|---|---|
| `ParagonBoard` | 657304 | 145,550 B |
| `ParagonBoardSelect` | 964599 | 34,481 B |

Container: `0xDEADBEEF` + the 16-byte SNO header → a root header at
`0x20` (root-struct offset `0x70`; a type/version word; offset/size/
count fields) → the embedded root-widget name `ParagonBoard_main` at
`0x80` → the widget graph.

The paragon render metric is not in the paragon record groups, the art
groups, or the texture atlases — all eliminated with evidence: group 63
`Paragon_*Nodes` are 113-byte tutorial triggers; group 29
`Paragon_*_Legendary_*` are node powers; groups 1/9/14/27 are art
(mesh/anim/VFX); group 42 is strings; group 44 `2DUI_Paragon*` are the
texture atlases (decodable, §6).

### 10.2 The D4 identifier hashes (reusable library-wide)

Every D4 serialization id is the DJB2 core `h = h*33 + ch` seeded
**0** (textbook DJB2 seeds 5381 — D4 does not):

| name | lowercase input | mask | identifies |
|---|---|---|---|
| `typeHash` | no | none (u32) | type / class / struct names |
| `fieldHash` | no | `& 0x0FFFFFFF` (28-bit) | struct field names |
| `gbidHash` | yes | none (u32) | GBIDs — this is `Diablo4.GbidHash` |

Self-verified: `gbidHash("ParagonNodeCoreStat_Normal") = 0x42C16A1B`,
the project's independently-known-good GBID. The 28-bit `fieldHash`
mask is why field-ids cluster `< 0x10000000`. This applies to every D4
SNO meta format; it is exposed as public API (`Diablo4.TypeHash` /
`FieldHash`) per the §10.10 contract.

### 10.3 Data-binding model and encoding

`0xE4825AB8` is a reflection-serialised, hash-addressed widget graph
of variable-size widget records. The **record header is pinned**
(verified across `ParagonBoard_main`@0x80 (17), `Template_ParagonBoard`
@0xA70 (21), `ParagonNodes_BaseLayer`@0x20F8 (22),
`ParagonNodes`@0x1E30 (12) — name lengths in parens):

```
nameStart                                  : name, NUL-terminated ASCII
classOff = nameStart + alignUp8(len+1) + 0x10
classOff + 0x00  u32  class id = typeHash(widget-class name)
classOff + 0x04  u32  0
classOff + 0x08  u32  0xFFFFFFFF  (sentinel)
… schema run + instance records follow
```

`alignUp8(n) = (n + 7) & ~7`. (The earlier "fixed name+0x28" model was
an over-generalisation from same-length names — see CL-13, now
resolved: the post-name fields sit after the name padded up to an
8-byte boundary, plus a constant `0x10`.) This makes a correct parser
possible: enumerate NUL-terminated identifier names, compute `classOff`,
require the `0xFFFFFFFF` at `classOff+0x08`, then read the schema run
and instance records below.

Widgets reference children by name-hash, not file offset (hence the
constant-heavy layout). Each field has two co-located parts:

- **Schema** — a packed run of **12-byte** entries
  `( fieldHash(name) , typeHash("DT_BINDABLEPROPERTY") , typeHash(DT_*
  underlying type) )`. Every field is a `DT_BINDABLEPROPERTY` of a
  `DT_*` type — D4's UI data-binding system. `0x1332C78D` =
  `typeHash("DT_BINDABLEPROPERTY")`.
- **Instance records** — fixed **56-byte (`0x38`)** records:
  `+0x00 u32 = 0x22` (record tag), `+0x04 u32` sub-tag (`0`/`3`
  observed), **`+0x08 u32` = the bound value**, `+0x0C..0x38` zero pad.
  Records are **positionally keyed** to the schema field order (the
  Nth record is the Nth schema field's value). `DT_INT`/`DT_SNO`/
  `DT_RGBACOLOR`/`DT_BYTE` values all read from the `+0x08` slot.

So a widget's `nWidth` = the `+0x08` of the 56-byte `0x22` record at
`nWidth`'s position in that widget's schema run. Observed live values
include `0x4B0` (1200) and `3`.

### 10.4 Type enum

| id (`typeHash`) | type |
|---|---|
| `0x1332C78D` | `DT_BINDABLEPROPERTY` (per-field binding marker) |
| `0xA4C42E02` | `DT_INT` |
| `0xE65047AD` | `DT_FLOAT` |
| `0x3D4646AB` | `DT_BYTE` |
| `0x3D47BD2C` | `DT_ENUM` |
| `0xE549F591` | `DT_CSTRING` |
| `0x8E266332` | `DT_RGBACOLOR` |
| `0xA4C45887` | `DT_SNO` |
| `0x2B0285C0` | `StringLabelHandleEx` |
| `0x6B1C5D9C` | (DT_* not yet named — residual, struct/vector-like) |

### 10.5 `ParagonBoard` schema

Field id → recovered name → type (count = occurrences). Type is known
for every field; names blank where the residual candidate set has not
yet matched (non-blocking — type classifies them):

| field id | name | type | n |
|---|---|---|---|
| `0x07F1EF79` | `nLeft` | DT_INT | 66 |
| `0x069EA64C` | `nRight` | DT_INT | 67 |
| `0x003DC5C1` | `nTop` | DT_INT | 76 |
| `0x0594CC83` | `nBottom` | DT_INT | 69 |
| `0x06F9158E` | `nWidth` | DT_INT | 86 |
| `0x02D88AE7` | `nHeight` | DT_INT | 74 |
| `0x0C2AFA21` | `dwAlpha` | DT_BYTE | 11 |
| `0x09A3F17B` | `rgbaTint` | DT_RGBACOLOR | 6 |
| `0x00957CB7` | (2nd DT_RGBACOLOR — lit/selected tint candidate) | DT_RGBACOLOR | 26 |
| `0x0789C1CD` | `hText` | StringLabelHandleEx | 52 |
| `0x0204DBB8` | `hTooltipText` | StringLabelHandleEx | 1 |
| `0x07DB38D3` | (primary texture/SNO ref) | DT_SNO | 27 |
| `0x01844A00` `0x0219D52D` `0x0C43C17C` `0x0CCBA90F` | (SNO refs) | DT_SNO | 1 each |
| `0x06AB76DE` `0x0CDB00E9` | (int) | DT_INT | 124 / 14 |
| `0x093CBAA8` `0x03D55658` | (enum) | DT_ENUM | 132 / 122 |
| `0x0C152636` + residuals | (DT_? `0x6B1C5D9C`) | DT_? | 93 / 1 |
| `0x02509B49` `0x008AB8D6` | (cstring) | DT_CSTRING | 3 |

The widget layout rect is `nLeft/nRight/nTop/nBottom/nWidth/nHeight`
(DT_INT, bindable); appearance is `rgbaTint` (+ the second
DT_RGBACOLOR), `dwAlpha`, `hText`/`hTooltipText`; textures are the five
DT_SNO fields (primary `0x07DB38D3`).

### 10.6 Standalone clean-room name recovery

Field/type names are **not stored in any SNO/CASC data file** (the
format is hash-keyed by design — one-way 28-bit `fieldHash`); they are
embedded in the **D4 client binary's** reflection registry. Recovery
is first-party, no third-party-JSON dependency:

1. String-extract printable identifiers from the locally-installed
   `Diablo IV.exe` (+ `diablo_iv_loader.dll`) — the user's own
   legally-obtained binary, processed in-tool, never shipped.
2. Hash candidates with `typeHash`/`fieldHash` (§10.2); match observed
   ids.
3. Expand the residue with D4 naming conventions
   (`n`/`fl`/`h`/`e`/`b`/`dw`/`sno`/`rgba`/`pt` × layout/widget terms).

This is a permanent library capability for any D4 SNO meta format.

### 10.7 FR-C7 geometry conclusions

- **No authored pixel constants.** A full 145 KB float scan finds no
  value cluster at any texture-native size, screen resolution, or node
  pitch; the rect fields are *bindable* (their values are the §10.3
  instance records, not literal layout constants). Node geometry is
  `ParagonBoardDefinition` grid (§7.1) + bound rect ints +
  texture-native sizes (§6), composed at runtime resolution. A literal
  `CellPitch` does not exist as stored data; the absolute px scale is
  permanently consumer-owned (same pattern as the 6 intrinsics / §3
  relight). The library returns the raw rects **and** the derived
  unitless ratios; the consumer owns only the resolution/zoom scalar.
- **Rarity tint.** Per-rarity colour is the bound `rgbaTint`
  (`0x09A3F17B`, DT_RGBACOLOR) on the *neutral* disc — the rarity
  fill-swatches and the orange ornate `A54E0DD1` are absent from the
  screen; the bound ornate is the gold `4A901508`. The consumer's
  shader-recipe model is correct, and the tint is a readable bound
  colour, not a per-rarity texture. The second DT_RGBACOLOR
  (`0x00957CB7`) is the candidate selected/relit tint.

### 10.8 Acceptance anchor (consumer oracle, dual-validated)

> **≈ 67.7 px / grid-step**, provenance **{zoom = 0 (smallest),
> render = 7680×2160, *Warlock Start* board, nothing selected}**;
> dual-validated ≤ 0.4 px: lattice autocorrelation 67.59/67.81 (square
> lattice); landmark span gate(10,0)→start(10,14) = 951.5 px ÷ 14 =
> 67.96. A known-grid-distance reference capture (two identified
> `(X,Y)` nodes, same provenance) supplements it (`Δpx ÷ Δgrid`).

**Reproduced — gate-2 PASSES.** The decode-true node-centre pitch is
the `Template_Node_Common` box = **100 ref units** (uniform square
tiling); `CanvasRef.Height = 1200` ⇒ `PitchRef = 100/1200`,
`DiscRef = (100 − 2×7 `Node_IconBase` insets)/1200 = 86/1200`. A single
uniform 100-ref box predicts a *square uniform* lattice at one scale —
exactly what the consumer's dual-validated anchor shows: autocorr
67.59(X)/67.81(Y) (square, ratio 0.997) and the gate→start span 67.96
**all ÷ the decode-true 100-ref pitch converge to ≈0.677 px/ref
(≤0.4 px)**. Two independent oracles + the square-lattice prediction
all consistent with the authored value = the over-determination
satisfied (proof, not inference). `RenderRatios.Provisional` is
therefore **false**. `IconCellFactor` on the consumer side = this C7
unitless ratio × the consumer-owned resolution/zoom basis (their
implied scale ≈0.677 px/ref at the 7680×2160/zoom-0 provenance).
Refinements **completed (decode-true)**: `NodeAvailableGlow` (ornate),
`Node_Icon` (symbol) and `GlyphNodeGlow_Revealed` (socket pulse ring)
have no own `nWidth` — they fill the 100-ref node box (minus symmetric
insets), so `OrnateOverDisc = SymbolOverDisc = SocketRingOverDisc =
100/86 ≈ 1.163`. `GreyRingOverDisc = 0` — the grey rim ring is **not
bound in `ParagonBoard`** (app-drawn/procedural, like the overlays;
the truthful answer, not a gap). `StateElements.Tint` / `Animation`
are **null and that is the decoded answer**: scanning the whole scene,
`rgbaTint` is bound only on non-node widgets (`BlackScreen`,
`Usage_Slot_2`, `Template_GlyphAura_Tile`) — **no per-rarity tint is
authored anywhere**, definitively confirming §2.3 (per-rarity colour is
a fixed shader recipe, permanently the consumer's, not data); and no
authored float anim fields exist on the pulse widgets (the pulse is
engine-driven). `PitchRef`/`DiscRef` decode-true + anchor-confirmed;
every `RenderRatios`/`StateElements` value is now either a decoded
number or a documented, evidence-backed "not in the data".

The *Warlock Start* view is **axis-aligned**, not rotated ~45° (CL-10):
the lattice autocorrelation is a clean square. `BoardRotationQuadrant`
is decoded from `ParagonNodes_BoardRotationLayer` as a 90°-multiple
index (0/1/2/3) and must resolve to 0 at this provenance — 45° is
unrepresentable by the contract type (§10.10, C-a).

### 10.9 Reconnaissance instrument

`build/SnoScan` (in `e:\Casc`, not shipped, not in the solution —
same posture as `build/TileIcon`) drives the real
`WiseOwl.Casc.Diablo4` decoder against the live install: `groups`,
`find`, `strings`, `scan`, `f32`, `members`, `dh` (the D4 hashes),
`crack` (wordlist → id matching), `dump`. Keeps the RE on our own
library; `e:\Paragon` stays read-only.

### 10.10 Converged API contract (Round-11 — agreed; amendable until publish)

Consensus reached (consumer Round-11 `8bc134c` + Round-12 ack). The
**agreed working contract is `docs/fr-c7-api-proposal.md` §7** (amendable via the loop until the next NuGet publish, then frozen by NuGet version-immutability);
its salient points:

- `ReadParagonRenderLayout()` returns **both** raw `WidgetRect` ints
  (audit / the CL acceptance row) **and** library-derived unitless
  `RenderRatios` (primary consume path), flagged `Provisional` until
  they reproduce the §10.8 anchor — deriving pitch from rects + grid is
  the library's job, not the consumer's (boundary).
- **`BoardRotationQuadrant : int`** ∈ {0,1,2,3} = 0/90/180/270 only;
  Start = 0; 45° unrepresentable by construction (enforces CL-10).
- Per-`StateElements` optional `RgbaTint?` (per rarity×state bound
  `rgbaTint`) plus an optional `LitTint?` (the second DT_RGBACOLOR on
  `selected` keys, if it is the relit colour).
- Texture handles are raw `uint`, never pre-resolved.
- **State contract = 15 baked + 3 overlay = 18** `StateElements`
  (round-4b's "17" was the arithmetic slip `4×2+3+2+2=15`; CL-11). The
  verbatim 18-row acceptance matrix is `fr-c7-api-proposal.md` §7.2.
- `Diablo4.TypeHash`/`FieldHash` exposed; a generic `ReadUiScene(snoId)`
  (raw widget graph only — no evaluator/imaging/policy) is also shipped,
  with `ReadParagonRenderLayout()` the thin typed projection on top.
  The generic surface has independent acceptance (CL-12).

### 10.11 Outstanding (assembly only — no external dependency)

The format is fully decoded and the shipped, header-pinned
`Diablo4Storage.ReadUiScene` reads real bound values. Authoritative
values (build `3.0.2.71886`, the correct parser — *not* the exploratory
`SnoScan widgets` heuristic, which over-attributed by nearest name; see
CL-14):

| Widget | Bound rect (UI ref units) |
|---|---|
| `ParagonBoard_main` (root) | `nWidth 1920`, `nHeight 1200` ⇒ **CanvasRef = 1920×1200** (both parsers agree) |
| `ContentBG` | `nWidth 2300` (the board content backing) |
| `ParagonNodes` (node container) | own rect **runtime-bound (0)** — not an authored constant (the bindable-rect premise, §10.7) |
| `Template_Node_Common` | **`nWidth ≈ 100`** — the per-node element template |
| `SidePanel_Content` | `nWidth 450` (chrome — *not* the node grid; the value earlier mis-attributed to `ParagonNodes`) |
| per-state / overlay | named widgets `Common_Node_BG_Black` / `Common_Node_Revealed` / `Node_Purchasable` / `Arrow_{Top,Right,Bottom,Left}` (pointer triangles) / `Connector_{Top,Right,Bottom,Left}` (connector bars) |

These are facts from the `+0x08` slot of the positional 56-byte `0x22`
records via the authoritative header-pinned parser.

**Per-state texture binding (decode-true).** Node textures are bound
**not** via the `DT_SNO` field but via field **`0x0C152636`** of type
**`0x6B1C5D9C`** (a texture/material-handle DT type; both still
unnamed — a refinement, identified by behaviour) on specifically-named
widgets:

| Widget | Bound handle | §2.2 role |
|---|---|---|
| `Node_IconBase` | `0x1D166DC7` | base disc |
| `NodeAvailableGlow` | `0x4A901508` | gold ornate (Rare/Legendary) |
| `GlyphNodeGlow_Revealed` / `_Purchased`, `Usage_Slot_2` | `0xBED4CF21` | socket pulse ring |

Only these are bound in `ParagonBoard`. The grey rim ring
(`0x87A89F86`), the selection ring, the connector bars
(`0x77ECA3A8`/`0x288DE11F`) and the four pointer triangles are **absent
from the scene data** — confirming they are **app-drawn / procedural
overlays** (consistent with FR §2.5 and the Round-11 `overlay.*` note:
"not per-node baked"). Per-rarity differentiation is the bound
`rgbaTint` (§10.7 / §2.3 — shader tint on the shared neutral disc, not
per-rarity textures). So the §7.2 18-row matrix maps to a **small set
of shared decode-true elements** (disc + gold ornate + pulse) ×
per-rarity `rgbaTint`, with the 3 `overlay.*` rows carrying *no* bound
scene data (the consumer keeps its catalogued procedural handles, as
the FR anticipated) — not 18 distinct baked layer-lists.

Remaining:

1. Populate `StateElements` from the decode-true elements above
   (`Node_IconBase`/`NodeAvailableGlow`/`GlyphNodeGlow_*` handles +
   rects + the per-rarity `rgbaTint` + the pulse `AnimSpec`); overlay
   rows carry the documented "app-drawn, not in scene data". No
   fabricated rows.
2. Derive `pitchRef` from the `Template_Node_Common` element + the node
   layout + the `ParagonBoardDefinition` Warlock-Start grid (§7.1);
   verify it reproduces ≈67.7 px/grid at the §10.8 provenance and is
   cross-widget consistent. `RenderRatios.Provisional` stays `true`
   until this passes — no pitch number asserted before it (the oracle
   is the *check*, never the source).
3. Resolve residual unnamed field-ids (type already known; a
   refinement, non-blocking).
4. Implement the §10.10 agreed contract + the verbatim 18-row
   acceptance matrix. The consumer is on HOLD; no public surface is
   added before step 2 passes.

### 10.12 Start/gate composite — the 0x58-block binding (FR-C8, CL-23)

FR-C7 §10.11 / the FR-C7 `Project()` concluded "no distinct gate/start
texture is bound in ParagonBoard" and collapsed `start.*`/`gate.*` to
the neutral disc. **That was wrong** — it followed from the §10.3
decode modelling only the **56-byte `0x22`** instance record. The
start/gate node templates bind their composite layers via a **distinct
fixed 0x58-byte (88-byte) block**:

```
+0x00  u32  tag        (2 = a bound layer value; 0x22 = a flag/other)
+0x04  u32  0
+0x08  u32  value       (the bound value — e.g. a texture handle)
+0x20  u32  ownerClassId (the owning widget's class id, back-ref)
+0x28  u32  0xFFFFFFFF   (sentinel — validates the block)
```

These blocks live inside the `Template_Node_Starter`
(ClassId `0x1E3077C7`) and `Template_Node_Quest` widget spans of
**ParagonBoard SNO 657304** (a descriptor table of ~0x28-stride entries
points at them). The `0x22`/56-byte scan never matched them, so they
were dropped (the consumer's "1 of 17 fields has a value").

Decoded ordered scene handles (build `3.0.2.71886`, raw-byte verified,
matching the consumer's owner-verified atlas oracle **exactly**):

| Template | Ordered tag-2 handles (back→front) | Atlas / role |
|---|---|---|
| `Template_Node_Starter` (Start) | `0xA0F996FE`, `0xF8312CA8` | filigree (`2DUI_Paragon_transparentElements`), grey hexagon (`2DUI_ParagonNodes`) |
| `Template_Node_Quest` (Gate/Exit) | `0xA0F996FE`, `0xC2DF4786`, `0x0E6B6249` | filigree; ornate square **selected** `0xC2DF4786` / **unselected** `0x0E6B6249` |

The per-node **symbol** drawn on top (`0x35B6E536` spider for Start
node 2458702; `0xE1316816` portal for Gate node 994337) is the
`ParagonNode.HIconMask` (§7.2) — correctly **not** a scene layer
(per-node, already exposed via `ParagonNodeDefinition` /
`TryGetIconFrame`). Start/gate use **no disc** (`0x1D166DC7` absent).

**Surfaced:** `UiWidget.ExtraLayerValues` (lossless raw, scope-B — the
ordered 0x58-block values per widget) and the typed
`ParagonRenderLayout` `start.*`/`gate.*` `States.Layers` (handles only;
catalog-validated by `ReadParagonRenderLayout` so int params like `20`
that share tag 2 are excluded — no fabrication). **Not decoded** (left
default, honest — consumer owns, FR-C7 §6 precedent): per-layer
rect/scale, the shader brightness/tint pass, and the exact
unselected↔selected ornate-square state split (the handle *identities*
are the consumer's confirmed RE; the data-side state binding is
located-but-not-pinned). Verdict: **#2 located, with the data** — not
data-silent.

### 10.13 Directional arrows / connectors + per-layer rect + animation (FR-C8 R5/R6, CL-24)

**R6 — directional pointer & connectors are NOT procedural (FR-C7 §6
correction).** The four `Arrow_{Top,Right,Bottom,Left}` widgets bind
the pre-oriented red arrow art, and `Connector_{Top,Right,Bottom,Left}`
bind the connector art, each with an **authored rect**, via the
standard texture-handle field (`0x0C152636` / type `0x6B1C5D9C`) on the
ordinary §10.3 **0x22** path — *not* a 0x58 block. FR-C7 missed them
for two compounding reasons: (a) it hardcoded the `overlay.*` rows
empty, and (b) the texture handle is each widget's **last** 0x22
record, whose 56-byte body straddles the next widget's `nameStart`, so
`UiScene.Parse`'s `p + RecordSize <= to` bound dropped it. Fixed
surgically: the tail record's value (`+0x08`) is collected when it
fits even though the body straddles (the full-record scan for every
other record is byte-identical — no FR-C7 regression). Cardinal map
(scene-decoded, build `3.0.2.71886`):

| Widget | Handle | Atlas frame (2DUI_Paragon_transparentElements 2061536) |
|---|---|---|
| `Arrow_Top` | `0xD51CAB25` | 107×86, points up |
| `Arrow_Right` | `0x6D3CB8DE` | 86×106 |
| `Arrow_Bottom` | `0x8EEAC178` | 106×86 |
| `Arrow_Left` | `0xB6D8C741` | 87×106 |
| `Connector_*` | `0x77ECA3A8` / `0x288DE11F` | connector bars |

`overlay.pointerTriangle.Layers` / `overlay.connectorBar.Layers` now
carry these (handle + decoded `Rect`), T/R/B/L. `overlay.selectionRing`
has no scene widget → genuinely engine-drawn (stays empty — honest, not
fabricated).

**R5 — start/gate per-layer rect/scale/tint: definitively NOT
authored.** The §10.12 0x58 layer blocks are **handle-only**: the
entire 88-byte block is `{tag@+0, value@+0x08, ownerClassId@+0x20,
0xFFFFFFFF@+0x28}` with every other word zero — no rect, scale, alpha
or tint. The pointing descriptor record references a Common-template
node child, so the start/gate frame layers **inherit the referenced
node-element box** (the `NodeTemplate` 100-ref box, §10.11); there is
no per-layer authored rect to surface. So `NodeElement.Rect`/`Alpha`
for `start.*`/`gate.*` layers stays `default` (the honest decoded
answer — size them to `NodeTemplate`, no eyeballed fraction needed and
none exists in the data). The arrow/connector widgets (above) **do**
carry an authored rect — surfaced.

**Animation (legendary/socket glow pulse): engine-driven — reaffirmed,
not authored.** The looping per-node glow pulse has no authored
frame-order/period in `ParagonBoard`: the glow widgets bind no
period/min/max float, and the scene's `Storyboard_*` widgets are UI
transitions (`Black_FadeIn/Out`, `Glyph_Expand/Collapse`,
`Board_Rotate`, `RefundAll_*`, `ScaleTest`, `CoreStatsActive`), not a
per-node pulse loop (48 DT_FLOAT fields scene-wide, none binding the
glow timing). This reaffirms FR-C7 (`AnimSpec = null` is the
evidence-backed decoded answer): the layer **order** is delivered
(`States.Layers` back→front); the pulse **timing** is an engine shader
loop — the consumer bakes a representative static frame (FR-C7 §6).
Definitive #3 for the timing; reopen with an in-game oracle if a build
shows authored pulse timing. See Appendix A CL-24.

## 11. Non-paragon typed record readers (C6)

The B1–B6 scope-freeze was **lifted by owner decision 2026-05-17**
(Appendix C). C6 adds typed readers for the non-paragon record groups
the consumer needs to eliminate `D4Extract`. Consistent with the
library boundary, these decode **identity + localized text only** —
the deep gameplay records (multi-KB skill/item engine structs) remain
the consumer's stat-effect model; the library does not fabricate a
model it cannot verify. Localized fields use the §6.7 sibling
convention (empty when absent — honest sentinel). All offsets
payload-relative (base `0x10`).

### 11.1 `PlayerClassDefinition` (group 74, `.prd`)

| Offset | Type | Field |
|---|---|---|
| 0 | DT_INT | `snoId` |
| 16 | DT_INT | `eClass` — internal class enum ordinal |

`eClass` is sparse but stable (Sorcerer 0, Barbarian 1, Rogue 3,
Druid 5, Necromancer 6, Spiritborn 7, Paladin 9, Warlock 10 on build
`3.0.2.71886`); ranking the real-class roster by it gives the glyph
`fUsableByClass` slot order (§7.3 / FR-D3 — this is the same field
CL-18 relies on, now exposed typed). Surface:
`Diablo4Storage.ReadPlayerClass(int)`. CL-21.

### 11.2 `PowerDefinition` (group 29, `.pow`)

Identity (`snoId@0`) + localized `Name`/`Description` from the §6.7
sibling table `Power_<snoName>`, labels `name`/`desc`. The power's
gameplay record (≈6 KB) is **not** decoded — consumer domain. (Note:
an inline `szName` exists at payload `+8` for *some* powers but is
absent for many — e.g. `CAMP_*` — so the sibling table is the reliable
name source, not that offset.) Anchor: power `2521393` →
`name` `Fathomless`. Surface: `Diablo4Storage.ReadPower(int, locale)`.
CL-22.

### 11.3 `AffixDefinition` (group 104, `.aff`)

Identity (`snoId@0`) + localized `Description` from sibling
`Affix_<snoName>`, label `Desc`. Affix magnitude/operation modeling is
the consumer's (glyph-affix magnitudes are §7.4). Anchor: affix
`2586362` → Desc `Your attacks Critically Strike …`. Surface:
`Diablo4Storage.ReadAffix(int, locale)`. CL-22.

### 11.4 `ItemDefinition` (group 73, `.itm`)

Identity (`snoId@0`) + localized `Name`/`Flavor`/`TransmogName` from
sibling `Item_<snoName>`. Item stat/affix/power modeling is consumer
domain. Anchor: item `223287` (`1HAxe_Unique_Generic_001`) → Name
`The Butcher's Cleaver`, TransmogName `Cadaver Chopper`. Surface:
`Diablo4Storage.ReadItem(int, locale)`. CL-22.

All four: byte-only `Parse(blob)` yields identity only (localized
fields empty — they need `CoreToc`); the deep binary beyond the
documented fields is deliberately not decoded (boundary, not a gap —
no fabricated values, mirroring the FR-C7 discipline).

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
  The paragon render metric is a group-46 UI-scene SNO (type `UI`),
  format hash `0xE4825AB8`, `ParagonBoard` SNO 657304 — not the paragon
  record/art/atlas groups (all eliminated with evidence). D4's
  serialization hash is **DJB2 seeded 0** (`typeHash` full u32,
  `fieldHash` 28-bit-masked, `gbidHash` lowercased = the existing
  `Diablo4.GbidHash`), self-verified vs the known GBID `0x42C16A1B` and
  reusable across every D4 SNO meta format. The format is a
  reflection / data-binding widget graph (12-byte schema entries +
  56-byte `0x22` instance records, value at `+0x08`, positionally
  keyed); the `ParagonBoard` schema is fully type-classified with the
  geometry/appearance fields named (`nLeft/nRight/nTop/nBottom/nWidth/
  nHeight`, `rgbaTint`, `dwAlpha`, the `DT_SNO` texture fields),
  recovered standalone by string-extracting the locally-installed
  `Diablo IV.exe` (no third-party-JSON dependency — names are absent
  from SNO data by design but present in the client reflection
  registry). Per-rarity colour is the bound `rgbaTint` on the neutral
  disc (FR §2.3 confirmed). Full reference: §10. The byte format is
  complete; the remaining work is the §10.11 assembly + the §10.8 67.7
  reproduction, and no pitch number is asserted until that passes.
- **CL-10 — paragon board rotation is a 90° quadrant, never 45°.** The
  FR's "~45°" assumption does not hold: the *Warlock Start* lattice
  (autocorrelation at `{zoom 0, 7680×2160, nothing selected}`) is a
  clean square, axis-aligned, ≈67.7 px/grid (dual-validated ≤0.4 px,
  §10.8). Rotation is decoded from `ParagonNodes_BoardRotationLayer` as
  a 90°-multiple index and is exposed as `BoardRotationQuadrant ∈
  {0,1,2,3}` (§10.10, C-a) so 45° is unrepresentable by construction;
  it resolves to 0 at this provenance.
- **CL-11 — FR-C7 state contract is 15 baked + 3 overlay = 18.** The
  consumer's round-4b summary stated "17"; the correct count is
  `4 rarities × 2 (unselected/selected) + 3 socket + 2 gate + 2 start =
  15` baked, plus 3 `overlay.*` = **18** `StateElements`. The verbatim
  18-row key table is the acceptance matrix in
  `docs/fr-c7-api-proposal.md` §7.2; `casc-format.md`/round-4b "17"
  references are superseded by this.
- **CL-12 — generic `ReadUiScene` is an independent surface.** Owner
  scope decision (over the library's recommendation): in addition to
  the typed `ReadParagonRenderLayout()`, ship a generic
  `ReadUiScene(snoId)` for any `0xE4825AB8` SNO, returning the raw
  decoded widget graph only (names / `typeHash` / `fieldHash` / raw
  bound values / schema) — no evaluator, imaging, or policy (the
  permanent boundary). Its acceptance is tracked independently of the
  paragon projection so neither gates the other (§10.10).
- **CL-13 — widget-record header framing was over-generalised.** A
  provisional model ("name+0x28 = class id, name+0x30 = sentinel,
  name+0x60 = block") was inferred from a few same-name-length widgets
  and asserted in an earlier §10.3. It does not generalise:
  `ParagonBoard_main`@0x80 has the class id at name+0x28 but
  `ParagonNodes`@0x1E30 has the `0xFFFFFFFF` sentinel there — the
  header offsets are relative to a padded/aligned name field or the
  enclosing record start, not the raw name start. §10.3 corrected to
  state the header framing is unpinned (the active §10.11 sub-problem);
  the two value encodings (12-byte separator-keyed schema entries;
  56-byte `0x22` instance records, value@+0x08) are independently
  proven and unaffected. Caught when the `walk` tool mis-parsed
  `ParagonNodes` — recorded rather than built upon.
  **RESOLVED:** the header is `classOff = nameStart + alignUp8(len+1)
  + 0x10`; class id at `classOff`, `0xFFFFFFFF` sentinel at
  `classOff+0x08` — verified across four widgets of name lengths
  12/17/21/22 (§10.3). The over-generalised model is fully superseded;
  a correct (non-heuristic) parser is now possible.
- **CL-14 — exploratory `SnoScan widgets` over-attributed rects; the
  header-pinned `ReadUiScene` is authoritative.** The exploratory tool
  associated a schema run with the *nearest preceding name*, which
  mis-attributed `nWidth 450 / nHeight 1115` to `ParagonNodes`; the
  shipped header-pinned `ReadUiScene` (correct parser, validated by the
  `0xFFFFFFFF` sentinel at `classOff+0x08`) shows that `450` is
  `SidePanel_Content` (chrome) and `ParagonNodes`'s own rect is
  runtime-bound (0) — consistent with the bindable-rect premise
  (§10.7). The real per-node element is `Template_Node_Common`
  (`nWidth ≈ 100`); the per-state/overlay widgets are named
  (`Common_Node_*`, `Node_Purchasable`, `Arrow_*`, `Connector_*`).
  `CanvasRef = 1920×1200` is unaffected (both parsers agree). Caught
  when the typed-projection integration test asserted the
  exploratory-derived `450` and the correct parser returned `0` —
  the §10.11 table was corrected to the authoritative values and the
  exploratory tool is downgraded to recon-only.

- **CL-15 — ParagonBoard localized name is a sibling StringList table,
  name-keyed (FR-D1).** `ParagonBoardDefinition` (group 108) carries no
  name/name-id/GBID; the in-game board name is in the group-42
  StringList SNO whose CoreTOC name is `"ParagonBoard_" + boardSnoName`,
  under label `Name`. The two SNO ids have **no fixed offset** — an
  early observation that Warlock's table is `boardSnoId − 1` was
  *not* generalised: Sorcerer's is not (`Paragon_Sorc_00` 939773 →
  `ParagonBoard_Paragon_Sorc_00` 1111181), so resolution is strictly
  CoreTOC-name-keyed. Convention recorded in §6.4; acceptance
  (`Paragon_Warlock_00` → `Start`, `Paragon_Warlock_03` → `Dynamism`
  enUS / `Dynamismus` deDE) is asserted by the
  `ReadParagonBoardName_resolves_localized_board_name` integration
  test. Shipped surface: `Diablo4Storage.TryReadParagonBoardName` /
  `ReadParagonBoardName`; `SnoGroup.StringList = 42` named. Raw value
  only, no fallback policy (consumer owns the SnoName fallback).

- **CL-16 — ParagonBoard class/index is the name convention, decoded
  library-side (FR-D1 rescoped).** The `ParagonBoard` record has no
  class/index field (the 1820 B record is fully accounted for by §7.1).
  The only first-party source is the SNO name
  `Paragon_<ClassToken>_<Index>`. Per the durable opaque-id principle
  (Appendix C, mirrored 2026-05-17) the convention is decoded once,
  library-side, not by a consumer regex: token = between `Paragon_` and
  the final `_`; index = trailing integer (variable width —
  `Paragon_Spirit_0` is a single digit, parse as int); class = the
  **unique case-sensitive prefix** of exactly one §6.5 PlayerClass
  roster SnoName (`Sorc`→`Sorcerer`, `Spirit`→`Spiritborn`, …). No
  match / ambiguity throws `CascFormatException` (re-verify signal).
  Recorded in §6.6; acceptance (`Paragon_Warlock_00`→Warlock/idx 0,
  `Paragon_Warlock_03`→Warlock/idx 3, `Paragon_Spirit_0`→Spiritborn/
  idx 0) asserted by `ReadParagonBoard_resolves_typed_class_and_index`.
  Shipped: `ParagonBoardDefinition.ClassSnoId/.ClassSnoName/.BoardIndex`
  populated by `Diablo4Storage.ReadParagonBoard(int)`; byte-only
  `Parse(blob)` leaves honest `0`/`""`/`-1` sentinels.

- **CL-17 — Character-class roster + localized names (FR-D2).** The
  playable-class roster is SNO group 74 (`PlayerClass`), independent of
  paragon. Localized name = the `General` StringList table (SNO 4118)
  label `"PlayerClass" + SnoName + "Male"` (markup-free; base
  `PlayerClass<SnoName>` carries `|5sing:plur` markup). Real-class
  membership is data-driven: a group-74 entry is a class iff that label
  exists — excludes `Axe Bad Data` (159433) with no hardcoded list.
  Stable key = the PlayerClass SNO id. Recorded in §6.5; acceptance
  (roster = the 8 classes incl. Warlock/Paladin/Spiritborn, junk
  filtered, locale-aware) asserted by
  `ReadCharacterClasses_returns_first_party_roster`. Shipped:
  `Diablo4Storage.ReadCharacterClasses(locale)` →
  `IReadOnlyList<CharacterClass>` (SnoId/SnoName/DisplayName), ordered
  by SnoId, cached per locale.

- **CL-18 — Glyph→class membership = `fUsableByClass` indexed by eClass
  rank (FR-D3).** `ParagonGlyphDefinition` (group 111) carries a
  per-class boolean fixed array `fUsableByClass` at payload `+0x24`.
  The slot for a class is its **eClass rank**: position when the §6.5
  PlayerClass roster is sorted ascending by the class's `eClass`
  ordinal (PlayerClass record payload `+16`; sparse 0/1/3/5/6/7/9/10 →
  rank-compact 0..7). Decoded library-side per the durable opaque-id
  principle — **not** a consumer bit-order guess and **not** the
  Maxroll `classFilter`. The mapping is **over-determined**: the
  explicitly-named `*_Necro` glyphs set exactly rank 4 (= Necromancer)
  and the consumer's empirically-verified Warlock = index 7 (= rank 7)
  both independently confirm the eClass-rank derivation; Sorcerer = rank
  0 cross-checks (Intelligence_Main glyph). Well-formed guard: affix
  `dataOffset` at payload `+0x50` == 104 — the `Axe Bad Data` junk SNO
  (732443, a 120-byte placeholder) otherwise reads a spurious all-8
  pattern, so it is gated to an empty set. Recorded in §7.3; acceptance
  (Warlock-usable → 2207749; Sorcerer-only excludes Warlock; `_Necro`
  → Necromancer; multi-class → full set; junk → empty) asserted by
  `ReadParagonGlyph_resolves_usable_by_class`. Shipped:
  `ParagonGlyphDefinition.UsableByClassSnoIds` (the shared class key),
  populated by `Diablo4Storage.ReadParagonGlyph(int)`; byte-only
  `Parse(blob)` leaves it empty.

- **CL-19 — FR-14 `SnoFolder.Child` acceptance pinned.** The
  id-keyed resolver was always folder-generic; the gated acceptance is
  now closed with a concrete build-`3.0.2.71886` anchor: SNO `1015186`
  (group 71, `AmbS_EMT_Dungeon_AncientsSand`) resolves
  `Base\Child\1015186-0` to non-empty bytes; a non-existent sub-id is a
  clean miss (no throw). The full census is ≈547,244
  `base/child/<id>-<n>` paths (`CascStorage.DiagnosticPaths`; SnoScan
  `childpaths` recon). The `Resolves_child_folder_by_id` test no longer
  self-skips. Re-verify trigger: Appendix D.

- **CL-20 — sibling-StringList convention generalized (FR-D1 → C6).**
  §6.4 (ParagonBoard `Name`) is one case of the general rule recorded
  in §6.7: localized text is the group-42 SNO `"<TypePrefix>_" +
  recordSnoName`, name-keyed via `CoreToc`. Verified prefixes/labels:
  `Item_`(Name/Flavor/TransmogName/Description), `Affix_`(Desc),
  `Power_`(name/desc), `ParagonBoard_`(Name). One internal resolver
  backs `TryReadParagonBoardName` + the C6 readers; raw text, honest
  empty when absent.

- **CL-21 — `PlayerClassDefinition.eClass` typed (C6, §11.1).** The
  class enum ordinal at PlayerClass payload `+16` (the field CL-18
  ranks for the glyph slot order) is now exposed via
  `Diablo4Storage.ReadPlayerClass(int)`. Anchors: Warlock 2207749→10,
  Sorcerer 131965→0, Necromancer 199277→6 (consistent with CL-18).

- **CL-22 — Power/Affix/Item typed readers (C6, §11.2–11.4).**
  Identity + §6.7 sibling-localized text only; deep gameplay records
  not decoded (boundary, not a gap — no fabricated values). Anchors
  (build `3.0.2.71886`, enUS): Power `2521393`→`Fathomless`; Affix
  `2586362`→Desc `Your attacks Critically Strike …`; Item
  `223287`→Name `The Butcher's Cleaver`, Transmog `Cadaver Chopper`.
  Asserted by `C6_typed_readers_decode_identity_and_localized_text`.

- **CL-23 — start/gate composite IS in ParagonBoard; the FR-C7
  "no gate/start texture" was wrong (FR-C8).** §10.3 modelled only the
  56-byte `0x22` instance record; the start/gate node templates bind
  their layers via a distinct fixed **0x58-byte block** (tag@+0,
  value@+8, ownerClassId@+0x20, `0xFFFFFFFF`@+0x28) the scan never
  matched, so `Project()` collapsed `start.*`/`gate.*` to the neutral
  disc and the raw `UiScene` surfaced the templates as near-empty.
  Located, raw-byte verified, oracle-exact (§10.12):
  `Template_Node_Starter` → `0xA0F996FE`,`0xF8312CA8`;
  `Template_Node_Quest` → `0xA0F996FE`,`0xC2DF4786`,`0x0E6B6249`; the
  per-node symbol is `HIconMask` (not a scene layer); no disc.
  Shipped: `UiWidget.ExtraLayerValues` (lossless raw) + the corrected
  typed `start.*`/`gate.*` `States.Layers` (catalog-validated, no
  fabrication). Per-layer rect/scale, shader brightness, and the exact
  unselected↔selected split are **located-not-pinned** → left default
  (consumer-owned, FR-C7 §6 precedent). Asserted by
  `ReadParagonRenderLayout_decodes_start_gate_composites`. Verdict:
  **#2 located, with the data** (not data-silent).

- **CL-24 — directional arrows + connectors are bound, not procedural;
  start/gate per-layer rect is not authored; glow animation is
  engine-driven (FR-C8 R5/R6).** §10.13. (a) `Arrow_*` bind the four
  cardinal arrow handles (`0xD51CAB25`/`0x6D3CB8DE`/`0x8EEAC178`/
  `0xB6D8C741`) and `Connector_*` the connector handles (`0x77ECA3A8`/
  `0x288DE11F`), each with an authored rect, via the standard 0x22
  texture-handle field — FR-C7's "overlays procedural / not in data"
  was wrong (a CL-23-family miss). Root parser cause: a widget's
  **last** 0x22 record straddles the next `nameStart`; the
  `p+RecordSize<=to` bound dropped it — fixed surgically (full-record
  scan byte-identical; only the straddling tail value is now also
  collected). `overlay.pointerTriangle`/`overlay.connectorBar` now
  populated; `overlay.selectionRing` genuinely empty (no widget). (b)
  start/gate 0x58 blocks are handle-only ⇒ no per-layer rect/scale/tint
  authored (inherit the `NodeTemplate` box) — definitive, `Rect`/
  `Alpha` stay `default`. (c) the per-node glow pulse has no authored
  timing (no period float; the `Storyboard_*` widgets are UI
  transitions) ⇒ engine-driven, `AnimSpec=null` reaffirmed (FR-C7) —
  definitive #3; layer *order* is delivered, *timing* is an engine
  shader loop. Asserted by
  `ReadParagonRenderLayout_decodes_directional_arrows` (+ the corrected
  `..._decodes_proven_structure` connectorBar assertion).

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
catalog, and the typed paragon/GameBalance **record decoders** (§§7–8)
plus the **C6 non-paragon readers** (§11: PlayerClass/Power/Affix/Item
— identity + sibling-localized text) — **raw fields only**.

It does **not** own (consumer policy, authoritative in `e:\Paragon`):
formula evaluation/recursion, the 6 calibrated engine intrinsics, the
scoring/objective model, the relight/disc+symbol composite calibration,
or the app's bundled-JSON schema. The library ships **no formula
evaluator at all**, by decision.

**FR-16 / C6 (scope-freeze lifted 2026-05-17, owner).** The earlier
"B1–B6 + existing, FROZEN" line is superseded: C6 typed readers ship
(§11). The boundary still holds at *modeling* — the library decodes
**identity + the verifiable raw/localized fields**; it does **not**
fabricate a stat-effect model of the multi-KB Power/Item engine
records (that, plus scoring/evaluation, stays the ParagonOptimizer
domain spec). The library will not grow scoring/evaluation APIs.
Round-2/3 + C6 disposition is tracked in `docs/feature-backlog.md`.

**Durable principle — SNO names are opaque ids (2026-05-17, owner;
mirrored here from `fr-d1-paragon-board-name.md §3` /
`wiseowl-casc-diablo4-requirements.md §1`).** A consumer treats every
SNO **name** as an **opaque, stable id** and **never decomposes its
substructure** to recover semantic fields. Any D4 **naming convention**
(e.g. `Paragon_<Class>_<NN>`) is a data mapping in the **same category
as a byte layout**: decoded **once, library-side**, documented with a
`CL-*` row and an Appendix D re-verify trigger, and exposed **typed** —
never re-implemented as a consumer regex that drifts silently when
Blizzard renames/relocalizes/extends it. *"It's a readable string not
bytes" does not move the boundary.* Applied: §6.4 (board name, CL-15),
§6.6 (board class/index, CL-16), §6.5 (class roster, CL-17), §6.7
(sibling convention generalized, CL-20), §11 (C6 readers). Decoding
such a convention library-side is in-boundary; "Readable string not
bytes" never makes name-parsing a consumer concern.

## Appendix D — source & re-verification

- Clean-room; cross-checked against the permissively-licensed references
  in `THIRD-PARTY.md` (incl. `alkhdaniel/diablo-4-string-parser` for the
  standalone `.stl` cross-check). No third-party source incorporated.
- Verified against Diablo IV build `3.0.2.71886` (`.build.info` Build
  Key `522f2f30f1eb0e32af225966b8ac91d1`).
- **Re-verify trigger:** the `.build.info` Build Key changes (seasonal
  patch). Re-run the integration tests; update Appendix A on any drift.
- **FR-14 / C6 acceptance anchors (build `3.0.2.71886`, enUS):**
  Child — SNO `1015186` (group 71) `Base\Child\1015186-0` non-empty.
  PlayerClass — `2207749`→eClass 10, `131965`→0, `199277`→6.
  Power — `2521393`→`Fathomless`. Affix — `2586362`→Desc
  `Your attacks Critically Strike …`. Item — `223287`→Name
  `The Butcher's Cleaver`, Transmog `Cadaver Chopper`. These are the
  `Resolves_child_folder_by_id` /
  `C6_typed_readers_decode_identity_and_localized_text` assertions;
  a season may relocalize the strings (re-pin from the live build).
