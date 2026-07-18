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
  offset 0. **Stored row pitch is texture-specific** (observed 64- and
  128-px alignments — e.g. BC1 atlas 447106 at 1208 px wide is stored at
  a **1280**-px pitch, 128-aligned, not the 1216 a `align(W,64)` guess
  gives). The exact pitch = `SerTex[0].size ÷ blockRows ÷ blockSize`
  (blocks-per-row), recovered from the mip0 byte count; a hard-coded
  `align(W,64)` drifts the stride on the 128-aligned atlases and garbles
  the image (slanted banding — #28 / CL-49). Decode at the stored pitch
  then crop to `W×H`.
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
| 104 Affix | `Affix_` | `Name`, `Desc` | `Legendary_Barb_110` (578755) → `Affix_Legendary_Barb_110` (1106289) → Name `of Limitless Rage`; charm `2586362` → `Affix_…` (2586361) → Desc `Your attacks Critically Strike …` (Desc-only, no Name) |
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
| 16 | DT_ENUM | `eNodeType` (0=Normal/structural/gate/rare, 3=Magic, 5=Start) |
| 20 | DT_ENUM | `eRarityOverride` (0=Common, 2=Magic, 3=Rare, 4=Legendary) |
| 24 | DT_SNO (group 29) | `snoPassivePower` |
| 32 | DT_VARIABLEARRAY[AttributeSpecifier] | `ptAttributes` (`dataOffset@+8`, `dataSize@+12`) |
| 48 | DT_VARIABLEARRAY[DT_SNO] | bonus-passive-power slot (`dataOffset@+8`, `dataSize@+12`); size-1 on rares, empty on all other observed kinds — see "Rare bonus mechanic" below |
| 64 | DT_VARIABLEARRAY[DT_SNO] | bonus stat-threshold tag array (`dataOffset@+8`, `dataSize@+12`); references group 124 `StatTag` records — populated only on rares |
| 80 | DT_INT | `bHasSocket` |
| 84 | DT_INT | `bIsGate` |
| 88 | DT_VARIABLEARRAY[DT_UINT] | per-attribute GBID array (`dataOffset@+8`, `dataSize@+12`); one u32 per `ptAttributes` element, same order |

`eNodeType` (offset 16) is a **distinct axis** from `eRarityOverride`: every
class start node is `5` (verified on all seven class boards); magic nodes are
`3`; normal, structural, gate, and (observed) rare nodes are `0`. Use it (not
rarity) to identify the start node.

The **per-attribute GBID array** (offset 88) parallels `ptAttributes`
element-for-element. Each u32 is a stable key for that attribute's
`eAttribute` — e.g. `eAttribute 9` (Strength) → `0x1E663884` everywhere it
appears — but its canonical resource name is not yet recovered (it is not a
DJB2/GBID hash of any tested attribute label); it is surfaced raw.

**Rare bonus mechanic (CL-67).** A rare node's in-game text reads
`+X% [StatA], +Y% [StatB]` followed by `Bonus: another +Z% [StatA] when
N [StatT] met`. The conditional half is wired through two node-level
descriptors that are present only on rares:

- **Offset 48** — `DT_VARIABLEARRAY[DT_SNO]` carrying a single slot
  (`dataSize == 4`). Across every rare sampled the slot value is `0` —
  the canonical purpose is unconfirmed, and the field name has not been
  recovered; the surface
  (`ParagonNodeDefinition.BonusPassivePowerSno`) exposes the raw int
  with `-1` reserved for "descriptor missing" (the non-rare case) so a
  consumer can distinguish "no slot authored" from "slot empty".
- **Offset 64** — `DT_VARIABLEARRAY[DT_SNO]` referencing **group 124
  `StatTag`** records (§7.5). Class-specific rares list **one** tag
  (e.g. `Warlock_Rare_006` → `WillpowerMain2`); class-generic rares
  list **two or three** tags as class-keyed alternatives
  (`Generic_Rare_001` → `[Barb_Strength+Dexterity, DexteritySide2,
  StrengthSide2]`). Surfaced as
  `ParagonNodeDefinition.BonusStatTagSnoIds`. Each tag's
  `ThresholdFormulaText` is the runtime expression the player's stat
  must meet for the bonus to apply.

The bonus's stat-identity and magnitude (the `+Z%` and which stat
receives it) are not modelled in this CL — open follow-up; the rare
`@88` GBID array is also one entry larger than `ptAttributes.Count` on
every rare sampled (2 attrs ⇒ 3 entries), which is the most likely
location for the bonus stat's GBID, but the linkage is not yet
verified.

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

formatHash `0xB460195F` (decimal `353797140`).

| Offset    | Type                              | Field |
|---        |---                                |---|
| 0         | DT_INT                            | `snoId` |
| 16, 20    | DT_VARIABLEARRAY[ptAttr]          | **Op-1 only** — `ptAttributes` descriptor (`dataOffset` payload-relative `@+0`, `dataSize@+4`; element stride 8: `(int AttributeId, uint ParamPlus12)`) |
| 24        | DT_ENUM                           | `eAffectedNodeRarity` — universally `0` on the live build (the "any rarity" sentinel; no live affix authors a rarity gate) |
| 48        | DT_ENUM                           | `eBonusOperation` (`1`=Attribute / `2`=NodeAmplification / `4`=AttributeConversion / `5`=Power) |
| 56, 60    | DT_VARIABLEARRAY[…]               | **Op-5 only** — first VLA descriptor (single 4-byte element of as-yet-uninterpreted purpose; placeholder) |
| 64, 68    | DT_VARIABLEARRAY[ptAttr]          | **Op-2 only** — `ptAttributes` descriptor (same shape as the Op-1 slot) |
| 72        | DT_UINT (GBID)                    | Op-2 main/side marker (`0x169F493F` on `_Main`, `0x16A2B4DF` on `_Side`); `0xFFFFFFFF` on every non-Op-2 |
| 76        | DT_FLOAT                          | `flStartingBonusScalar` (== Maxroll `base`; zero on Op-5) |
| 80        | DT_FLOAT                          | `flAddedBonusScalarPerLevel` (== Maxroll `perLevel`; zero on Op-5) |
| 84        | DT_FLOAT                          | `flDisplayFactor` — per-op engine constant: `100` on Op-1/4, `500` on Op-2, `1` on Op-5 |
| 88        | DT_SNO                            | `snoPower` — group-29 PowerDefinition ref on Op-5; sentinel `-1` on every other op |
| 104, 108  | DT_VARIABLEARRAY[ptAttr]          | **Op-4 only** — `ptAttributes` descriptor (same shape) |
| 120, 124  | DT_VARIABLEARRAY[DT_UINT]         | `Tags` descriptor — raw GBID list; element stride 4 |

The `ptAttributes` descriptor moves between three slots (`+16`, `+64`,
`+104`) because each is a distinct schema field (`Attribute` / `NodeAmplification`
/ `AttributeConversion`) — the decoder switches on
`eBonusOperation` to read the right one (Op-5 has no per-attribute
scaling — its magnitude lives in the linked `snoPower` record). The
`Tags` descriptor is at a fixed `+120/+124` across every op; it
contains the affix's classification anchors — an always-present
`0xD4A1BC54` "ParagonGlyphAffix root" anchor on Op-2, a class-attribute
anchor (`0xD8EA381A` co-occurs with Willpower-keyed affixes,
`0x6D5C0968` with Intelligence, `0x1E663884` with Strength, `0x3044FD97`
with Dexterity), and the per-skill-tag selector (`0x6A1F0A80` Abyss,
`0x945652E5` Archfiend, `0x32ABA6FB` Demonology, `0x911594F4` Vulnerable,
`0x8A342FB1` Bleeding, `0x979521AC` Burning, etc. — uncracked; tracked
in `docs/d4-hash-dictionary.md`).

**Threshold / level-gate `Requirements` are NOT in the `.gaf` bytes** —
the per-class "+40 Willpower" / "+25 Intelligence" gate on Op-2 main
affixes and the "unlocks at Level 50" gate on Op-4 `Mult*_Legendary`
affixes are engine constants (class-coupled / op-coupled). They are
runtime-bound on the same axis as the encrypted controller code
(memory `[[project_engine-controller-code-encrypted]]`); the consumer
hard-codes them per class (Warlock = `+40 W`, etc.) or queries them
out of band. The library boundary stops at what is structurally
encoded.

Shipped public surface (CL-84): `ParagonGlyphAffixDefinition.OperationKind`
(typed enum), `.DisplayFactor`, `.AffectedAttributes` (`IReadOnlyList<GlyphAffixAttributeRef>`),
`.Tags` (`IReadOnlyList<uint>`), `.LinkedPowerSnoId` (`int?`),
`.AffectedRarityKind` (`ParagonRarity?` — null on every live affix).

### 7.5 `StatTagDefinition` (group 124, stat-threshold tag) (CL-67)

Group 124 records are referenced from rare-node bonus arrays (§7.2) and
from glyph activation gates. They name a stat-threshold gate
(`<Stat>{Main|Side}{Tier}` for the simple form,
`<Class>_<StatA>+<StatB>` for class-keyed alternatives, and
`Glyph_<Stat>_{Main|Side}` for glyph-keyed) and carry a **formula text**
whose evaluation yields the stat threshold the player must meet.

| Offset | Type | Field |
|---|---|---|
| 0 | DT_INT | `snoId` |
| 64 | DT_VARIABLEARRAY[DT_CHAR] | formula-text descriptor (`dataOffset@+8` payload-relative; `dataSize@+12` includes the trailing NUL) |
| 80 | DT_VARIABLEARRAY | parallel pre-parsed token stream — not modelled (the text is authoritative; the token stream is the engine's compiled equivalent) |

Worked example — `WillpowerMain2` (SNO `1068426`):

```
@64 descriptor → dataOffset=96, dataSize=37
@96..@131 ASCII     "760 + (455 * ParagonBoardEquipIndex)" + NUL
```

The `ParagonBoardEquipIndex` binding is the slot index of the player's
equipped paragon-board chain (resolved by the consumer at evaluation
time; the live `Fathomless` binding shows EquipIndex = 3 ⇒ threshold
`760 + 455*3 = 2125`, consistent with the displayed
"+X% when 2125 Willpower met").

**Composite-tag variants** (`Barb_Strength+Dexterity` etc.) carry the
primary formula at the descriptor's `dataOffset` plus additional
sub-records for the per-alternative stats. The primary text is what
`StatTagDefinition.ThresholdFormulaText` surfaces today; the additional
structure is open follow-up.

**Glyph-keyed variants** (`Glyph_Willpower_Main` etc.) carry a numeric
constant rather than a formula (`"40"`). The text decode is identical;
the consumer interprets it as a literal.

The library surface stops at the text — evaluation belongs to the
consumer (Appendix C; mirrors the `AttributeFormulaTable` boundary). The
typed reader is `Diablo4Storage.ReadStatTag(int)` /
`TryReadStatTag(int, out StatTagDefinition)`.

### 7.6 Paragon magnitude resolution — budget multipliers + formula evaluator (CL-68, FR-C21)

The displayed magnitude of a paragon **stat node** (the `+X%` /
`+X` value the in-game tooltip shows) is the engine's formula
output for that node's attribute, evaluated with the rarity-specific
budget multiplier substituted in:

```
displayed = formula-constant × ParagonPowerBudgetMultiplierNode<R>{Major|Minor}<Off|Def>()
```

Worked validations (owner in-game readings on build `3.0.2.71886`):

| Node                                       | Formula text                                                              | × multiplier | = magnitude |
|---                                         |---                                                                        |---           |---          |
| `Generic_Magic_Armor`                      | `0.75 * ParagonPowerBudgetMultiplierNodeMagicDefensive()`                 | × 10         | `7.5`       |
| `Generic_Magic_DamageToElite`              | `3 * ParagonPowerBudgetMultiplierNodeMagicOffensive()`                    | × 2.5        | `7.5`       |
| `Generic_Rare_AllResistance`               | `0.75 * ParagonPowerBudgetMultiplierNodeRareMajorDefensive()`             | × 4          | `3.0`       |
| `Generic_Rare_MaxLife`                     | `1 * ParagonPowerBudgetMultiplierNodeRareMajorDefensive()`                | × 4          | `4.0`       |
| `Generic_Rare_Damage`                      | `2 * ParagonPowerBudgetMultiplierNodeRareMajorOffensive()`                | × 5          | `10`        |
| `Warlock_Rare_006` (Demonology tag)        | `3.5 * ParagonPowerBudgetMultiplierNodeRareMajorOffensive()`              | × 5          | `17.5`      |
| `Generic_Rare_CriticalDamage`              | `3 * ParagonPowerBudgetMultiplierNodeRareMinorOffensive()`                | × 5          | `15`        |
| `ParagonNodeCoreStat_Normal` (start node)  | `5`                                                                        | (no factor)  | `5`         |

The six budget multipliers (`ParagonPowerBudget`) are
**clean-room-pinned** from the in-game readings: the engine
implements them as formula-DSL intrinsic functions absent from every
shipped GameBalance data table (no SNO has "Budget" in its name,
they're not in the 1038-entry `AttributeFormulas`, not in
`PowerFormulaTables`). The pinned values are:

```
MagicDefensive          = 10        MagicOffensive        = 2.5
RareMajorDefensive      = 4         RareMajorOffensive    = 5
RareMinorDefensive      = 4         RareMinorOffensive    = 5
```

**Re-verification trigger:** if a future build's displayed magnitudes
disagree, either (a) the per-node formula constant changed (re-read
`NodeAttribute` + `AttributeFormulaTable` for the named formula), or
(b) the engine retuned a multiplier (re-pin from owner readings and
update the table here). The library has no way to read the
multipliers from data — they only exist in the engine code.

**Evaluator surface (`ParagonMagnitudeFormula.Evaluate(string)`).** A
strict subset of the engine's formula DSL: a numeric literal, zero-arg
intrinsic calls, binary `+ - * /`, and parens. Built on the existing
internal `PowerScriptFormulaEvaluator` with a function resolver that
delegates to `ParagonPowerBudget.TryGetMultiplier`; returns NaN when
the expression references an unknown intrinsic (future-build trip
wire). Threshold formulas with a runtime variable
(`ParagonBoardEquipIndex` — `StatTagDefinition.ThresholdFormulaText`,
§7.5) are NOT evaluated here; the consumer supplies that binding to
the bonus-threshold resolution path (a separate FR-C21 surface).

**Boundary:** this is the FR-C21 carve-out from Appendix C — the
library now ships the magnitude evaluator + the calibration table for
the paragon node-info surface. Power-script formulas, glyph
rank/radius scaling, item/affix value resolution, and general
`AttributeFormulaTable` evaluation remain the consumer's.

### 7.7 `ParagonNodeInfo` — the FR-C21 node projection (CL-69)

A display-ready projection of one paragon node, served by
`Catalog.GetNodeInfo(int sno)`. The library does the magnitude
evaluation (§7.6), unit inference, and stat-name resolution so the
consumer renders directly from the projection.

```
ParagonNodeInfo {
  int Sno; string Name;
  ParagonNodeKind Kind;       // Normal/Magic/Rare/Legendary/Start/Socket/Gate
  ParagonRarity   Rarity;     // raw eRarityOverride (distinct axis from Kind)
  AssetRef? Icon, IconMask;   // TextureAtlas refs (null when handle absent/unresolved)
  AssetRef? PassivePower;     // Power SNO ref (null when none)
  string?   PassivePowerName; // sibling-StringList localized name (null when missing)
  ParagonNodeStat[] Stats;    // empty for Start / Socket; Gate carries +5 to
                              // each basic stat (Str/Int/Will/Dex) — CL-74
  bool HasSocket, IsGate;     // raw flags retained for parity
}

ParagonNodeStat {
  int AttributeId;            // raw eAttribute (budget category, NOT the stat key)
  string StatName;            // resolved English name (token humanized; "Armor",
                              //   "Damage to Elite", "Resistance Cold", …;
                              //   "Attribute <id>" fallback for class-specific names)
  int Variant;                // raw NParam (informational; 0 on every observed node)
  string? VariantName;        // reserved (always null today)
  double? FlatValue;          // displayed magnitude (formula evaluated; null when
                              //   the formula references an unknown intrinsic)
  StatUnit Unit;              // Flat | Percent | Multiplier (heuristic; FlatValue
                              //   is the numeric truth)
  AssetRef? Formula;          // named AttributeFormulas entry (null when inline)
  string? InlineFormula;      // node's own formula text (null when GBID-referenced)
}
```

**`Sno` is the canonical aggregation key** for a stat (not
`(AttributeId, Variant)`). Three nodes — `Generic_Magic_Armor`,
`Generic_Magic_ArmorPercent`, `Generic_Magic_DamageReductionFromElite`
— decode to **identical** attribute fields (`AttributeId 481`,
`NParam 0`, same formula GBID, same parallel-array GBID) yet display
three distinct stats; only the SNO/name disambiguates them. The
Optimizer signed off on this correction (`casc-fr#33`, 2026-05-23).

**`StatName` resolution.** For `Generic_<Rarity>_<Token>` node
names the trailing token is humanized via a CamelCase split +
abbreviation expansion: `Generic_Magic_DamageToElite` ⇒
`"Damage to Elite"`, `Generic_Magic_DamageReductionFromVulnerable`
⇒ `"Damage Reduction from Vulnerable"`, `Generic_Magic_Str` ⇒
`"Strength"`, `Generic_Magic_HPFlat` ⇒ `"Max Life (Flat)"`,
`Generic_Magic_HPPercent` ⇒ `"Max Life"`, `Generic_Magic_CDR` ⇒
`"Cooldown Reduction"`, `Generic_Magic_AttackSpeedBasic` ⇒
`"Attack Speed (Basic Skills)"`. Class-specific names without the
`Generic_` prefix (e.g. `Warlock_Rare_006`) carry no encoded stat
token — the projection falls back to `"Attribute <id>"` (the
`NodeAttribute.AttributeId`). Localized labels via
`AttributeDescriptions` (sno `4080`) are deferred to a follow-on
iteration.

**`StatUnit` heuristic.** Token-driven dispatch: `ResistanceMax*`
caps render as `Percent`, all other `Resistance*` render as
`Flat`; the pure-stat tokens (`Str`, `Int`, `Will`, `Dex`),
`HPFlat`, `Thorns`, `Essence`, `MaximumWrath`, `MaximumDominance`,
`HealingBonus`, `BonusFortify` render as `Flat`; every other token
renders as `Percent`. Bare-constant formulas (Normal-rarity nodes
whose magnitude text has no identifier) render as `Flat`. When the
node name carries no `Generic_<Rarity>_<Token>` token, dispatch
falls back to the player-stat / resistance / HP-flat / Thorns
`AttributeId`s. This is a hint — `FlatValue` is the numeric truth.

**Caching.** `Catalog.GetNodeInfo` caches both the decoded
`ParagonNodeDefinition` and the resolved `ParagonNodeInfo` by SNO
for the storage's lifetime (`ConcurrentDictionary`). The optimizer
hot path re-queries the same boards repeatedly, each carrying
~17–21 distinct node defs across ~441 cells. The shared
`AttributeFormulaTable` (sno `201912`, ~1 MB) is decoded once on
first call and held. Missing/undecodable SNOs memoize as `null`
so a malformed repeat-query is just as cheap as a hit.

### 7.8 `Catalog.GetBoardNodes` + `EnumerateNodes` — the FR-C21 hot path (CL-70)

The consumer hot path for the Optimizer's multi-board B&B search and
the display UI:

```csharp
public IReadOnlyList<(ParagonGridCell Cell, ParagonNodeInfo Info)>
    GetBoardNodes(int boardSno);

public IEnumerable<ParagonNodeInfo>
    EnumerateNodes(AssetQuery? query = null);
```

`GetBoardNodes` walks the board's authored 21×21 grid row-major
(`row 0 = top`, `col 0 = left`), skipping empty cells, and pairs each
placed node SNO with its grid cell and the resolved
`ParagonNodeInfo` (§7.7). The board itself is cached
(`ConcurrentDictionary<int, ParagonBoardDefinition?>`), the per-cell
infos are cached by node SNO (the §7.7 cache), and the projected
`(cell, info)` list is cached **per board SNO** so a repeat query
returns the same list instance with O(1) cost — the optimizer's
re-query-the-same-board hot path. Missing / undecodable board SNOs
memoize as an empty list (the search-tree pruning often probes
malformed ids).

`ParagonGridCell` is a `readonly record struct (int Row, int Col)`.
The pair tuple matches the Optimizer's proposed shape (#33 reply
2026-05-22).

`EnumerateNodes` streams every paragon node in the install through
the §7.7 projection — lazy, sharing the SNO-keyed decode cache with
`GetNodeInfo` / `GetBoardNodes`. The query's `Kind` / `Kinds` are
overridden to `ParagonNode`; other facets (`NameContains`, `Where`,
`OrderByName`, …) apply as usual. Malformed nodes are silently
skipped (their CoreTOC name is missing or `ParagonNodeDefinition`
fails to parse).

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

### 8.1 Formula grammar, function contracts + the min/max derivation (CL-95, CL-100)

**Grammar.** The formula DSL — shared by the `AttributeFormulas` GBID curves
(`FormulaText`) and the affix `InlineFormula` (§11.3) — is a small expression
language. Recorded shape (LIB-3 R7; operators in *ascending* precedence):

```
expr            := ternary
ternary         := comparison ( "?" expr ":" expr )?
comparison      := additive ( ( ">" | "<" | ">=" | "<=" | "==" ) additive )?
additive        := multiplicative ( ( "+" | "-" ) multiplicative )*
multiplicative  := unary ( ( "*" | "/" ) unary )*
unary           := "-" unary | primary
primary         := number | varRef | call | "(" expr ")"
call            := ident "(" ( expr ( "," expr )* )? ")"
varRef          := "SF_" digit+ | ident | qualifiedRef        ; see below
qualifiedRef    := "PowerTag." ident "." '"' "Script Formula" digit+ '"'
```

- **Arithmetic** (`+ - * /`, unary `-`, parentheses) is the common case — the
  entire GBID-formula table and the vast majority of inline formulas. The
  library's `ParagonMagnitudeFormula` / `PowerScriptFormulaEvaluator` implement
  exactly this subset (plus the zero-arg intrinsics below); they do **not**
  parse the `?`/`:`/relational productions.
- **Comparison + ternary** (`cond ? a : b` with a relational `>`) appear on a
  small number of unique affixes (**2** on `3.1.1.72836` — e.g.
  `Chest_Unique_Paladin_001`). The semantics are the conventional C-style ones
  (a relational yields a boolean; the ternary selects a branch), but the engine
  operator table is **not** independently oracle-confirmed — so treat the two
  ternary arms as **two candidate ranges**: the printed value is
  runtime-conditional (below), not a single span.

**Variable references.** A `varRef` resolves in one of four namespaces:

- **`SF_N`** — a script-formula slot of the enclosing power (§11.2), positional.
- **Engine intrinsics** — zero-arg calls resolved by the engine, not the data:
  the 6 budget multipliers (§7.2/CL-68), `IPower()`, `GetTotalAffixBonus()`,
  and `CurrentLegendaryRank()` (below).
- **Designer-attribute tokens** — a bare identifier that is *not* an intrinsic
  is a `DataAttributes` (SNO `1907204`) token referenced **by name**:
  `S14_Mythic_UniquePotency` = `DataAttributes[280]`,
  `S14_Mythic_CooldownReductionCDR` = `[279]` (byte-verified). This ties the
  evaluator to the DataAttributes namespace (FR-C27); the tokens are
  seasonal/conditional (Mythic potency, kill-streak, …).
- **Power cross-references** — `PowerTag.<Name>."Script Formula N"` reads
  another power's *N*-th script formula. The referent power is resolvable
  (`CoreToc.TryGetId(SnoGroup.Power, Name)` → `ReadPower`), but on `3.1.1.72836`
  **all 86** such references target one power, `S10ChaosTuningPerClass`
  (SNO `2434194`), whose per-class script-formula values are encoded in the
  binary-AST opcodes the FR-C13 decoder defers (§11.2, devlog 0035 §3) — so the
  reference is **identifiable but not numerically resolved** in this release.

**`CurrentLegendaryRank()` — rank-scaled, deterministic (CL-100; cap corrected
FR-C38 CL-107).** The intrinsic returns the item's legendary/aspect rank, an
integer in **`[1 … MaxRank]`** where **`MaxRank` is per-aspect**, read from the
int32 at the g104 **affix** record's payload **`+0x94`** and surfaced as
`AffixDefinition.MaxRank`. It is **not** a global constant: on `3.1.1.72836` the
661 `legendary_*` aspect affixes cap at **21** (394), **11** (82), **16** (79),
**6** (19), and a spread of others (all present + in a sane 1..200 range). Rank
is **1-based**: the dominant `base + (CurrentLegendaryRank()-1)*k` inline shape
places the base at rank 1 (the `-1` is meaningless for a 0-based rank). So a
rank-scaled affix's printable value is the **span
`[InlineFormula(1) … InlineFormula(MaxRank)]`**, *not* a roll range — owner
Codex-of-Power oracle, 4 aspects, exact: Edgemaster's
(`40+(CurrentLegendaryRank()-1)*1`, `MaxRank` 21) → `[40 … 60]`%; Conceited
(same, 21) → `[40 … 60]`%; Aspect of Coagulation (`15+(rank-1)*1`, 6) →
`[15 … 20]`%; Glynn's Anvil cap (`25+(rank-1)*1`, 16) → `[25 … 40]`%. **630**
g104 affixes (`3.1.1.72836`) are rank-scaled this way.

> **CL-100 correction (FR-C38).** The earlier claim that the cap was a *universal
> `10`* (surfaced as `PowerDefinition.MaxRank` / `.MaxLegendaryRank`, both since
> **removed**) was a misread: the `("10", 10.0)` record on every `legendary_*`
> Power is a fixed **value-descriptor footer** (it follows the
> `Affix_Value_N#… / 100` token — see the Power tail on `legendary_generic_063`),
> universal *because it is not the rank cap*. The consuming check that "confirmed"
> `10` was tautological (it re-read the library's own decode); the owner's in-game
> oracle refuted the semantic. The footer is still stripped from
> `PowerDefinition.ScriptFormulas` (it is not an SF_N); its value is no longer
> surfaced. A multi-value aspect's shown range is not always `[f(1), f(MaxRank)]`
> of one term (Glynn's per-Resolve bracket `[2.5–5.0]` ≠ `f(16)=4.0` — its cap is
> a separate derived quantity), so the span rule holds per value-term, not per
> tooltip line.

The library never evaluates the formula text (boundary, Appendix C), but the
value **range** a UI prints is derivable without an evaluator, from the roll
functions' argument contracts. The functions are engine-defined C++; their
contracts (established by structural cross-validation over the 1038-entry table
— e.g. the `GearAffix_CritChance` per-item-power ladder whose per-band bounds
match its args exactly) are:

| function | contract | roll min / max |
|---|---|---|
| `FloatRandomRangeWithInterval(g, min, max)` | a random value in **`[min, max]`** (args 2, 3) quantized to `g` evenly-spaced steps. `g` (arg 1) is the granularity only — it changes *which* discrete values can roll, **not** the range. | `min` / `max` |
| `FloatRandomRangeWithIntervalUniqueAffixPityBonus(g, min, max)` / `FloatRangeWithIntervalUniqueAffixPityBonus(rand, g, min, max)` | on unique/legendary power inline formulas (§11.3, CL-96). The **declared range is `[min, max]` = args 2, 3** — the same slot grammar as the base `…WithInterval` function, so the displayed min/max is data, not analogy. **`Pity`** is a runtime bad-luck-protection mechanic that biases which in-range value the roll lands on (toward the high end over repeated rolls); its distribution is **engine-coded** — *not* in the SNO — but by construction it selects *within* the declared `[min, max]`, so it does not move the printed bounds. Recorded contract (CL-95/98): print `[arg2, arg3]`; the pity distribution is the engine's. | `min` / `max` |
| `RandomInt(lo, hi)` | a uniform integer, **inclusive** of both bounds. (Confirmed by the `RandomInt(1, 8 + ROUND(…))` forms whose upper bound grows with item power.) | `lo` / `hi` |
| `RandomFloat(lo, hi)`, `SharedRandomFloat(…)`, `FloatRangeWithInterval(g, lo, hi)` | uniform in `[lo, hi]` (the `Shared*` variant correlates rolls across a group; irrelevant to the per-affix range). | `lo` / `hi` |
| `IPower()` | the item's **item power** at evaluation. Not clamped here — the applicable range row is the one whose `nItemPowerRangeStart` is the greatest `≤ IPower()`; the top row covers all higher item powers. | deterministic |
| `ROUND(x)` / `Round(x)` | round to nearest integer. **Tie-breaking (banker's vs away-from-zero) is engine-defined and not recoverable from the SNO data** — but it affects a term by at most 1 and only at an exact half-integer argument (rare given the fractional `(k/denom)*(IPower()-start)` terms). | deterministic |
| `Max(a, b)`, `Pin(x, lo, hi)`, `Pow(a, b)` | larger-of; clamp `x` to `[lo, hi]`; `a` to the power `b`. | deterministic |
| `ParagonPowerBudgetMultiplier*()`, `GetTotalAffixBonus()`, `CurrentLegendaryRank()` | engine intrinsics / cross-references (the 6 calibrated budget multipliers per §7.2/CL-68; total-of-a-stat; the item's legendary/aspect rank). | deterministic |

**Min/max rule.** Evaluate the row's `FormulaText` twice — with **every** roll
function replaced by its low argument for the minimum and its high argument for
the maximum (deterministic functions identical in both) — then **clamp both to
`[RangeValue1, RangeValue2]`**. Worked: `GearAffix_CritChance` @ item power 850
`"FloatRandomRangeWithInterval(3,3.5,5)/100"` → min `3.5/100`, max `5/100` =
**3.5 %–5 %**; `AffixCoreStat1x` @ item power 900 `"(71 + RandomInt(1,15)) - 2"`
→ **70–84**.

**`RangeValue1` / `RangeValue2` are output CLAMPS, not the roll spread** —
verified: across 2580 ranges they collapse to a handful of round,
formula-independent bounds (`(0, 100)` for percentages ×493, `(1, 9999)` for
core stat, `(0, 99999)` for large stats ×547, per-row bounds like
`(0.025, 99.975)` on the inverse-percentage curves). A consumer that mistakes
them for the min/max is wrong; apply them as the final `Clamp`.

The residual uncertainties (`g`'s exact step count, `ROUND` tie-breaking) do
**not** affect the printed range — the range is fully determined by the roll
functions' `[min, max]` args and the clamps.

### 8.2 `LevelScaling` → base Max Life (SNO 206158; FR-C29 Phase 2, CL-99)

`LevelScaling` (GameBalance 206158) is a per-level curve table. Layout
(verified `3.1.1.72836`): a `DT_VARIABLEARRAY` descriptor at payload `+0x50`
(`dataOffset@+0` = 88, `byteSize@+4` = 42400) → **200 rows × 212 bytes**;
**row index = level − 1**; the `float` **`hpScalar` is at row column `+4`**
(`1.0` at level 1). Rows are indexed `1..200`: **characters occupy `1..70`**
(the cap, `heroDetails` id 279), `71..200` extend the curve into content levels.
**Reconciliation (FR-C34, CL-101):** an earlier note that this one `hpScalar`
column *also* governs monster HP is **superseded** — monsters scale their HP off
the separate, far steeper `DifficultyTiers` curve (§8.3): **×101,051 at L70** vs
`hpScalar`'s ×30.5, a ~3,300× different curve. `hpScalar` is the *player*
base-Life curve; what consumes rows `71..200` is not modeled here, but it is
**not** the monster-HP source.

**Base Max Life is class-independent** (every class reads identically):
`round(50 × hpScalar[level])`, **round-half-away-from-zero**. The base
`Hitpoints_Max = 50` is a class-independent constant (cross-validated 15/15
against owner oracles, incl. out-of-sample L11–L14; *fitted, not yet located*
as a readable field — baked per the engine-constants pattern). Anchors:
`round(50 × 1.03) = 52` at L2 (the sole exact-`.5` case, `51.5 → 52`, which
pins the rounding mode), `round(50 × 17.200) = 860` at L60,
`round(50 × 30.526) = 1526` at L70. **Key discipline lesson (this FR): a
render-time value can exist nowhere in the data — `1526` is not stored; the
operands (`50` and the scalar) are. Search for the operands, not the result.**
Surface: `Diablo4Storage.ReadLevelScaling()` → `LevelScalingTable`
(`HpScalar(level)`, `BaseLife(level)`, `BaseHitpointsMax`, `MaxCharacterLevel`).
**Remaining columns (CL-102) — exposed raw, not named.** The 212-byte row has
~10 non-zero columns; `LevelScalingRow.Columns` now exposes all 53 (indexed by
`+4·c`) so nothing is hidden, but **only `+4` `hpScalar` is a labeled, verified
column**. The Maxroll dump names some of the others (`monsterDr` / `powerBase` /
`powerDelta` / `powerItem` / `xpScalar`), but — unlike `DifficultyTiers`'s XP
column (§8.3), which has an *independent* anchor — **none can be verified from
the blob** (no anchor, no in-game readout to oracle against), so the library does
**not** assert those names. Observed per-level behavior (`3.1.1.72836`; col at
L1/L70/L200): `+8` grows `0.85 → 7.84 → 10.17`; `+32` *decreases*
`1.2 → 0.147 → 0.5`; `+36` decreases `1.0 → 0.033 → 0.5`; `+20`/`+24`/`+28` are
constants (`0.002` / `0.015` / `0.5`); `+40` is `0.8` then `2.25` at L200; col
`+0` reads `0` (level is implied by row order, not stored). Confidently mapping
the Maxroll names to these offsets needs either the d4data GameBalance
column-order schema (community intel to verify per
[[feedback_third-party-re-as-intel]]) or one owner in-game oracle per column
(e.g. the item power at a known level would pin `powerItem` for `IPower()`).

### 8.3 `DifficultyTiers` → per-monster-level curve (SNO 1973217; FR-C34, CL-101)

`DifficultyTiers` (GameBalance 1973217) is the **monster/content analogue** of
§8.2 — the per-**monster-level** scaling curve. Layout (verified `3.1.1.72836`):
a `DT_VARIABLEARRAY` descriptor at payload `+0x50` (`dataOffset@+0` = 88,
`byteSize@+4` = 19200) → **150 rows × 128 bytes**; **row index = level − 1**
(monster levels `1..150`). Columns (byte offset within the row):

| col | reading | L1 / L40 / L70 | confidence |
|---|---|---|---|
| `+0` (`int32`) | **level** | 1 / 40 / 70 | verified (row-index identity) |
| `+4` (`float`) | **monster HP multiplier** | 1.0 / 909.8 / 101,051 | **inferred** (devlog 0084; no oracle) |
| `+8` (`float`) | **monster damage multiplier** | 1.0 / 16.06 / 64.44 | **inferred** (no oracle) |
| `+36` (`float`) | **per-level XP value** | 0 / **8.0** / **11.0** | **verified** (the anchor) |
| `+40` (`float`) | per-level gold value | 0 / 2.0 / 2.75 | candidate |

**The anchor locks the layout.** `+36` reproduces the game's per-level XP curve
exactly (L40 = 8.0, L70 = 11.0; `+0.1`/level from ~L40) — an *independent* check
that fixes the stride/offset, so every column is correctly *located*. But
`+4`/`+8` carry **inferred** semantics: both are per-level multipliers (×1.0 at
L1), yet the "monster HP / damage" meaning **cannot be owner-validated** — D4
shows monster health as a bar only, no numeric readout (honest-label discipline,
[[feedback_calibrate-claims-to-evidence]] / FR-C31). The remaining ~26 columns
are a mix of small `int` flags and `float` reward/scaling coefficients, exposed
**unlabeled** on `DifficultyTierRow.Columns`. Surface:
`Diablo4Storage.ReadDifficultyTiers()` → `DifficultyTiersTable`
(`Row(level)` / `MonsterHpScalar` / `MonsterDamageScalar` / `PerLevelXpValue` /
`PerLevelGoldValue` / `LevelCount`).

**Monster tables landscape (FR-C34 recon; how far RE reaches).** Monsters are
**Actor SNOs (group 1, ~61k)**, named `<family>_<role>_<element>_<context>`
(`Goatman_sorcerer_phys`, `BSK_Goatman_sorcerer_cold`, `…_unique_DGN_…`). The
`.acr` record is **identity + appearance/anim references** (base appearance actor
in group 9; anim tree in group 67) — **no base-HP field**. Other monster
GameBalance (group 20) tables: `MonsterLevelCurves` (1610053 — 6 named
`Raid_Tier_0..5` scaling curves for raid content), `MonsterNames` (44325 — a
385 KB monster name-affix registry of prefixes/suffixes:
`BloodSeekerBarbPrefix…`, `X1_Raid_Special_Add_Suffix`), `MonsterAffixCategories`
(2440465), `MonsterTags` (1441616). **The per-monster BASE HP is not a flat
readable field** — like the player-side `Hitpoints_Max = 50` (§8.2, fitted not
located), monster HP is engine-assembled from base attributes × this
`DifficultyTiers` curve × difficulty/raid-tier. That is the engine boundary; the
per-level *scaling* is now typed, the per-monster *base* is not in the data as a
number.

### 8.4 `MonsterNames` registry + `MonsterLevelCurves` (FR-C35/C36, CL-105)

**`MonsterNames` (FR-C35).** The localized name-affix fragments the game composes
into elite/special monster display names. The GameBalance registry (SNO `44325`)
holds the tokens; the localized text lives in the **`MonsterNames` StringList**
(group 42, name-matched), `1,277` labels: token → fragment
(`FrozenSuffix004` → "Frostburn", `ElectricLanceSuffix001` → "Boltrend"). A
consumer composes a full elite name from a base monster name + a prefix and/or
suffix fragment — the same "fragments the game composes" pattern as affix/aspect
display names (§11.3 / FR-C30). Surface: `Diablo4Storage.ReadMonsterNames(locale)`
→ `MonsterNameRegistry` (`Fragments` [`MonsterNameFragment{Token, Text, Kind}`],
`Prefixes`, `Suffixes`). **Prefix/suffix is inferred from the token spelling**
(honest — the exact composition rule is engine-side); token + text are
byte-verified.

**`MonsterLevelCurves` (FR-C36, CL-110) — six per-raid-tier scaling curves.**
⚠️ **Corrects a wrong earlier finding** ("a name registry, NOT a curve / not in
the data"). The curves *are* here — the earlier read stopped at the tier records'
placeholder `1.0` floats and never followed each record's curve descriptor. The
table (SNO `1610053`, group 20) is: a VLA @ payload `+0x50` → **6 × 320-byte tier
records** (`Raid_Tier_0..5`, named inline), and **each record carries a
`DT_VARIABLEARRAY` at record offset `+312`** → its curve rows in the record tail.
Each curve row is **12 bytes** = two `int32` (equal in the live data — the level)
+ one `float32` (the scaled effective value, climbing to `100` across the tier's
level span). Tier 0 spans levels 55→95 (11 rows); higher tiers start higher (Tier
1 at 65, … Tier 5 at 105) with fewer rows. Surface: `MonsterLevelCurvesTable`
(`Tiers` → `MonsterLevelCurve` → `MonsterLevelCurvePoint{Level,LevelHigh,ScaledValue}`)
via `Diablo4Storage.ReadMonsterLevelCurves()`. The exact remap semantic (effective
level vs. multiplier) is a structural inference — named descriptively, raw values
exposed. This is *separate* from `DifficultyTiers` (§8.3, the per-monster-level
HP/XP curve): `MonsterLevelCurves` is the per-**raid-tier** level remap.

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
- **Instance records** — each schema field has exactly one value
  record, **positionally keyed** to the schema order (the Nth record is
  the Nth schema field's value), in **one of two interchangeable
  encodings** (FR-C16 R7 — both proven against scene 657304):
  - the fixed **56-byte (`0x38`) `0x22` record**: `+0x00 u32 = 0x22`
    (tag), `+0x04 u32` sub-tag (`0`/`3` observed), **`+0x08 u32` = the
    bound value**, `+0x0C..0x38` zero pad; and
  - the **12-byte tag-2 block**: `+0x00 u32 = 2`, `+0x04 u32 = 0`,
    **`+0x08 u32` = the bound value`**.

  Different widgets use different encodings for the *same* fields, and
  some mix them: `Node_IconBase` is all-`0x22`;
  `Template_Board_Background_Center` is all-tag-2 (its `nWidth`/`nHeight`
  = `1200` live only in tag-2 blocks); `Node_Icon` interleaves both (a
  symmetric `28`-inset). A parser that reads only `0x22` records
  **under-decodes** the tag-2 widgets — the chrome centre's `1200×1200`
  read as all-zero, and a mixed widget's positional keying collapsed
  (Appendix A CL-47). The field-value run is the first *fieldCount*
  records; any trailing `0x58` layer-blocks (§10.12) come after it.

  `DT_INT`/`DT_SNO`/`DT_RGBACOLOR`/`DT_BYTE` values all read from the
  `+0x08` slot of whichever record encodes the field. **Parent widgets**
  whose span nests anonymous, name-less child sub-records (a class id +
  `0xFFFFFFFF` sentinel at `+0x08` — the rarity sub-templates' per-state
  disc layers) confine their own field scan to the run **before the
  first child marker**, so child fields never bleed into the parent.

  Each **child sub-record is itself a complete widget** minus the inline
  name: a class id + `0xFFFFFFFF` sentinel, then its own schema run +
  positionally-keyed value records, bounded by the next sibling marker (or
  the parent's end). The parser decodes them recursively-shallow (one
  level — no grandchildren observed) into `UiWidget.Children`, so each
  layer's `hImageFrame` stays paired with its `nLeft/nTop/nRight/nBottom/
  nWidth/nHeight` insets (Appendix A CL-50). This is load-bearing for the
  node-render recipe: a `Template_Node_<rarity>` *parent* binds no usable
  rect of its own (the all-zero parent rect FR-C18 #29 reported is
  faithful), while its disc children carry the authored inset — Magic/Rare
  disc at inset `7` (= `Node_IconBase`'s 86² base-disc slot), Legendary at
  `−3` overscan, the start/gate filigree (`0xA0F996FE`) at `−18`/`−20`
  overscan in a `140²` box. The flat `ExtraLayerValues` view is retained
  for the §10.14 losslessness gate but is lossy for this pairing.

So a widget's `nWidth` = the `+0x08` of the `0x22` **or** tag-2 record at
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
| `NodeAvailableGlow` | `0x4A901508` | selectable/available glow (state-driven, any rarity — distinct from the per-rarity static ornate, which is each `Template_Node_*` widget's own bound layer) |
| `GlyphNodeGlow_Revealed` / `_Purchased`, `Usage_Slot_2` | `0xBED4CF21` | socket pulse ring |
| `Node_SearchResultHighlight` | `0x49FDA722` | search-result decoration (spiked ring; user-typed glyph-search match, **not** the selected-state ring) |
| `Node_Located` | `0x87A89F86` | grey rim ring |
| `Arrow_{Top,Right,Bottom,Left}` | `0xD51CAB25` / `0x6D3CB8DE` / `0x8EEAC178` / `0xB6D8C741` | directional pointer art (T/R/B/L), each with authored rect |
| `Connector_{Top,Right,Bottom,Left}` | `0x77ECA3A8` / `0x288DE11F` | connector bar art (T/B share, R/L share), each with authored rect |

**The selected-state red ring is part of each rarity's selected
composite, not a separate scene-widget overlay.** Every rarity's
selected state binds a full-disc composite frame whose disc art
carries the red perimeter ring sitting in the inter-ridge channel of
the base disc: Magic-selected `0x72C29402`, Rare-selected
`0x03EDABAB`, and Legendary-selected `0xBD27FB7C` are bound on
`Template_Node_{Magic,Rare,Legendary}`'s 0x58 block (§10.12);
Common-selected `0xD3051CCA` is bound on the separate
`Node_Purchased` widget (the "allocated/spent" indicator). All four
are scene-authored art — no standalone overlay frame is composited
on top. Accordingly the `overlay.selectionRing` state row carries
empty `Layers` with `Unresolved = true` (§10.14 per-record gate);
the recipe in §10.15 records the per-rarity composite handles.

The §10.12 / §10.13 / §10.14 / §10.15 sections cover the additional
binding shapes (0x58 block, dropped-tail values, the exhaustive
whole-scene model, and the per-rarity node composite recipe).
Per-rarity differentiation is **authored as art** in each rarity's
`Template_Node_*` 0x58 block — an interior-fill frame (per-rarity
colour) for Magic/Rare/Legendary, plus an ornate outer frame for
Rare/Legendary. There is no shader tint on a shared neutral disc;
`StateElements.Tint` stays `null` and the colour comes from the
authored fills (§10.15). The §7.2 21-row state matrix maps to that
small set of authored elements (grey base disc + per-rarity fill +
per-rarity ornate where applicable + socket composite + the six
overlay rows) — not 21 distinct baked layer-lists.

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

`overlay.pointerTriangle.Layers` / `overlay.connectorBar.Layers` carry
these (handle + decoded `Rect`), T/R/B/L. `overlay.selectionRing.Layers`
is empty with `Unresolved = true` — the selected-state red ring is
baked into each per-rarity selected composite (§10.11 / §10.15), not a
separate overlay.

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

**Select/deselect brightness/colour (FR-C8 R7): not authored —
engine-driven.** Likewise the dim-unselected / bright-selected look:
`rgbaTint` (`0x09A3F17B`, `DT_RGBACOLOR 0x8E266332`) is declared/bound
only on non-node widgets (glyph grid, `CoreStatActive`, …), never on
the node-state widgets (`Common_Node_Revealed` / `Node_Purchasable` /
`Node_Purchased` / `Node_IconBase` / `Node_Located`); no
`rgbaTintSelected`/`rgbaTintLit`/`flBrightness` field exists. The only
authored per-widget brightness is `dwAlpha` (`0x0C2AFA21`, `DT_BYTE`,
surfaced as `NodeElement.Alpha`). So selection state is a **widget
swap** (which layers compose per state — delivered in `States`) under
a **fixed engine shader pass** (§2.3 / §10.7) for the colour/brightness
delta — consumer-owned, the same pass applied to the
"atlas-darker-than-in-game" frames. `StateElements.Tint`/`LitTint =
null` is the decoded answer, not a gap. Reopen only with an in-game
oracle showing a node *recolouring* (not just swapping the glow layer)
on select.

**R9 — the selectable glow, and an FR-C7 per-rarity-ornate
correction (CL-25).** FR-C7's `Project()` used
`Elem("NodeAvailableGlow")` (`0x4A901508`) as the r3/r4 "gold ornate"
— the *same projection gap* CL-23 fixed for start/gate: it never read
`Template_Node_Rare`/`_Legendary`'s **own** `0x58`-bound layer. The
data is decisive: `NodeAvailableGlow` (widget [105], ClassId
`0x145F2056`) binds `0x4A901508` (unique in the scene) with an
authored rect — and per the owner oracle it is the **selectable/
available glow** (the yellow pulsing perimeter outline on every
*unselected* node that is cardinally adjacent to a selected one, **any
rarity**), *not* a per-rarity decoration. The genuine per-rarity
static ornate is `Template_Node_Rare`'s own `0x58` block (handle
**`0xB71BD068`**) and `Template_Node_Legendary`'s own block
(catalog-validated `LayersOf`). So: r3/r4 now carry `disc` + their
template's own decode-true ornate (`0x4A901508` removed from the baked
rows), and `NodeAvailableGlow` is surfaced as a new
`overlay.availableGlow` State (handle + decoded Rect, one perimeter
frame). **Cross-check answer:** `0x4A901508` is **not** a distinct
rare ornate — it *was* `NodeAvailableGlow` mis-labelled; it is now its
own selectable-state overlay, distinct from the true rare ornate
`0xB71BD068`. The §7.2 matrix is **21 rows** (the 18 + the
pre-publish-amended `overlay.availableGlow` (CL-25) +
`overlay.locatedHighlight` + `overlay.equipGlow` (CL-34); FR-C8/C12
is unreleased so the contract is amendable). Verified by
`ReadParagonRenderLayout_decodes_proven_structure` (rare ⊇
`0xB71BD068`, ∌ `0x4A901508`; `overlay.availableGlow` ⊇ `0x4A901508`).

### 10.14 Exhaustive render-model + the lossless-decode guarantee (FR-C9)

FR-C8 took nine rounds because each was the same shape: a binding
`Project()` *silently dropped*, found only as a visual defect. FR-C9
makes completeness structural.

**The two binding-record value shapes (the published schema).** A
widget's bound values are at the `+0x08` slot of exactly one of:

| Shape | Marker | Value |
|---|---|---|
| 56-byte `0x22` record | `byte/u32 @+0 == 0x22` | `u32 @+0x08` (positionally keyed to the schema) |
| bound-layer block | `u32 @+0 == 2`, `u32 @+0x04 == 0` | `u32 @+0x08` |

The FR-C8/CL-23 block model over-fit two examples (it required
`ownerClassId @+0x20` and `0xFFFFFFFF @+0x28`); those words are **not
universal** — other blocks carry a pointer or zeros there, and a
widget's *last* block straddles the next `nameStart` so its tail is
unreadable. The only stable, self-validating marker is
`tag==2, +4==0, value@+8` (the same lesson CL-24 taught for the `0x22`
tail). `UiScene.Parse` now captures **every** such value (both shapes,
bounded on the value field, never the full record) — so raw
`ReadUiScene` is **lossless** for texture bindings.

**Structural definition + guarantee.** A *texture-binding* is, shape-
agnostically, any handle-magnitude `u32` (≥ `0x10000`; D4 handles are
32-bit hashes — smaller atlas-resolving values are field ints/enums,
never bindings) that resolves via the icon catalog
(`Diablo4Storage.IsParagonTextureHandle`). The library **guarantees**:
for `ParagonBoard` 657304 and `ParagonBoardSelect` 964599, every
texture-binding present anywhere in the raw scene is surfaced by
`ReadParagonRenderModel()` — and this is **enforced by casc's own
acceptance suite** (`ParagonRenderModel_covers_every_bound_atlas_
handle`): a future projection/parse gap fails the library's CI, not
the consumer's eyeballs. The canonical FR-C7-era miss (grey rim ring
`0x87A89F86`, "not in data") is now present with its rect.

**Surface.** `Diablo4Storage.ReadParagonRenderModel()` →
`ParagonRenderModel(Layout, Scenes)`: `Layout` is the role-assigned
FR-C7/C8 projection; `Scenes` (657304, 964599) lists every binding
widget with `{Name, ClassId, Layers[{handle, rect, alpha}]}` — the
one-shot exhaustive audit surface. The library owns *complete faithful
decode + the gates*; role/state classification stays the consumer's
(FR-C7 §6).

**Two complementary structural gates.** Completeness is enforced at
two independent shapes, asserted by casc's own acceptance suite:

1. **Handle-level coverage** —
   `ParagonRenderModel_covers_every_bound_atlas_handle`: every
   handle-magnitude atlas-resolvable `u32` anywhere in the raw scenes
   must appear in the exhaustive model. Catches a new *binding shape*
   that would orphan a handle entirely.
2. **Per-binding-record coverage** —
   `ParagonRenderLayout_every_enumerated_state_has_layers`: every
   enumerated state in `ReadParagonRenderLayout().States` carries at
   least one bound layer **or** is explicitly marked
   `StateElements.Unresolved = true`. `Unresolved` is the structural
   exception for rows the schema enumerates but no scene widget binds
   — typically because the art is composited inside another row's
   bindings (e.g. `overlay.selectionRing`'s red ring lives in each
   per-rarity selected composite — §10.11 / §10.15). Catches a
   *record-level* drop: a state row the projection enumerates and
   leaves empty without acknowledging it as `Unresolved`, which the
   handle gate cannot see if the dropped binding's handle is also
   bound elsewhere.

3. **Per-rarity layer scene-bindedness** —
   `ParagonRenderLayout_per_rarity_layers_are_scene_bound`: every
   layer in a per-rarity (rarity 0/2/3/4) `States` row's handle must
   appear in scene 657304's per-widget bindings (the exhaustive
   `Scenes` view). Per-rarity composites are authored scene art, never
   fabricated from the catalog; this catches a recipe layer that
   references an atlas frame no scene widget binds.

Both gates are shape-agnostic; together they make the FR-C8
nine-round "discovered as a visual defect months later" pattern fail
casc CI, not consumer eyeballs.

### 10.15 Paragon node composite recipe (FR-C10)

The per-rarity node in `ParagonBoard` is not a single disc with a
shader tint — it is an ordered atlas-frame composite the engine
assembles from per-rarity authored art: a shared grey metal base
disc with two raised concentric ridges (`0x1D166DC7`, bound on
`Node_IconBase`), then a per-rarity **interior fill** that sits inset
in the recessed centre (at the fill frame's native pixel size,
centred on the disc anchor — there is no authored sub-rect), then for
Rare/Legendary an **ornate outer frame** that extends to/beyond the
disc edge, then on the selected state a swap to that rarity's
**selected composite** whose disc art carries the red perimeter
ring sitting in the inter-ridge channel — for Magic/Rare/Legendary
this is a 0x58-block binding on `Template_Node_<rarity>`; for Common
it is `Node_Purchased`'s binding (the "allocated/spent" indicator).
Every layer is scene-authored; no standalone overlay frame is
composited on top. Per-rarity colour comes from each rarity's
authored interior-fill frame, not a shader tint on the shared disc
(`StateElements.Tint` stays `null`).

The composite is surfaced as the `Layers` of each per-rarity
`(RarityOverride, State)` row in `ReadParagonRenderLayout().States`.
Each `NodeElement` carries `{TextureHandle, AtlasSno, NativeWidth,
NativeHeight}` so the consumer composites at the engine's
authoritative native scale without a second catalog walk.

| Rarity | Unselected layers (back → front) | Selected layers |
|---|---|---|
| 0 Common | `0x1D166DC7` (grey base disc, 154²) | `0x1D166DC7`, `0xD3051CCA` (`Node_Purchased`, 153² dark disc + perimeter ring composite) |
| 2 Magic | `0x1D166DC7`, `0x621CB6FF` (magic base composite, 153²), `0xFEC31E48` (blue interior fill, 135²) | `0x1D166DC7`, `0x621CB6FF`, `0xFEC31E48`, `0x72C29402` (`Template_Node_Magic` selected, 154² blue disc + perimeter ring composite) |
| 3 Rare | `0x1D166DC7`, `0xF8373491` (interior fill, 135²), `0xB71BD068` (yellow ornate frame, 154²) | `0x1D166DC7`, `0xF8373491`, `0x03EDABAB` (`Template_Node_Rare` selected, 153² yellow ornate + perimeter ring composite) |
| 4 Legendary | `0x1D166DC7`, `0x006ED182` (interior fill, 136²), `0x232DF7F9` (orange spike ornate, 189²), `0xCC3E3B25` (class-specific 135² in `2DUI_ParagonNodesIcons_Rogue`) | `0x1D166DC7`, `0x006ED182`, `0xBD27FB7C` (`Template_Node_Legendary` selected, 189² orange ornate + perimeter ring composite), `0xCC3E3B25` |

For Magic/Rare/Legendary the selected-state composite **replaces**
the unselected variant in the trailing layer slot — the perimeter
red ring is baked into that frame's disc art, so no separate ring
layer is needed. Common's selected composite (`0xD3051CCA`, bound on
the separate `Node_Purchased` widget) is layered on top of the base
disc with the same effect.

**Positioning.** The base disc draws at its authored
`Node_IconBase` rect (fills the `NodeTemplate` 100-ref box minus
authored insets). The interior fill draws at its atlas frame's
native pixel size, centred on the disc. The ornate frame
(Rare/Legendary) draws centred on the disc at the ornate frame's
native pixel size (Rare 154² matches the disc; Legendary 189²
extends ~17% beyond). The selected-state composite draws at its
own native size centred on the disc — the perimeter ring's
placement is part of that frame's geometry, not an authored
sub-rect.

**Acceptance.**
`ReadParagonRenderLayout_decodes_node_composite_recipe` asserts the
per-rarity layer counts, handles, and swap-on-select for every
rarity, and that every emitted layer carries a non-zero `AtlasSno`
and native size.
`ParagonRenderLayout_per_rarity_layers_are_scene_bound` cross-
references every per-rarity layer's handle against the exhaustive
scene-bindings view (§10.14) — per-rarity composites must be
authored scene art, never fabricated. Pre-existing CL-26 / CL-27
gates remain green.

### 10.16 Paragon board chrome (FR-C11)

The paragon board's chrome — the dark textured background field drawn
behind the node grid in `ParagonBoard` (657304), the 4-cardinal-side
rim that wraps it, the preview-frame backing + filigree band of
`ParagonBoardSelect` (964599) — is surfaced as
`ParagonRenderModel.BoardChrome` (`ParagonBoardChrome`). Board chrome
was previously consumer-owned ("not reproduced") under FR-C7 §6 and
is now part of CASC's decode (owner ruling, 2026-05-19); the per-node
art boundary (§10.15) is unchanged.

**Main-board chrome (scene 657304): 5-piece composite.**

| Field | Widget | Handle | Catalog | Native px |
|---|---|---|---|---|
| `BackgroundCenter` | `Template_Board_Background_Center` | `0x2954DF0C` | SNO 447106 (`2DUI_Paragon`) | 1200 × 1200 |
| `BorderTop` | `Template_Board_Background_Top` | `0x900C7D87` | not in icon catalog | — |
| `BorderBottom` | `Template_Board_Background_Bottom` | `0x900C7D87` | not in icon catalog | — |
| `BorderLeft` | `Template_Board_Background_Left` | `0x225F2DA8` | not in icon catalog | — |
| `BorderRight` | `Template_Board_Background_Right` | `0x225F2DA8` | not in icon catalog | — |

The centre is one widget; the rim is four scene-authored cardinal
sides — Top and Bottom share `0x900C7D87`, Left and Right share
`0x225F2DA8`. **There are no corner widgets**: the scene authors only
side bands, not a 9-slice. The two rim handles are scene-bound via
the standard `0x6B1C5D9C` texture-handle field but resolve through a
non-icon-catalog texture path CASC does not currently index — their
`NodeElement.AtlasSno` / `NativeWidth` / `NativeHeight` come back
`0`. The consumer either provides a non-icon-catalog resolution path
or a procedural equivalent for the rim bands.

**Board-select chrome (scene 964599).**

| Field index | Widget | Handle | Atlas SNO | Native px |
|---|---|---|---|---|
| `BoardSelectChrome[0]`, `[1]` | `Board_BG` 0x58-block | `0xDE8B9881`, `0x368C511E` | 2061536 (`2DUI_Paragon_transparentElements`), 1208406 (`2DUI_ParagonNodes`) | 275 × 278, 135 × 135 |
| `BoardSelectChrome[2]` | `Board_Icon_Filigrees` | `0x71C3ECC9` | 838456 | 1458 × 334 |

All catalog-resolvable.

**All chrome widgets author rect-zero (`Rect = default`) in the
scene**: the engine fills the parent canvas at native pixel size —
the "21×21 board" coordinate space the consumer composites against
is engine-internal positioning, not scene-authored sub-rects. The
consumer scales native px to the runtime board-rect at its chosen
zoom.

**Rim animation (the "fire border"): engine-internal (CL-28 / CL-30 /
CL-32 discipline).** Scene data authors **only the 4-side rim
geometry above** for the main board's chrome. There is no blend-mode
field, no frame-order list, no animation-timing parameter on any of
the rim widgets — the rim's animated appearance is produced by the
engine's renderer on top of the scene-bound side bands, not by a
scene-authored frame sequence. The FR-C11 R1 "ember candidate"
handles (`0x6CFA1668`, `0x749F8139`, `0xAA7571AB`) are atlas-only
with no scene binding; `0xB5C007F8` is bound to
`Template_GlyphAura_Tile` (a glyph aura tile, not a board overlay);
`0xC1473C21` is bound to `Common_Node_BG_Black`/`_Revealed`
(per-node background art). The typed model does not surface those
candidates as rim layers — fabrication would repeat the CL-29-class
mis-attribution the scene-bind gates exist to prevent. The consumer
either renders the static side bands once they have a texture for
the rim handles (a separate texture-resolution subsystem CASC does
not yet provide), runs a procedural rim shader on top of those
bands, or falls back to a procedural rim entirely.
`UI_Paragon_FrameGlow` (SNO 1364280, single-frame texture with
sentinel handle `0x00000000`) and the `ui_paragon_glowLine` /
`_glowLineThin` SNOs (1302551 / 1302489, 2048 × 64) are also
engine-referenced and not scene-bound to any board-chrome widget.

**Acceptance.**
`ReadParagonBoardChrome_surfaces_scene_bound_chrome` asserts the
5-piece composite (centre handle / atlas / native size; rim
handle equality Top=Bottom, Left=Right; rim `AtlasSno`/native px
zero), the board-select chrome layer shape, and that no
fire-border catalog handle leaks into the typed model.
`ReadParagonBoardChrome_layers_are_scene_bound` (gate, parity with
CL-26 / CL-27 / CL-28 / CL-30) cross-references the centre and the
board-select layers against `Scenes` per-widget bindings, and the
4 rim handles against the raw scene-657304 widget data (their
target is non-icon-catalog so `Scenes` filters them out).

### 10.17 Per-node-cell background + special-node addendum (FR-C11 R3 / FR-C12)

**`Common_Node_Revealed` binding (FR-C11 R3 §2 / FR-C15 R2).** A
scene-bound layer on the `Common_Node_Revealed` widget — handle
`0xC1473C21` (catalog-resolvable in 2DUI atlas SNO 447106) via the
standard `0x6B1C5D9C` texture-handle field; authored rect
`L=R=T=B=3` inside the 100-pitch `NodeTemplate` (94×94 footprint
centred in the 100×100 cell, ~6-ref-unit inter-cell gap); widget
records `dwAlpha = 0xFF` so the atlas frame's own alpha drives the
composite. The sibling `Common_Node_BG_Black` widget binds the
same handle at the same rect — likely the hidden-state variant per
the widget name. Surfaced as
`ParagonRenderLayout.CommonNodeRevealedLayer` (single `NodeElement`).
The field is **binding-named, not role-named** — see the role
retraction below.

**FR-C15 R2 / CL-39 — role retraction.** CL-33 (FR-C11 R3 §2)
originally proposed this binding as the "per-node cell background
tile" — the persistent darker rounded square the lighter board
field shows through between cells (owner game-vs-app oracle). The
consumer plumbed
`ParagonRenderLayout.NodeCellBackground` (the CL-33 field name)
end-to-end and visually inspected the resolved `0xC1473C21` atlas
frame: the texture is a **horizontal ember-strip / cell-reveal
glow pattern**, NOT a clean rounded dark square. The binding
traversal is correct (the widget DOES bind this handle through the
standard texture-handle field), but the proposed VISUAL ROLE
("per-node cell background tile") is empirically wrong. The actual
role is more likely a **transient cell-reveal effect** (consistent
with the widget name `_Revealed` — engine animation when a cell
becomes visible), not the persistent per-node tile owner sees in
the steady-state board. The typed field was renamed from
`NodeCellBackground` → `CommonNodeRevealedLayer` (binding-derived
name, no role assertion) to comply with the lesson learned: a
widget's name is not authoritative evidence of its visual role.
The persistent per-node cell tile owner sees in-game remains
**unidentified** in CASC's current decode — possibly bound on a
different widget the FR-C12 R2 broad probe didn't flag, possibly
bound via a non-`0x6B1C5D9C` DT-type, possibly engine-procedural
(parallel to CL-31 §3 rim-fire). See FR-C15 R2 follow-up.

**`NodeAvailableGlow` authored extent (FR-C11 R3 §3).** The
selectable-glow widget's authored rect is genuinely all-zero — the
widget inherits `NodeTemplate`'s 100-pitch parent box, *but* the
bound atlas frame (handle `0x4A901508`, atlas SNO 2061536) is **325 ×
326 px** — over 3× the cell pitch. The engine renders the frame at
native pixel size centred on the cell, so the yellow glow nearly
touches adjacent cells in-game. The consumer should compose at
`NodeElement.NativeWidth` / `NativeHeight` (the CL-29 fields, already
populated), not at the cell rect — drawing at 1 cell under-draws the
glow.

**Special-node composite recipes (FR-C12 §3).** Recorded for parity
with the §10.15 per-rarity table. Surfaced via `States` rows with
`RarityOverride = -1`.

| State | Layers (back → front) | Source |
|---|---|---|
| `socket.unselected` / `socket.selected` / `socket.socketed` | ornate outer disk `0xF6443089` (135²) + red bead ring `0xBED4CF21` (135²) + inner spike-frame `0x23F487F3` (136²); `.socketed` adds the per-node glyph image (`HIconMask` from `ParagonNodeDefinition`) seated in the inner spike-frame's center depression | scene-bound — outer disk + inner well on `Usage_Slot_2`'s 0x58-block; bead ring on `GlyphNodeGlow_Revealed`'s texture-handle field for `.unselected`/`.selected`, on `GlyphNodeGlow_Purchased` for `.socketed` (the engine reuses the same atlas frames for both the side-panel and the on-board per-node render). The socket-class node has its OWN ornate outer disk and does NOT composite the shared per-rarity grey-base `0x1D166DC7` — the engine's state dispatch for socket cells never references `Node_IconBase`. Per-state activation policy (bead-ring pulse animation on `.unselected` only, static at opacity 1.0 on `.selected` / `.socketed`) is consumer-side per FR-C7 §6 (CL-36 owner visual-oracle confirmation). |
| `overlay.locatedHighlight` | `Node_Located` `0x87A89F86` (135²) | scene-bound via the 0x58-block |
| `overlay.equipGlow` | `Node_EquipGlow` `0xFC806F42` (91×90) | scene-bound via the 0x58-block |
| `start.unselected` / `start.selected` | `Template_Node_Starter` 0x58 block: filigree `0xA0F996FE` + grey hexagon `0xF8312CA8` | scene-bound; selected variant authored as same handles (no visual change) |
| `gate.unselected` / `gate.selected` | `Template_Node_Quest` 0x58 block: filigree `0xA0F996FE` + ornate-squares `0xC2DF4786` / `0x0E6B6249` | scene-bound; CL-23 mapped `0xC2DF4786 → selected`, `0x0E6B6249 → unselected` from visual inspection (state-flag bytes in the 0x58 block not RE'd — re-verify on owner visual oracle if a state-specific render is required) |

The socket recipe is verified by owner game-vs-app visual oracle on
the rebuilt app + CASC's own frame extraction
(`e:/tmp/scene-probe/socket-composite-stack.png` cross-checked against
owner atlas-frame oracle): the inner spike-frame `0x23F487F3` has a
center depression sized to seat a glyph icon, the red glowing bead
ring `0xBED4CF21` provides the pulsing animation layer, and the
ornate outer disk `0xF6443089` provides the socket-specific frame.
The engine's state dispatch for socket cells does NOT composite
`Node_IconBase` `0x1D166DC7` (the shared per-rarity grey-base would
project ~9.5 px beyond the ornate disk's silhouette as a thin grey
ring — the game never renders that on a socket in any state). Scene
657304 binds the three socket-specific handles on `Usage_Slot_2`
(the right-side equipped-glyph panel widget): the engine composites
the same atlas frames in two contexts. No additional widget in scene
657304 binds further socket-composite art under any widget name,
field type, or binding shape (the FR-C12 R2 broad probe is
exhaustive). The dispatch boundary is enforced by the row no-phantom
gate (CL-35) — see below.

**Row no-phantom gate (FR-C12 R3 / CL-35).**
`ParagonRenderLayout_socket_rows_have_no_phantom_layers` asserts
every layer in a `socket.*` row is bound on a widget the engine
actually dispatches for socket cells — the authorized set is
`{GlyphNodeGlow_Revealed, GlyphNodeGlow_Purchased, Usage_Slot_2}`.
A layer bound only on `Node_IconBase` (the per-rarity grey-base) or
any other non-socket-class widget would FAIL the gate as a phantom.
This is the dual of the row-completeness gate
(`ParagonRenderLayout_row_layers_cover_every_scene_bound_row_widget_handle`,
CL-34) which asserts no scene-bound row-widget handle is missing
from any row: completeness catches drops; no-phantom catches
fabrications/contamination. Both run at CI time on a real D4 install.

**Selected-node red ring re-verify (FR-C12 §2).** No change from
CL-30: the selected-state red ring is part of each per-rarity
selected composite (Magic-selected `0x72C29402`, Rare-selected
`0x03EDABAB`, Legendary-selected `0xBD27FB7C`; Common-selected
`0xD3051CCA` on `Node_Purchased`). The standalone `0xB732F921` from
CL-28 is in the icon catalog but bound to no scene widget and not
referenced by any per-rarity recipe.

**Special-node scene-bind gate (FR-C12 §4).**
`ParagonRenderLayout_special_node_layers_are_scene_bound` cross-
references every layer in a `RarityOverride < 0` row against the
raw scene 657304 widget data via `ReadUiScene` (parity with the
per-rarity scene-bind gate; raw widget data rather than the
icon-catalog-filtered `Scenes` view — the CL-31 → CL-32 lesson).

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
sibling table `Power_<snoName>`, labels `name`/`desc` + the
**Script Formula slot table** (FR-C13 Phase 1, CL-37). The power's
deeper gameplay record (≈6 KB of buffs / payloads / mods) stays
consumer domain. (Note: an inline `szName` exists at payload `+8`
for *some* powers but is absent for many — e.g. `CAMP_*` — so the
sibling table is the reliable name source, not that offset.)
Anchor: power `2521393` → `name` `Fathomless`. Surface:
`Diablo4Storage.ReadPower(int, locale)`. CL-22.

**Skill modifiers + the skill tree (LIB-5, CL-104).** The class skill tree is
**data-driven** (correcting an earlier premature "engine-assembled" read that was
based only on the `SkillTree` UI scene — group 46 — which is chrome + `Testnode`
templates with **zero** skill-Power references). The pieces:
- **Skills = Powers** (g29), named `<Class>_<Skill>` (`Rogue_BladeShift` = 399111,
  `Rogue_Puncture` = 364877).
- **Skill modifiers** (the skill-tree enhancement/upgrade nodes) live in the
  skill's **sibling StringList** `Power_<snoName>` as `Mod<N>_Name` /
  `Mod<N>_Description` labels — the generalized §6.7 convention. Surfaced as
  `PowerDefinition.Modifiers` (`PowerModifier{Index, Name, Description}`), ordered
  by the sparse index. **Validated against the live game**: `Rogue_BladeShift`
  decodes its 7 modifiers (Grenade Shift / Resistance / Range of Motion /
  Impossible Escape / Energy / Overpower / Resolve at indices 0,1,2,3,5,7,9),
  names + effect text matching in-game exactly; generalizes across skills.
- **Passive clusters** are g99 `Class_<Class>_<Section>_<Cluster>` records that
  list their skills as typed Power refs (`201, 1, 29=group, <PowerSNO>`; Barb
  `…_Shouts` → War Cry / Challenging Shout / Rallying Cry).
- **Open (phase-2 RE, may be data or engine):** the modifier **group** assignment
  (one pick per group; mutual exclusivity), the modifier **prerequisite** (≥1
  point in the parent skill), and the **category** point-threshold prerequisites
  (a Core skill needs N points in Basic first). Located but not yet decoded — the
  modifier *content* is complete; these structural rules are the follow-up.

**Script Formula slot table (FR-C13 Phase 1).** Powers using the
`DT_STRING_FORMULA` mechanism (every legendary node passive, plus
many active skills and structural powers) carry a tail-data array
of positional slot records that the engine resolves through
`[SF_<i>n</i>...]` placeholders in the localized `Description`
format string. CASC surfaces this table as
`PowerDefinition.ScriptFormulas` — an `IReadOnlyList<PowerScriptFormula>`
where each entry's `Index` matches the format-string SF_N indices
verbatim, `Text` is the literal text form of the formula
(`"0.02"` for trivial numeric literals, `"SF_1 / 3"` for arithmetic
expressions on other slots), and `LiteralValue` is the IEEE-754
single-precision scalar for trivial-numeric slots (Phase 1 surface).

The decoder walks the blob backward from the terminator record
`("0", 0.0)` to collect the slot table, then strips the universal
trailing `("10", 10.0)` **value-descriptor footer** — a fixed footer,
*not* the rank cap and *not* an SF_N (FR-C38 CL-107 corrected the CL-100
"max-rank sentinel" reading; the per-aspect legendary cap is
`AffixDefinition.MaxRank` at affix `+0x94`, §8.1 `CurrentLegendaryRank()`).
Its value is no longer surfaced. The
storage is positional vs the engine's SF_N indices — same as the
format-string placeholders. Some powers' formats skip SF_N indices
(Dynamism uses `SF_0`/`SF_2`/`SF_3`, skipping `SF_1`); the
positional slot table still has the corresponding entry at the
skipped index — the value just isn't referenced by the format
string.

Phase 1 lifts Layout-A records (3-character ASCII chunks like
`".15"`/`"4.5"`/`"60"`/`"10"`, followed by type tag `0x06`, then
the float). Layout-B and Layout-C records (4-character ASCII chunks
like `"0.75"`/`"0.02"` and pad-prefixed records like Demonic
Spicules's `"60"` Layout-C entry) plus expression-text records
(Demonic Spicules's `"SF_1 / 3"` for SF_2 = SF_1/3 = 20) are
deferred to Phase 2's expression evaluator. The library surfaces
`empty list` rather than fabricating a partial table when the
Layout-A walk fails — honest decode discipline (parallel to the
FR-C9 no-fabrication gates on the paragon-render side).

Phase 1 anchors (6 of 9 Warlock legendaries fully decoded; 3
deferred to Phase 2):

| Power | SNO | Stored slot table | Source |
|---|---|---|---|
| Pyrosis    | 2527268 | `[4.5]` | Phase 1 ✓ |
| Fathomless | 2521393 | `[0.15, 7, 6]` | Phase 1 ✓ |
| Overmind   | 2524552 | `[0.45, 0.65]` (IEEE-754 round-to-nearest = `0.45000002`, `0.65000004`) | Phase 1 ✓ |
| Ritualism  | 2526168 | `[0.9, 9, 15]` | Phase 1 ✓ |
| Chaos      | 2527294 | `[1.0, 2, 1]` | Phase 1 ✓ |
| Dynamism   | 2524312 | `[0.03, 1.0, 1.0, 2.0]` (SF_1 unused in format) | Phase 1 ✓ |
| Greater Hex | 2527280 | `[0.75, 0.25]` | Phase 2 (Layout B) |
| Dominion   | 2524673 | `[0.8, 0.5, 12]` | Phase 1 ✓ |
| Demonic Spicules | 2525006 | `[0.02, 60, (SF_1/3 → 20)]` | Phase 2 (Layout C + expression) |

Acceptance gate:
`PowerDefinition_decodes_script_formulas_for_anchored_legendaries`
asserts the slot tables for the 6 Phase-1-complete powers, plus a
no-crash sweep
`PowerDefinition_decodes_script_formulas_for_all_legendaries_no_crash`
exercises every legendary node power across the 8 classes (72
powers) — no decode-time exceptions on any blob, even when the
table is empty for layout-deferred powers.

**Phase 2 — resolved SF_N map + engine-function refs (CL-40).** Per
the FR-C13 R4 sign-off, Phase 2 lifts the 4-character ASCII
`Layout B` records (Greater Hex's `"0.75"`/`"0.25"` slots), the
zero-prefix `Layout C` records with ASCII at +4 (Greater Hex
sentinel/terminator), mixed 16/20-byte stride backward walks (the
Layout B records carry a 4-byte trailing pad), and multi-sentinel
stripping (Greater Hex repeats the `"10"` sentinel). Two typed
surfaces added on `PowerDefinition`:

- `IReadOnlyDictionary<string, double> ResolvedFormulas` — the
  positional slot table re-keyed by `"SF_N"`. Trivial-numeric slots
  promote their `PowerScriptFormula.LiteralValue` directly;
  expression-text slots (e.g. Demonic Spicules's `"SF_1 / 3"`) are
  evaluated by the internal `PowerScriptFormulaEvaluator` against
  the other slots' resolved values (iterative resolution to a
  fixed point; unresolvable references collapse to
  `double.NaN`).
- `IReadOnlyList<PowerFunctionRef> FunctionRefs` —
  engine-function references the power's localized
  `Description` format-string contains (e.g. Barbarian
  *Warbringer*'s `[SF_1 * PlayerHealthMax()]` surfaces
  `PowerFunctionRef("PlayerHealthMax", argSlots)`). The consumer
  registers a per-name resolver delegate to substitute the
  engine-runtime value (player-state accessors are outside CASC's
  domain — surface structurally, resolve consumer-side).

Phase 2 anchor verification (8 of 9 Warlock legendaries + Greater
Hex + Warbringer FunctionRef):

| Power | Resolved SF_N | Notes |
|---|---|---|
| Pyrosis | `SF_0 = 4.5` | trivial |
| Fathomless | `SF_0 = 0.15`, `SF_1 = 7`, `SF_2 = 6` | raw stored; the format-rendered "105% cap" = `SF_0 × SF_1 × 100` is consumer-side eval |
| Overmind | `SF_0 ≈ 0.45`, `SF_1 ≈ 0.65` | IEEE-754 round-to-nearest |
| Ritualism | `SF_0 = 0.9`, `SF_1 = 9`, `SF_2 = 15` | format-string `[1 + SF_1]` renders 10 consumer-side |
| Chaos | `SF_0 = 1`, `SF_1 = 2`, `SF_2 = 1` | trivial |
| Dynamism | `SF_0 = 0.03`, `SF_1 = 1` (unused), `SF_2 = 1`, `SF_3 = 2` | format skips SF_1 |
| Dominion | `SF_0 = 0.8`, `SF_1 = 0.5`, `SF_2 = 12` | trivial |
| **Greater Hex** | `SF_0 = 0.75`, `SF_1 = 0.25` | **Phase 2 lift** — Layout B 20-byte stride |
| Demonic Spicules | (deferred) | expression-text record (non-16-byte) Phase 3 |
| Barbarian *Warbringer* | `FunctionRefs ⊇ {PowerFunctionRef("PlayerHealthMax", [])}` | engine function surfaced from `[SF_1 * PlayerHealthMax()]` |

Acceptance gate
`PowerDefinition_resolves_phase2_formulas_and_function_refs`
verifies the above anchors. Demonic Spicules's expression-text
record deferred to Phase 3's compiled-form AST decoder.

**Phase 3 — compiled-form AST decoder + cross-validation (CL-41).**
Per the FR-C13 R5 sign-off, Phase 3 lifts the 48-byte type=`0x05`
*expression record* that Phase 1/2's backward-walk halted on, adds
a binary-AST evaluation path, and surfaces a third typed surface on
`PowerDefinition` for cross-validation against `ResolvedFormulas`.

The expression record (anchored on Demonic Spicules's
`SF_2 = "SF_1 / 3"`):

```
+0..3   pad = 0
+4..15  ASCII text (NULL-terminated within 12 bytes — "SF_1 / 3\0\0\0\0")
+16..19 type tag = 0x05  (expression marker)
+20..23 opcode marker    (observed = 7; opaque)
+24..35 pad = 0
+36..39 type tag = 0x06  (embedded-literal marker)
+40..43 IEEE-754 single  (binary operand value — 3.0f on the anchor)
+44..47 trailing opcode  (observed = 0x0E; opaque)
```

The 4-byte pad following the record explains the 52-byte backward
stride. The decoder tries `-16`, `-20`, `-52` strides in order;
the `-52` candidate must be a genuine type=`0x05` record start
(not any literal) to avoid jumping past the slot region into the
early-tail literal blocks. Demonic Spicules previously decoded as
0 slots (the expression record halted the walk); Phase 3 decodes
all 3 (`SF_0 = "0.02"` Layout B literal, `SF_1 = "60"` Layout C
literal, `SF_2 = "SF_1 / 3"` type=`0x05` expression).

`IReadOnlyDictionary<string, double> CompiledFormulas` — the
engine-truth `{SF_N → value}` map. Literal slots use the IEEE-754
single read directly from the slot record's float position
(identical to `PowerScriptFormula.LiteralValue` promoted). Expression
slots evaluate the operator tree from the text but substitute
numeric operands from the binary AST opcode region's embedded
IEEE-754 singles in left-to-right encounter order (Demonic
Spicules's `SF_2` = `SF_1 / binary_literal[0]` = `60 / 3.0f` =
`20`; the `3.0f` comes from compiled bytes at +40, not from
re-parsing the text `"3"`). When `ResolvedFormulas[SF_N]` and
`CompiledFormulas[SF_N]` disagree, the engine-compiled text and
binary forms have drifted — the R5 regression gate.

R5 cross-validation gate — 9 Warlock legendary anchors:

| Anchor | SF_N keys | `ResolvedFormulas == CompiledFormulas` |
|---|--:|---|
| Pyrosis | 1 | ✓ |
| Fathomless | 3 | ✓ |
| Overmind | 2 | ✓ |
| Ritualism | 3 | ✓ |
| Chaos | 3 | ✓ |
| Dominion | 3 | ✓ |
| Dynamism | 4 | ✓ |
| Greater Hex | 2 | ✓ |
| **Demonic Spicules** | **3** | **✓ (SF_2 = 20 via both paths)** |
| **Total** | **24** | **24 agree, 0 disagree** |

Acceptance gates
`PowerDefinition_phase3_compiled_formulas_match_resolved_for_9_warlock_anchors`
(the R5 cross-validation) and
`PowerDefinition_phase3_decodes_demonic_spicules_expression_slot`
(the expression-record anchor). AST opcode interior (the `0x07`
and `0x0E` opaque markers) deliberately not decoded — the
single-binary-operator + single-literal-operand shape covers every
expression-text slot in the 72-power live build; more complex AST
shapes would need additional record-shape RE.

### 11.3 `AffixDefinition` (group 104, `.aff`)

Identity (`snoId@0`) + localized `Name` and `Description` from the same
sibling `Affix_<snoName>` table (§6.7), labels `Name` (the display name,
e.g. `"of Limitless Rage"`) and `Desc` (the rules text). Each field is an
honest empty sentinel when its label is absent — every affix that carries
rules text has a `Desc`, but only ~1 in 4 group-104 affixes (1,464 / 6,145
on `3.1.1.72836`) carry a `Name` (the rest are unnamed system/internal
affixes). Affix magnitude/operation modeling is the consumer's (glyph-affix
magnitudes are §7.4), as is any `"Aspect …"` composition around the raw
display name. Anchors: affix `578755` (`Legendary_Barb_110`) → Name
`of Limitless Rage`; affix `2586362` → Desc `Your attacks Critically
Strike …`, no `Name`. Surface: `Diablo4Storage.ReadAffix(int, locale)`
(both fields on `AffixDefinition`) + `TryReadAffixName(int, out string,
locale)` (name without a full decode — the affix analogue of
`TryReadParagonBoardName`). CL-22, CL-87.

**Effects — which attribute(s) the affix modifies (CL-92, LIB-3).**
`AffixDefinition.Effects` is the `arModifiers` `DT_VARIABLEARRAY` at payload
`+0xB0` (descriptor `dataOff@+0xB0` / `byteSize@+0xB4`), an array of fixed
**104-byte modifier records** (`count = byteSize / 104`, verified: every one
of 5,867 authored arrays is an exact multiple of 104, 1–24 modifiers). Within
each 104-byte record (26 `int32` slots `idx0..25`):

- **`idx4` = the modified `AttributeId`** (byte `+16`) — the single load-bearing
  field. Validated 1:1 across 1,220 single-modifier affixes against the value
  token their `Desc` placeholder names (`275 → Crit_Percent_Bonus`,
  `142 → Hitpoints_Max_Percent_Bonus`, `79 → Resistance_All_Bonus_Percent`,
  `482 → Armor_Percent`, core-stat sibling blocks `4/5/6/7` flat &
  `13/14/15/16/17` percent). Zero conflicts. `idx4` unifies with the runtime
  engine `eAttribute` space `GetAttributeName` resolves (§ FR-C25/C27), and is
  *finer* than the coarse power-budget category on a node (`482 = Armor%` and
  `1125 = Damage Reduction` are distinct affix ids that a node lumps together).
- **`idx7` = the attribute parameter** (`ParamPlus12`, byte `+28`): `0xFFFFFFFF`
  when tag-agnostic; a small enum for parametric attributes (single-resistance
  element: cold `3` / lightning `2` / poison `4`); or a skill-tag GBID on
  tag-conditional attributes (`259 = Damage per Skill Tag`) — the same
  mechanism as §7.4 and FR-C28.
- `idx10/14/20/24` (values `~472..640`, params `idx11/15/21/25 = 2/4/12`) and
  the family-shared GBID `idx16` are the engine's **magnitude-formula slots** —
  shared identically across every affix of a family, therefore *not* stat
  identity.
- Sentinels skipped: `idx4 == 0` (empty/padding) and `idx4 == -1` (explicit
  "no attribute" marker, e.g. a socket-restriction modifier).

**Two AttributeId namespaces (verified CL-92).** The high bit `0x80000000`
selects the registry: a **positive** `idx4` is an engine `eAttribute` →
localized name via `GetAttributeName`; a **negative** `idx4` is a reference
into the data-defined **`DataAttributes`** designer table (SNO `1907204`) by
*ordinal* `idx4 & 0x7FFFFFFF` (verified: ordinal `84 = Barb_Berserking_
AttackSpeed`, `82 = Barb_Berserking_DamageReduction`, `86 = …MovementSpeed`).
The two namespaces are **disjoint** — never `abs()` the id (negative-208 is a
different attribute from positive-208). `AffixEffect.IsDataDefinedAttribute` /
`.DataAttributeOrdinal` expose the split; the data-defined name resolves via
`Diablo4Storage.TryGetDataAttributeName(id, out token)` (CL-93 — reads the
`DataAttributes` table szName by ordinal). The same flag appears on
`NodeAttribute` / `GlyphAffixAttributeRef` AttributeIds (node/glyph
conditional-damage refs — Shadowform/Demonform/Volatile/Overpower/kill-streak,
verified: ordinal `251 = Warlock_Demonform_Damage_Bonus`,
`252 = Multiplicative_…`), so the resolver is namespace-agnostic across all
three record families.

**`DataAttributes` table + namespace-aware `GetAttributeName` (CL-93).** The
`DataAttributes` record (SNO `1907204`, group 20 `GameBalance`) is an array of
360-byte entries (ASCII szName@+0, gbid@+256) behind a VLA descriptor at
payload `+80`/`+84`; a flagged AttributeId indexes it by ordinal. `GetAttributeName`
is the **engine** `eAttribute` resolver only — it returns `null` for a flagged
(negative) id and for the `-1` "no attribute" sentinel, routing DataAttributes
resolution to `TryGetDataAttributeName`. Its engine-side pipeline is
season-robust (runtime `id→token` node scan → `LabelByToken` → sno-4080), and
its by-id `LabelByAttributeId` fallback is **restricted to the stable low range**
(`< 481`): the drift-prone tail renumbers every build (Armor 481→482, Elites
950→953, Barrier 1124→1127), so a stale by-id entry there is intentionally
absent — a shifted id returns an honest `null` rather than a wrong name (the
CL-93 FR-C31 fix: live glyph-affix refs to pre-shift id `1124` had resolved to
`"Barrier Generation"` on a damage-while-Healthy affix).

**`GetAttributeName` coverage ceiling — a structural boundary, not a gap
(CL-97, FR-C27 R2/R3).** `GetAttributeName(id)` is **context-free and
localized**; a second read-not-curated source — the item-affix `Desc`
placeholder token (itself a sno-4080 key, keyed by the current-build id) — lifts
coverage of the ids live paragon nodes/glyph-affixes reference to **~68 % on
3.1.1.72836** (48 % → 68 %; 17 rescued). The remaining ~32 % are **budget-
category ids whose specific stat lives in the *node name*, not the
`AttributeId`** (the CL-66/CL-76 finding) — e.g. `707` is Damage-Over-Time on a
DOT node (and via the affix `Desc`) but Bleed on a bleed node: **the same id,
different sub-stats.** A context-free resolver cannot name those correctly — it
would have to pick one arbitrarily, i.e. re-introduce the FR-C31 wrong-name
defect through a different door — so **a bare-id humanized fallback is
deliberately not provided.** Those are named by `ParagonNodeStat.StatName`,
which has the node context that makes them unambiguous. So ~68 % is the honest
ceiling for the context-free localized resolver; the node-context remainder is
`StatName`'s domain by construction, not an undiscovered source.

**Value range — the `idx16` formula GBID → item-power roll curve (CL-94).**
There is no literal `(min,max)` float pair anywhere in the affix record; the
rolled magnitude is **item-power-curve driven**, and `idx16` (`AffixEffect.FormulaGbid`)
is the key: it is the `GbidHash` of an `AttributeFormulas` (SNO `201912`,
§8) entry name, resolvable via `AttributeFormulaTable.TryGetByGbid(gbid, out
formula)`. The entry's `arRanges` give, per `ItemPowerRangeStart`, the
`DT_STRING_FORMULA` source text the game rolls from — e.g. `GearAffix_CritChance
→ "FloatRandomRangeWithInterval(1,0.5,1)/100"` at low item power, `…(1,3,3.5)/100`
at high; `AffixCoreStat1x` / `AffixInversePercentage*` / `GearAffix_AttackSpeed`
similarly. The min/max a UI prints comes from **evaluating** that text at a given
`IPower()`; the library exposes the raw formula (it never evaluates — same
boundary as the paragon magnitudes, Appendix C). The one **literal** in-record
float field is the `"Static Value N"` `float32` VLA at fixed struct `+0xC0`
(`count = size/4`, positional → the `Desc`'s `[Affix."Static Value N"]`
placeholders) — set/mythic/unique fixed scalars, each a distinct quantity, on
`AffixDefinition.StaticValues` (empty for rollable stat affixes). **Operation**
(additive / percent / multiplicative) has *no* structural discriminator —
additive-vs-multiplicative twins of the same stat (`252` vs `253`, `707` vs
`708`, `736` vs `737`) are byte-identical in all 26 slots except `idx4`; combine
semantics are intrinsic to the AttributeId identity (the `Multiplicative_*` /
`_Percent` attribute name) plus the `Desc` format, surfaced implicitly via
`AttributeName` + `Description`. Surface: `AffixDefinition.Effects`
(`IReadOnlyList<AffixEffect>`, each `AttributeId` / `ParamPlus12` / `FormulaGbid`
/ resolved `AttributeName` + `HasParam` / `IsDataDefinedAttribute` /
`DataAttributeOrdinal`) + `AffixDefinition.StaticValues`. Names resolved by
`ReadAffix`: `GetAttributeName(int, uint, locale)` for positive ids,
`TryGetDataAttributeName` for negative (DataAttributes) ids (CL-94); byte-only
`Parse` leaves them empty. Anchors: `2590254` (CriticalHitChance) → attr `275`
"Critical Strike Chance", formula `GearAffix_CritChance`; `928841`
(Resistance_Dual_ColdLightning) → 2 effects attr `74` param `3`/`2`; `2292505`
(SetPower_Barb01_01) → `StaticValues [100,50,20,120]`. CL-92, CL-94.

**Inline value formulas — unique/legendary power rolls (CL-96).** A
**unique/legendary power** affix does not reference an `AttributeFormulas` GBID:
its `idx16` is the **NoGbid sentinel `0xFFFFFFFF`** (`AffixEffect.NoFormula` —
corrected from the erroneous `0` in CL-94). Its rolled value formula is instead
stored **inline** in the record as a `DT_STRING_FORMULA` located by the same
modifier's `idx10` (payload offset) / `idx11` (length), exposed as
`AffixEffect.InlineFormula` — e.g. `2HMace_Unique_Barb_100` →
`"FloatRandomRangeWithIntervalUniqueAffixPityBonus(20, 60, 80)"` (→ 60–80 per
§8.1 args 2/3), the source of that power `Desc`'s rollable `[Affix_Value_1]`
token. Same grammar/derivation as the GBID-referenced formulas (§8.1); the
`*UniqueAffixPityBonus` variants add an engine-defined pity floor that does not
change the `[min,max]` bounds. **Coverage (measured): 1,136 of the 1,168
affixes whose `Desc` carries a rollable `[Affix_Value_N]` carry a decodable
inline formula; the remaining 32 do not** (a residual — a different or absent
in-record value source; recorded, not papered over). A consumer resolves a
modifier's value as: `FormulaGbid != NoFormula` → the `AttributeFormulas` curve;
else `InlineFormula` if non-empty → evaluate directly; else no roll (its numbers,
if any, are on `StaticValues`). The `[Affix."Static Value N"]` placeholder is a
*separate* token from `[Affix_Value_N]` and resolves to `StaticValues`.

**Inline-formula blockers (LIB-3 R7, CL-100; live `3.1.1.72836`).** Of the
**2,590** g104 affixes carrying an inline formula, the ones that don't compute a
plain `[min,max]` break down as: **630 rank-scaled** (the formula calls
`CurrentLegendaryRank()` — deterministic, now printable as the span
`[formula(1) … formula(10)]` via the exposed max rank, §8.1); **86 power
cross-references** (`PowerTag.S10ChaosTuningPerClass."Script Formula N"` —
identifiable but not numerically resolved this release, §8.1/§11.2); and **2
conditional** (the Mythic-potency ternary — two runtime-conditional ranges,
§8.1). The remainder are pure roll functions that compute directly. **The 32
residual** (rollable `Desc`, no inline formula) resolves into **21 that are
`FormulaGbid`-backed** (computable via the `AttributeFormulas` GBID path —
`Boost_Legendary_*` + Season set-seal `Talisman_SealAffix_Set_*`) and **11 with
neither inline nor GBID formula** — the genuine residual: Skill-Rank *integer
grants* (`legendary_druid_108/109`), set-powers whose value references `Owner.*`
runtime state or `Affix."Static Value N"` (`Talisman_SetPower_*`), a mount-armor
unique, and one test affix (`S12_KillStreak_Feast_Test`); none is a decode gap.
These population figures are scoped to the g104-inline predicate and differ from
a consumer's rollable-predicate population (e.g. the Optimizer's 597/83) — a
population difference, not a reconciled single figure.

**Affix pool — allowed item types (#51, CL-106).** For rolled rarities
(magic/rare/legendary) an affix is drawn from a **per-item-type pool**. That pool
is encoded **per affix**, not in a central table (`AffixFamilyList` 214106 is a
family-*name* registry, mostly-empty records): each gear affix carries a
`DT_VARIABLEARRAY[int]` at payload **`+0x78`** (`dataOff@+0x78` / `byteSize@+0x7C`)
listing the `eItemType` ordinals it may roll on — semantically verified
(`CoreStat_Strength` → armor/jewelry `[16,17,28,30,29,23]`;
`CoreStat_Strength_Weapon` → the 11 weapon types; `Charm_Armor_Percent` → `[71]`;
`CritHitChance` → `[70]`). Surface: `AffixDefinition.AllowedItemTypes` (the
byte-verified primitive) + `Diablo4Storage.RollableAffixes(itemTypeId)` (the
inverted "what rolls on this type" convenience, a lazy full pass over group 104).
**The values are engine `eItemType` ordinals, NOT group-98 `ItemType` SNO ids.**
But the ordinal → name map **is in g98 after all** (#51, CL-108): each g98
`ItemType` record carries its `eItemType` ordinal as the **first int32 of the
`DT_VARIABLEARRAY` at payload `+0x28`** — surfaced as `ItemType.EItemType`. So
`16 → "Helm"`, `17 → "ChestArmor"`, `28 → "Gloves"`, `29 → "Boots"`, `30 →
"Legs"`, `26 → "Amulet"`, `19 → "Ring"`, `1 → "Axe"`, `10 → "Bow"`, `71 →
"Charm"` (build 3.1.1.72836). 1H/2H and class variants **share one ordinal**
(`Axe`/`Axe2H` are both `1`), so the map is one-ordinal-to-many-names;
`Diablo4Storage.ReadItemTypeNames()` picks the shortest equippable base name and
`GetItemTypeName(ordinal)` resolves one. (The earlier "not in g98" was a misread —
the ordinal is in the `+0x28` array, not the header fields the first attempt
scanned.) **Honest gap:** a few ordinals seen in pools — `9` (a weapon), `23` (a
Strength armor type) — have *no* g98 record and stay unnamed
(`GetItemTypeName` returns `null`, never a wrong name); they are
engine-aggregate/legacy values. Tempering pools use the same per-affix mechanism
(a temper-family-tagged subset); `TemperRecipeFamily` is likewise just a name
registry.

### 11.4 `ItemDefinition` (group 73, `.itm`)

Identity (`snoId@0`) + localized `Name`/`Flavor`/`TransmogName` from
sibling `Item_<snoName>`. Item stat/affix/power modeling is consumer
domain. Anchor: item `223287` (`1HAxe_Unique_Generic_001`) → Name
`The Butcher's Cleaver`, TransmogName `Cadaver Chopper`. Surface:
`Diablo4Storage.ReadItem(int, locale)` + `EnumerateItems(class)`. CL-22.

**Season-prefixed unique duplicates (#56, CL-109).** The live Item group carries
**leftover seasonal/PTR duplicate** unique SNOs whose CoreTOC name is
season-prefixed (`^S\d+_` — e.g. `S10_Amulet_Unique_Rogue_100_Boots`, a stale
duplicate of the canonical `Amulet_Unique_Rogue_100` with a different slot / more
explicits). Of ~1030 live `_Unique_` item SNOs, ~473 are season-prefixed. They
**share the localized display `Name`** with the canonical item — so any surface
that de-dupes uniques by display name must prefer the **non-`^S\d+_`**
`ItemDefinition.SnoName` (now exposed for exactly this test). The likely origin is
**seasonal→eternal migration**: a character's seasonal-realm item needs a distinct
SNO so it doesn't collide with the canonical eternal version, so the season
incarnation is kept under an `S<n>_`-prefixed name. No clean structural bit
separates them — the `0x10000000` flag (payload `+0x14`) correlates ~91% on
season-prefixed vs ~7% canonical but has exceptions both ways (it reads as a
seasonal-content bit that is also set on some current-season canonical uniques),
so the **name prefix is the reliable signal**, not a flag.

All four: byte-only `Parse(blob)` yields identity only (localized
fields empty — they need `CoreToc`); the deep binary beyond the
documented fields is deliberately not decoded (boundary, not a gap —
no fabricated values, mirroring the FR-C7 discipline).

### 11.5 `TiledStyleDefinition` (group 103, `.uis`)

UI tile-style records (CL-42). The engine's recipe for rendering a
tiled UI overlay (vignette, inner-shadow, bag background, frame
chrome, …) as a multi-piece composition with scale + padding.

A widget carries a `DT_SNO` field named `snoTiledStyle`
(`FieldHash("snoTiledStyle") == 0x07DB38D3`) pointing to a record in
this group; at render time the engine consults the bound TiledStyle
and composes the overlay. Distinct from the widget's `hImage` (the
primary content); `snoTiledStyle` defines the *framing/composition*
applied to the widget's rect. Group format hash `0x80504E18`.

The record is a `TiledStyleDefinition` SNO (type hash `0x02F5672C`)
holding a polymorphic `ptWindowPiece` array; CASC decodes the first
(and only, in every record observed) element. The element's
PolymorphicBase header stores the variant tag at blob `+0x50`; for
the **`NSlice`** variant (tag `0xBC0D579E` — the 9-slice composition
class, the common case) the struct fields map `struct +N → blob
+0x48+N`:

```
+0x00  uint32   magic                  = 0xDEADBEEF
+0x10  int32    SnoId                  (self-reference)
+0x50  uint32   TypeTag (= dwType)     0xBC0D579E NSlice | 0x02E46583 TiledWindowPieces | …
+0x58  float32  flImageScale           (1.0 / 0.5 / 0.9 observed)
+0x5C  uint32   nPadding
+0x60  uint32   hSourceImage           (the sliced/tiled texture handle)
+0x64  uint32   eSliceStyle            (slice mode enum)
+0x68  16 bytes DT_VARIABLEARRAY        (struct +0x20 — opaque)
+0x78  16 bytes DT_VARIABLEARRAY        (struct +0x30 — opaque)
+0x88  int32    fTileCenter            (≠0 ⇒ interior is TILED, not stretched)
+0x8C  int32    fTileHorizontalBorders (≠0 ⇒ top/bottom strips tiled)
+0x90  int32    fTileVerticalBorders   (≠0 ⇒ left/right strips tiled)
```

Field/type names cracked from the `blizzhackers/d4data`
`!!D4Checksums.yml` + `!NSlice.bc0d579e.yml` schemas (cited as intel
only — see memory `feedback_third-party-re-as-intel`). The N-slice
model — fixed corners + (optionally tiled) edges + (optionally
tiled) centre — is exactly the "raised perimeter edges + tiled
interior" composition the FR-C14 owner observations described.

Anchors verified (scene 657304 `snoTiledStyle` bindings):

| Widget | SNO id | CoreToc name | variant | `flImageScale` | `fTileCenter` |
|---|--:|---|---|--:|--:|
| `Vignette` | 843662 | InnerShadow | NSlice | 1.0 | **0** (stretched) |
| `Paragon_Points_Container` | 1309282 | Frame_AbilityPoints | NSlice | 0.5 | 1 (tiled) |
| `Points_Tutorial_Highlight` | 872641 | Tutorial_Highlight | NSlice | 1.0 | 0 |
| `ParagonStats` | 787949 | HellGothicChill | NSlice | 0.9 | 1 |
| `Board_Info` | 603760 | BagBackground | TiledWindowPieces | 0.9 | (suffix not decoded) |
| `CoreStatEntryStack` | 792649 | HellGothicSuperChillEdge | NSlice | 0.5 | 1 |

`Vignette → InnerShadow` has `fTileCenter = 0` — a *stretched*
inner-shadow, **not** a tiled pattern; it is therefore not the
paragon-board background pattern overlay (correcting the R8/R9
working hypothesis). No `SnoGroup.UiStyle` record sources the
owner-confirmed board pattern `0x22FF3AF6` (411 scanned) — the
board's pattern is rendered via the Stack-widget `ExtraLayerValues`
path, not the TiledStyle/NSlice path.

Surface: `Diablo4Storage.ReadTiledStyle(int)` returns the typed
record (NSlice variant fully decoded ⇒ `HasPartialDecode = false`;
other variants keep the tile flags at `-1` + `HasPartialDecode =
true`). The per-widget binding is on
`ParagonBoardChrome.TiledStyleBindings`. The cracked-hash registry
(`Diablo4.KnownFieldNames` / `KnownTypeNames`) is the persistent
surface for hashes recovered across FRs.

CL-42 (R9 typed-surface lift) + CL-43 (R10 NSlice full decode),
derived from FR-C14 R8's `snoTiledStyle` crack and R10's variant
+ field cracks via the `blizzhackers/d4data` checksum registries.

## 12. Character-Sheet stat model (FR-C29)

The in-game Character Sheet derives ~25 stats from the four core
attributes (Strength / Intelligence / Willpower / Dexterity) plus level,
class, Torment tier, gear and Paragon. FR-C29 asked for the per-class
derivation. The split is: the **coefficients are universal engine
constants** (not authored anywhere), and the **core→bonus map is
per-class data** (decoded from `PlayerClass`).

### 12.1 The coefficients are universal constants (not located in the data)

The per-point core→derived-stat rates were **not located in any searched
SNO source**. A thorough data-mine (2026-07, re-run against the
Paragon-corrected precise values) searched every candidate source —
`PlayerClass` (g74), `Hero` (g39), `AttributeFormulas` (201912),
`AttributeDescriptions`/`HeroDetails` (g42), `SimpleScalarFormulas`
(2536879), `LevelScaling` (206158), `DataAttributes` (1907204),
`DamageMitigation` (1846727, empty), and a whole-group float-grep over
GameBalance (g20, g49) — and found no coefficient home. The core-stat
tooltip is engine-computed: `HeroDetails` (sno 4123) `[TipStrength]` =
`Strength: {s1} … Increases Armor by {s3}` — the coefficient never
appears, only the runtime-substituted result. So the rates are not in any
searched source (engine-side, or a global config not yet identified);
being **universal**, a newly-added class reuses them without an engine
change, so they are baked as owner-oracle-validated constants
(`CharacterStatModel`; the engine-constants pattern, Appendix D):

| Derived stat | Per point | Unit / source |
|---|---|---|
| Armor | 2.0 | flat, from Strength |
| Resistance to All Elements | 0.4 | flat, from Intelligence |
| Skill Damage | 0.125 | %, from the **primary** attribute |
| Healing Received | 0.035 | %, from Willpower |
| Dodge Chance | 0.006 | %, from Dexterity |
| Critical Strike Chance | 0.0025 | %, from the **crit** attribute |
| Resource Generation | 0.005 | %, from the **resource** attribute |

Inherent base stats (class-independent): base Crit `5.0%`, Crit Damage
`50.0%`, Vulnerable Damage `20.0%`, Movement Speed `100.0%`. Validated
across four classes spanning all four primary-attribute archetypes
(Warlock, Rogue, Necromancer, Barbarian), incl. a high-Paragon Warlock
(Will 1876 → Skill Damage 234.6%) that pinned the small-magnitude rates
to three significant figures. (Skill-damage per point reads lower at
level 1 and plateaus at `0.125%` by ~L60 — the endgame value is the API
constant.)

### 12.2 The core→bonus map IS data (per class, decoded structurally)

What varies by class is *which core feeds which mobile bonus*. Four
conversions are fixed for every class (Str→Armor, Int→Resist,
Will→Healing, Dex→Dodge); three are "mobile" — **Skill Damage** goes to
the class's primary, and **Critical Strike Chance** / **Resource
Generation** go to per-class cores. This mapping is authored in the
`PlayerClass` record as three `DT_VARIABLEARRAY` descriptors at payload
`+0x40` / `+0x50` / `+0x60`, each a single `(coreIndex:int32,
weight:float32, …)` element; in slot order they name the SkillDamage core
(weight `1.25`), the Crit core, and the ResourceGen core (weight `1.0`).
Decoded structurally by `PlayerClassDefinition` (`PrimaryAttribute` /
`CriticalStrikeAttribute` / `ResourceGenerationAttribute` /
`StatConversions`) — **no per-class table is hard-coded**. A rule
inferred from the first three classes ("Crit = the core opposite the
primary") is wrong for Druid / Paladin / Spiritborn, so reading the array
is required. All eight decoded maps:

| Class | Skill Damage | Crit | Resource Gen |
|---|---|---|---|
| Warlock | Willpower | Strength | Intelligence |
| Rogue | Dexterity | Intelligence | Strength |
| Necromancer | Intelligence | Dexterity | Willpower |
| Sorcerer | Intelligence | Dexterity | Willpower |
| Barbarian | Strength | Dexterity | Willpower |
| Paladin | Strength | Intelligence | Willpower |
| Druid | Willpower | Dexterity | Intelligence |
| Spiritborn | Dexterity | Strength | Intelligence |

### 12.3 Boundary

The library returns the typed conversion table + the base constants; the
consumer composes the actual numbers (base × level scaling) + (cores ×
coefficients) + (gear/Paragon contributions). Base Max Life is
level-driven and class-independent (L1=50, L60=860, L70=1526 — anchors
for a future `CharacterBaseStats` level-curve decode); the Toughness /
damage-reduction composites (Phase 3) and the discrete Torment
multipliers (Phase 4) are engine-coded — the honest boundary (devlog
0084; `DifficultyTiers` 1973217 is a per-monster-level curve, not the
Torment-tier table).

## 13. Item base-type taxonomy (LIB-1)

Every item (`ItemDefinition`, group 73) references a **base type** — an entry
in the item-type dictionary (group 98, `GearItem`; the engine's `eItemType`
enum: `Sword`, `Amulet`, `Helm`, `Charm`, `HealthPotion`, … — ~153 entries).
The base type is what classifies an item as a weapon / armor / jewelry / charm.

### 13.1 The item→type link

An item record stores its base-type SNO id at **payload `+0x0C`**
(`ItemDefinition.ItemTypeSnoId`; e.g. `Chest_Normal_Generic_001` → `ChestArmor`
446829, `1HSword_Legendary_Generic_001` → `Sword` 446796). Resolve it with
`Diablo4Storage.ReadItemType`.

### 13.2 The ItemType record (group 98) and classification

Payload base `0x10`; `snoId` at payload `0`. The classification fields
(payload-relative): a **kind** word at `+0x08` (`32`/`48` ⇒ equippable gear;
smaller values ⇒ non-gear), a **sub-kind** at `+0x0C` (`5` ⇒ Charm), a
**weapon-family** enum at `+0x30` (`≥ 0` ⇒ a weapon-slot item — a coarse family
shared across related weapons, e.g. Axe/Sword/Mace = 1; `-1` ⇒ not a weapon), an
**armor-value scalar** (float) at `+0x3C` (`0` ⇒ jewelry, `> 0` ⇒ body armor),
and a **slot** word at `+0x44` (`> 0` for armor/jewelry). `ItemClass` is derived:

| Class | Rule |
|---|---|
| Charm | sub-kind `+0x0C == 5` |
| Weapon | weapon-family `+0x30 ≥ 0` (incl. off-hands: shield, focus, totem) |
| Armor | `+0x30 == -1`, slot `+0x44 > 0`, armor-scalar `+0x3C > 0` |
| Jewelry | `+0x30 == -1`, slot `+0x44 > 0`, armor-scalar `+0x3C == 0` |
| Other | non-equippable (kind `+0x08 ∉ {32,48}`), or an unslotted equippable (e.g. Essence) |

Verified across the full g98 set on build 3.1.1.72836: **Weapon 28** (all
melee/ranged weapons + off-hands), **Armor 5** (ChestArmor/Helm/Legs/Gloves/
Boots), **Jewelry 2** (Amulet/Ring), **Charm 1**, **Other 117** (consumables,
currency, gems/runes, quest items, caches, keys, essences, mount/companion
armor, …). Structural — no name parsing.

`ItemType.EItemType` (#51, CL-108) is the record's engine `eItemType` **ordinal**
— the first int32 of the `DT_VARIABLEARRAY` at payload `+0x28` — the value that
appears in an affix's `AllowedItemTypes` pool. `Diablo4Storage.ReadItemTypeNames()`
inverts the full g98 set into an ordinal → base-type-name map (§11.3).

### 13.3 API

- `Diablo4Storage.ReadItemType(int)` → `ItemType` (`SnoId`, `Name`, `Class`,
  `IsEquippable`, `WeaponFamily`).
- `Diablo4Storage.EnumerateItemTypes()` → the classified type dictionary
  (`.Where(t => t.Class == ItemClass.Weapon)` for every weapon type).
- `Diablo4Storage.EnumerateItems(ItemClass)` → every item of a category (every
  weapon / charm / … in the game); identity only, `ReadItem` for localized text.
- `ItemDefinition.ItemTypeSnoId` — the item→type link.
- Catalog: `AssetKind.ItemType` (via `Catalog.OfKind`) with a decoded
  `category` facet.

### 13.4 Unique item → its fixed aspect affix (LIB-4, CL-103)

A unique/legendary item's **power** is not stored in the item record — the item
(group 73) references only its **model actor** and **base-item template** (e.g.
`1HAxe_Unique_Druid_100`'s record → `axe_uniq06` model + `1HAxe_Legendary_Generic_001`
base). The power lives in an `AffixDefinition` (group 104) that shares the item's
SNO name **verbatim** — the generalized §6.7 sibling convention (the same
name-keying used for the localized sibling-StringList tables, CL-20). So the link
is by shared name, resolved `CoreToc.TryGetName(Item, id)` → `CoreToc.TryGetId(Affix,
name)` → `ReadAffix`. Name-verified across the unique roster (item `X` ↔ affix
`X`); the affix carries the item's `Effects` / `InlineFormula` (its rolled
values, §8.1/§11.3) + localized `Name`. Surface:
`Diablo4Storage.TryReadUniqueAffix(int itemSnoId, out AffixDefinition?, locale)` —
`false` for a non-unique item or a seasonal `S<NN>_`-prefixed variant whose affix
name differs. (Wiring only — no new byte layout; joins the shipped `ReadItem` +
`ReadAffix` readers.)

## Appendix A — correction log (Diablo IV errata)

What was found wrong/omitted during empirical implementation, and the
true value (the sections above already state the corrected truth).

- **CL-110 — `MonsterLevelCurves` is six per-raid-tier curves (FR-C36; corrects
  CL-105's "not in the data").** The earlier finding — `MonsterLevelCurves` (1610053)
  is "an empty name registry, no curve" — was **wrong**: the read stopped at the
  tier records' placeholder floats and never followed each record's curve descriptor.
  Each of the 6 `Raid_Tier_N` records (VLA @ `+0x50`, 320 B each) carries a
  `DT_VARIABLEARRAY` at record `+312` → 12-byte curve rows (two `int32` level + one
  `float32` scaled value, reaching 100 across the tier span). Shipped
  `MonsterLevelCurvesTable` / `Diablo4Storage.ReadMonsterLevelCurves()`. Fourth
  "not-in-the-data" miss of the session — see [[feedback_never-declare-engine-driven]].
  §8.4; devlog 0106.

- **CL-109 — item `SnoName` + season-prefixed unique-duplicate convention (#56).**
  The live Item group has ~473 leftover season-prefixed (`^S\d+_`) duplicate unique
  SNOs that share a canonical item's localized display name (different slot / more
  explicits) — a de-dup hazard for any "list uniques" / affix-pool / taxonomy
  surface. Exposed `ItemDefinition.SnoName` (the CoreTOC name, populated by
  `ReadItem` + `EnumerateItems`) so consumers prefer the non-`^S\d+_` record; the
  name prefix is the reliable signal (the `0x10000000` flag correlates ~91% but has
  exceptions). Likely origin: seasonal→eternal migration keeps the season item under
  a distinct `S<n>_` SNO. §11.4; devlog 0104.

- **CL-108 — `eItemType` ordinal → name, from g98 `+0x28` (#51; corrects CL-106's
  "not in g98").** The affix-pool ordinals in `AllowedItemTypes` *are* nameable from
  the data after all: each g98 `ItemType` record's `eItemType` ordinal is the first
  int32 of the `DT_VARIABLEARRAY` at payload **`+0x28`** — surfaced as
  `ItemType.EItemType`; `Diablo4Storage.ReadItemTypeNames()` / `GetItemTypeName(int)`
  invert the g98 set into an ordinal → base-type-name map (`16→Helm`, `17→ChestArmor`,
  `28→Gloves`, `29→Boots`, `30→Legs`, `26→Amulet`, `19→Ring`, `1→Axe`, `71→Charm`).
  1H/2H + class variants share one ordinal (`Axe`/`Axe2H`=`1`) → shortest-equippable
  representative. The CL-106 "correlation failed / needs the EXE enum" was a misread
  (it scanned the header, not the `+0x28` array). Honest gap: ordinals `9`/`23`
  appear in pools but have no g98 record → `GetItemTypeName` returns `null` (engine
  -aggregate/legacy). Owner declined the in-game oracle + asked for the EXE route;
  the answer was in the SNO data. §11.3, §13.1. Recon: `SnoScan itemtypeenum`. devlog 0103.

- **CL-107 — legendary max rank is per-aspect, at affix `+0x94` (FR-C38, `casc-fr#55`;
  corrects CL-100).** The rank cap `CurrentLegendaryRank()` reaches is **not** a
  universal `10` — it is **per-aspect**, the int32 at the g104 **affix** record's
  payload **`+0x94`**, surfaced as **`AffixDefinition.MaxRank`**. Owner
  Codex-of-Power oracle (4 aspects, 3 distinct caps) validated it exactly:
  Edgemaster's / Conceited `21`, Aspect of Coagulation `6`, Glynn's Anvil cap `16`
  — each reproducing the game's shown span as `[InlineFormula(1) …
  InlineFormula(MaxRank)]` (e.g. Edgemaster `40+(rank-1)*1` → `[40 … 60]`%).
  Present + sane on **661/661** `legendary_*` aspect affixes (caps: 21×394, 11×82,
  16×79, 6×19, …). The CL-100 `("10", 10.0)` "max-rank sentinel" was a misread —
  it is a fixed **value-descriptor footer** on the *Power* record (follows the
  `Affix_Value_N#… / 100` token), universal *because it is not the rank cap*; the
  consuming "confirmation" of `10` was tautological, refuted by the owner oracle.
  **Removed** `PowerDefinition.MaxRank` + `.MaxLegendaryRank`; the footer is still
  stripped from `ScriptFormulas` (not an SF_N) but its value is no longer
  surfaced. Caveat (Glynn's): a multi-value aspect's tooltip range is not always
  `[f(1), f(MaxRank)]` of one term — the span rule holds per value-term. §8.1
  (`CurrentLegendaryRank()`), §8.2. Recon: `SnoScan maxrankscan`. devlog 0101.

- **CL-106 — affix pool: allowed item types (#51).** For rolled rarities an affix
  is drawn from a per-item-type pool encoded **per affix** (not in `AffixFamilyList`,
  a mere name registry): a `+0x78` `DT_VARIABLEARRAY[int]` of `eItemType` ordinals.
  `AffixDefinition.AllowedItemTypes` (primitive) + `Diablo4Storage.RollableAffixes(itemTypeId)`
  (inverted convenience). Verified: `CoreStat_Strength_Weapon` → the weapon types,
  `CritHitChance` → `[70]`. The values are engine `eItemType` ordinals, **not**
  g98 SNO *ids* — but they **are** nameable from g98 (`ItemType.EItemType` at
  `+0x28`); the "names need an oracle/EXE" call here was wrong — see **CL-108**.
  §11.3; devlog 0100.

- **CL-105 — `MonsterNames` registry (FR-C35) + `MonsterLevelCurves` finding
  (FR-C36).** `Diablo4Storage.ReadMonsterNames(locale)` → `MonsterNameRegistry`:
  the elite-monster name-affix fragments (token → localized text → prefix/suffix
  kind), from the `MonsterNames` StringList (group 42, 1,277 labels;
  `FrozenSuffix004` → "Frostburn"). Prefix/suffix inferred from the token
  spelling (honest). FR-C36 (`MonsterLevelCurves` 1610053) resolved as a
  **finding, not a reader**: the table is a 6-entry name registry
  (`Raid_Tier_0..5`) with near-empty records (placeholder `1.0`s, identical
  across tiers) — no per-level curve; the scaling is `DifficultyTiers` (§8.3).
  §8.4; devlog 0099.

- **CL-104 — skill modifiers + the skill tree is data-driven (LIB-5, retraction).**
  Retracts an earlier "skill trees are engine-assembled" boundary claim — it was
  wrong, made from a shallow look at the `SkillTree` UI scene (chrome/templates,
  0 skill refs) without decoding the actual data. The tree IS data: skills =
  Powers (g29); a skill's selectable **modifiers** are `Mod<N>_Name` /
  `Mod<N>_Description` labels in its sibling StringList `Power_<snoName>` (§6.7),
  now surfaced as `PowerDefinition.Modifiers` (`PowerModifier`). Validated
  against the in-game Rogue tree (Blade Shift's 7 modifiers, exact names + text).
  Passive clusters are g99 `Class_*` records → typed Power refs. Open follow-up:
  modifier groups / prerequisites / category thresholds (located, may be data or
  engine — do not pre-declare). **Discipline: don't assert an engine boundary
  from partial coverage** (~15–20 of 182 groups were modeled); the owner caught
  this the way the Optimizer catches over-claims. §11.2; devlog 0098.

- **CL-103 — unique item → fixed aspect affix wiring (LIB-4, proactive).** A
  unique item's power is the same-name `AffixDefinition` (the item record refs
  only its model actor + base template, not the affix). `Diablo4Storage.TryReadUniqueAffix(itemSnoId)`
  joins `ReadItem` + `ReadAffix` by the shared §6.7 sibling name → the item's
  `Effects`/`InlineFormula`/localized `Name`. Name-verified 5/5 across the unique
  roster; `false` for non-uniques / `S<NN>_`-prefixed variants. Wiring only, no
  new byte layout. §13.4; devlog 0097.

- **CL-102 — `LevelScaling` remaining columns exposed raw (companion to FR-C34,
  `casc-fr#50`).** `LevelScalingRow` / `LevelScalingTable.Row(level)` /
  `.Rows` now expose all 53 columns of the row (`.Columns`), so no column is
  hidden — but **only `hpScalar` (col `+4`) is named** (it is oracle-anchored via
  base Life, §8.2). The Optimizer's companion ask was to type `monsterDr` /
  `powerBase` / `powerDelta` / `powerItem` / `xpScalar` ("on your terms, not
  Maxroll's"). RE finding: **those names cannot be verified from the blob** —
  unlike `DifficultyTiers`'s XP anchor there is no anchor or in-game oracle, so
  asserting them would repeat the FR-C31 wrong-name defect. Shipped the raw
  exposure + a per-level behavioral characterisation (§8.2) instead; naming is
  blocked pending the d4data column-order schema or an owner oracle. Honest
  boundary, not a guess. Recon: `SnoScan rawhex`.

- **CL-101 — `DifficultyTiers` per-monster-level curve + §8.2 reconciliation
  (FR-C34, `casc-fr#50`).** Typed the monster/content scaling table (SNO
  1973217, group 20): `Diablo4Storage.ReadDifficultyTiers()` → `DifficultyTiersTable`
  — 150 rows (monster levels 1..150, VLA @ payload `+0x50` → 150 × 128 B),
  byte-verified against the live blob. Row layout locked by an **independent**
  anchor: `+36` per-level XP reproduces the game curve (L40 = 8.0, L70 = 11.0).
  `+4`/`+8` HP/damage multipliers are **inferred** labels (no monster-HP oracle
  exists — D4 shows health as a bar; AC-3), the ~26 other columns exposed
  unlabeled on `.Columns`. **Corrected §8.2**: its "one `hpScalar` column serves
  both populations" was wrong — monster HP scales off *this* far steeper curve
  (×101,051 vs ×30.5 at L70), not `LevelScaling` rows 71–200. Monster-data recon
  (owner ask): monsters are Actor SNOs (g1, ~61k; `.acr` = identity + appearance/
  anim, no base-HP field); base HP is engine-assembled (not a flat field), the
  same boundary as the player base `50`. Recon: `SnoScan strdump`. §8.3; devlog 0095.

- **CL-100 — max legendary rank + affix formula grammar (LIB-3 R7, `casc-fr#45`).**
  ⚠️ **The max-rank portion was WRONG — see CL-107.** Rank-scaled aspect affixes
  call `CurrentLegendaryRank()` (deterministic, not a roll); their printable value
  is a rank *span*, which needs the max rank. Claimed (incorrectly): the max
  legendary rank is a **universal engine constant = 10** — every one of the
  **699** `legendary_*` Power records terminates its script-formula tail with an
  identical `("10", 10.0)` record. **This was a misread** (that record is a
  value-descriptor footer, not the rank cap; the real cap is per-aspect at affix
  `+0x94` — CL-107). The `PowerDefinition.MaxRank` / `.MaxLegendaryRank` members
  shipped here were **removed**. Rank is 1-based (the dominant
  `…(CurrentLegendaryRank()-1)…` shape), so the span is `[formula(1) …
  formula(MaxRank)]`. Also (these stand): rewrote **§8.1 as a
  grammar** (ternary `?:`, relational `>`, variable references) — a bare
  identifier is a `DataAttributes` token by name (`S14_Mythic_UniquePotency` =
  `[280]`), and `PowerTag.<Name>."Script Formula N"` is a cross-reference,
  identifiable but not numerically resolved (its referent `S10ChaosTuningPerClass`
  needs the deferred binary-AST decode). Residual: of 32 rollable-Desc affixes
  with no inline formula, **21 are GBID-backed** (already computable) and **11**
  are genuine non-roll residuals (Skill-Rank grants / `Owner.*` set powers / a
  test affix). §8.1, §11.2, §11.3. Recon: `SnoScan inlinedump` / `affixstr` /
  `ranksentinel` / `rollableresidual`.

- **CL-99 — base Max Life from `LevelScaling` (FR-C29 Phase 2, `casc-fr#41`).**
  `Diablo4Storage.ReadLevelScaling()` → `LevelScalingTable`: the per-level
  `hpScalar` curve (SNO 206158, VLA @ payload `+0x50` → 200 rows × 212,
  `hpScalar` @ col `+4`, row = level−1) and the **class-independent base Max
  Life** projection `BaseLife(level) = round(50 × hpScalar[level])`
  (round-half-away-from-zero; L2 `51.5→52`, L60 `860`, L70 `1526`). Byte-verified
  against the raw blob; the base `50` is baked per the engine-constants pattern
  (fitted 15/15, not yet located). **The `1526` a tooltip shows is a rounded
  product that exists nowhere in the data** — the operands do; this closed only
  after searching for the operands, not the result (§8.2). §8.2.

- **CL-98 — `ParagonMagnitudeFormula.TryEvaluate` (FR-C33, `casc-fr#49`).**
  `Evaluate` returns a silent `double.NaN` both for a genuinely-NaN result and
  for a formula that references an engine function the library doesn't implement
  (the six budget-multipliers are the only supported calls) — a consumer can't
  tell them apart and propagates the NaN into a displayed number. New
  `TryEvaluate(text, out value)` returns `false` (value `NaN`) when any function
  ref is unsupported (re-checked against `ResolveBudgetMultiplier`), `true` with
  the computed value otherwise. Consistent with the honest-sentinel principle
  applied elsewhere (FR-C31 `null` over a wrong name).

- **CL-97 — read-not-curated attribute names from item-affix Desc (FR-C27 R2,
  `casc-fr#39`).** CL-88 made `GetAttributeName` season-*durable* (tokens
  survive renumbering) but not more *covering* — it still resolves only the ~40
  curated `LabelByToken` tokens, so ~half of live-referenced ids returned
  `null`. New source: an item-affix `Desc` placeholder (`[Crit_Percent_Bonus *
  100|%|]`) names the affix's modified attribute with a token that **is itself a
  sno-4080 key**, keyed by the current-build `AttributeId` — a read-not-curated
  `id → label` map, built once from a full g104 scan and cached, consulted after
  the node path. **Measured:** over the 85 live positive attribute ids that
  nodes (g106) + glyph affixes (g112) reference, `GetAttributeName` coverage
  rose **48.2% → 68.2%** (17 ids rescued, e.g. `707 → "Damage Over Time"`,
  `1207 → "Lucky Hit Chance"`). **It does not fully close the gap:** ~32%
  (node/glyph-only ids no affix references, e.g. `256`, `322`) still return
  `null` — the honest residual, not a wrong name. Fully closing needs either a
  node-side read source (not yet located) or curation for the tail. §11.3.

- **CL-96 — inline value formulas for unique/legendary power rolls (LIB-3 R5,
  `casc-fr#45`) + a CL-94 sentinel fix.** The CL-94 delivery claim that the
  `idx16`→`AttributeFormulas` chain gives "305 uniques" a value source was an
  **overreach** — it resolves *gear* stat affixes; a unique/legendary power's
  `idx16` is the NoGbid sentinel `0xFFFFFFFF`, so the GBID chain returns nothing.
  Its rolled value formula is **inline** in the record: a `DT_STRING_FORMULA`
  located by the modifier's `idx10` (offset) / `idx11` (length), now exposed as
  `AffixEffect.InlineFormula` (e.g. `2HMace_Unique_Barb_100` →
  `"FloatRandomRangeWithIntervalUniqueAffixPityBonus(20, 60, 80)"`). Measured
  coverage: **1,136 of 1,168** affixes with a rollable `[Affix_Value_N]` desc
  carry a decodable inline formula (32 residual, recorded). Also **fixed
  `AffixEffect.NoFormula`**: it was `0` in CL-94, but `idx16` is *never* `0` —
  the real NoGbid sentinel is `0xFFFFFFFF` (a consumer testing
  `FormulaGbid != NoFormula` was wrongly treating every unique's `0xFFFFFFFF`
  as a resolvable GBID). §8.1, §11.3.

- **CL-95 — formula function contracts (LIB-3 R3, `casc-fr#45`) + a CL-93
  coverage correction (FR-C31 R2, `casc-fr#46`).** *(a)* Recorded the
  `AttributeFormulas` roll-function contracts and the min/max derivation rule
  (§8.1): `FloatRandomRangeWithInterval(g, min, max)` → args 2/3 are the roll
  bounds (`g` = granularity only — proven by the `GearAffix_CritChance` ladder),
  `RandomInt(lo, hi)` inclusive, `IPower()` range-selected, `ROUND` nearest
  (tie-break engine-defined, ≤1 effect); and confirmed `RangeValue1/2` are
  output **clamps**, not the roll spread (2580 ranges collapse to round
  formula-independent bounds). The residual engine-uncertainties don't affect
  the printed range. `FormulaRange` XML clarified. *(b)* **The CL-93 claim "no
  current coverage is lost" was wrong.** Three ids — `954`, `1120`, `1124` —
  are live-referenced by group-112 glyph affixes, are **not** node-scannable,
  and now return `null` (a small, accepted loss). Root cause is not stale rot
  but **reassigned ordinals**: the multiplicative-variant id is the additive id
  **+1** in the same engine namespace (verified — 40/44 `Mult*` glyph-affix ids
  resolve via `id-1` to their exact stat: `1124→1123` "Damage while Healthy",
  `954→953` "Damage to Elites", `1120→1119` "Damage to Healthy Enemies"; the
  same additive-at-N / `Multiplicative_`-at-N+1 convention seen in the
  `DataAttributes` pairs). Deliberately **not** shipped as a blind `id-1`
  fallback — it over-applies (`162→161` "Maximum" truncated; `253/255/260` are
  FR-C28 compound-base whose bare label needs `ParamPlus12`). The proper
  resolution for these live-but-unresolved glyph-affix ids is the affix-`Desc`
  name source (FR-C27 / `#39`). §8.1, §11.3.

- **CL-94 — affix value range: `idx16` → the item-power roll formula (LIB-3
  R2, `casc-fr#45`).** The modifier `idx16` GBID (`AffixEffect.FormulaGbid`,
  byte `+64`) is the `GbidHash` of an `AttributeFormulas` (SNO 201912) entry —
  the affix's **value-by-item-power** curve, resolvable via the new
  `AttributeFormulaTable.TryGetByGbid(gbid, out formula)` (verified: crit affix
  → `GearAffix_CritChance` → `"FloatRandomRangeWithInterval(1,0.5,1)/100"`;
  `AffixCoreStat1x`; `GearAffix_AttackSpeed`). So the value range is
  **data-driven, not engine-coded** — the library exposes the raw per-
  `ItemPowerRangeStart` formula text; evaluation stays the consumer's (paragon
  magnitude boundary). Also shipped: `AffixDefinition.StaticValues` — the
  `"Static Value N"` `float32` VLA at struct `+0xC0` (set/unique fixed scalars,
  `Desc`-indexed; `2292505 → [100,50,20,120]`); and `AffixEffect.AttributeName`
  now resolves **negative (DataAttributes) ids** via `TryGetDataAttributeName`
  (was empty). §11.3.

- **CL-93 — namespace-aware `GetAttributeName` + `DataAttributes`
  resolution; stop stale wrong-names (FR-C31/C32 + LIB-4,
  `casc-fr#46`/`#47`/`#48`).** Three coupled fixes on the attribute-name
  surface. **(1) FR-C32 — bit 31 on `AttributeId`** is a namespace flag, not a
  sign: a negative id references the `DataAttributes` designer table (SNO
  1907204) by ordinal `id & 0x7FFFFFFF` — a *disjoint* namespace from the engine
  `eAttribute` registry (never `abs()`; engine-254 ≠ DataAttributes-254). New
  `Diablo4Storage.TryGetDataAttributeName(int, out string)` resolves it
  (verified against nodes/glyph-affixes/item-affixes: ordinal 84 =
  `Barb_Berserking_AttackSpeed`, 251 = `Warlock_Demonform_Damage_Bonus`, 252 =
  `Multiplicative_…`). `AttributeId == -1` confirmed as the "no attribute"
  sentinel. **(2) FR-C31 — stale wrong-names**: the by-id `LabelByAttributeId`
  fallback returned a stale pre-shift name (id `1124 → "Barrier Generation"` on a
  damage-while-Healthy glyph affix). Fix: the fallback is now restricted to the
  season-stable low range (`< 481`, `AttributeNames.StableAttributeIdRangeExclusiveMax`);
  the drift-prone tail resolves via the runtime token scan or returns honest
  `null` — never a wrong name. `GetAttributeName` also returns `null` for flagged
  (negative) ids (a disjoint namespace). **(3) LIB-4** — the `GetAttributeName` /
  `AttributeNames` XML docs regenerated: CL-88 token-scan pipeline described as
  primary, examples use current ids (482/953, not stale 481/950). §11.3.

- **CL-92 — item/aspect affix effects: which attribute(s) an affix
  modifies (LIB-3, `casc-fr#45`).** `AffixDefinition.Effects` decodes the
  `arModifiers` `DT_VARIABLEARRAY` at payload `+0xB0` — an array of fixed
  104-byte modifier records (`count = byteSize / 104`; verified exact across
  all 5,867 authored arrays, 1–24 modifiers). Per record: `idx4` = the
  modified `AttributeId`, `idx7` = its parameter (`ParamPlus12`);
  `idx10/14/20/24` + the `idx16` GBID are family-shared magnitude-formula
  slots (not stat identity). **A disproven first hypothesis was held off**:
  `idx10` (`480`/`472`) is *not* the modified stat (the same value recurs
  across unrelated stats); the real key is `idx4`, validated 1:1 across 1,220
  single-modifier affixes against their `Desc` value-token (zero conflicts).
  `idx4` unifies with the runtime `eAttribute` space `GetAttributeName`
  resolves. **Two namespaces (verified):** positive `idx4` = engine attribute;
  negative `idx4` = a `DataAttributes` (SNO 1907204) ordinal `idx4 &
  0x7FFFFFFF` (ordinal 84 = `Barb_Berserking_AttackSpeed`) — disjoint, never
  `abs()`. Sentinels `idx4 ∈ {0, -1}` skipped. **Magnitude/operation are not
  in the record**: no `(min,max)` float pair exists at any position; standard
  affixes are item-power-curve driven (keyed by `idx16`); operation has no
  structural discriminator (additive/multiplicative twins `252`/`253`,
  `707`/`708` are byte-identical but for `idx4`) — both stay implicit
  (`AttributeName` + `Description`) per Appendix C. Surface: `AffixEffect`
  (`AttributeId`/`ParamPlus12`/`AttributeName` + `HasParam`/
  `IsDataDefinedAttribute`/`DataAttributeOrdinal`) on `AffixDefinition.Effects`;
  names via `ReadAffix` → `GetAttributeName`. §11.3.

- **CL-91 — install auto-detection for `Diablo4Storage.Open()` (LIB-2).**
  No-arg `Open()` / `OpenAsync()` + `TryLocateInstall(out path)` resolve the
  install root from the `WISEOWL_CASC_INSTALL` override then the Windows
  registry — the Battle.net uninstall entry `…\Uninstall\Diablo IV` →
  `InstallLocation`. **Note:** the Battle.net uninstaller is 32-bit, so the key
  is under `WOW6432Node`, not the 64-bit view; the locator tries both.
  Dependency-free (`reg.exe` via `Process`, guarded by
  `OperatingSystem.IsWindows()`); a candidate is accepted only with a
  `.build.info`. Not a byte-format change.

- **CL-90 — item base-type taxonomy: weapon/armor/jewelry/charm
  classification + enumeration (LIB-1, §13).** First proactive
  comprehensive-data-exposure work item. Group 98 (`GearItem`) is the
  `eItemType` dictionary (~153 types); an item (group 73) names its base type
  at payload `+0x0C`. Decoded structurally (no name parsing): kind `+0x08`
  (32/48 = gear), sub-kind `+0x0C` (5 = Charm), weapon-family `+0x30` (≥0 =
  weapon-slot, -1 = not), armor-scalar `+0x3C` (0 = jewelry, >0 = armor), slot
  `+0x44`. Ships `ItemType`/`ItemClass`, `Diablo4Storage.ReadItemType` /
  `EnumerateItemTypes` / `EnumerateItems(ItemClass)`,
  `ItemDefinition.ItemTypeSnoId`, and `AssetKind.ItemType` with a decoded
  `category` facet. Verified counts (build 3.1.1.72836): Weapon 28 / Armor 5 /
  Jewelry 2 / Charm 1 / Other 117.

- **CL-89 — Character-Sheet stat model: universal coefficients +
  structural per-class core→bonus map (FR-C29 Phase 1, §12).** The
  premise that the per-class core→derived-stat coefficients are authored
  data was **half wrong**: a nine-source data-mine (devlog 0084, re-run
  against the Paragon-corrected precise values) found no coefficient home
  (the core-stat tooltip substitutes a runtime-computed result rather than
  exposing them — engine-side, or a global config not yet located), but
  owner core-stat oracles across all four primary-attribute archetypes
  showed the coefficients are **universal** (identical for every class —
  so a newly-added class reuses them, no per-class data needed), so they
  bake as validated constants (`CharacterStatModel`). What *is* per-class — and *is*
  in the data — is the **map** of which core feeds Skill Damage / Crit /
  Resource Generation, authored as three `(coreIndex, weight)` arrays in
  the `PlayerClass` record (payload `+0x40`/`+0x50`/`+0x60`). Decoded
  structurally (`PlayerClassDefinition.PrimaryAttribute` /
  `CriticalStrikeAttribute` / `ResourceGenerationAttribute` /
  `StatConversions`); a "Crit = opposite the primary" rule that fit the
  first three classes is wrong for Druid/Paladin/Spiritborn, so reading
  the array is required — no per-class table is hard-coded. Phases 2–4
  (base-Life curve, Toughness composite, Torment multipliers) stay at the
  honest engine-coded/level-curve boundary.

- **CL-88 — season-robust `GetAttributeName` via runtime `id→token`
  resolution; retires the fragile curated `AttributeId` map (FR-C27 on
  `casc-fr#39`).** FR-C27 was filed on the premise that `DataAttributes`
  (sno `1907204`) is the engine's full `AttributeId` registry whose
  offset just needed pinning. **That premise is wrong:** `DataAttributes`
  is the *designer/season-extensible* attribute table (281 entries on
  `3.1.1.72836` — skill consumes `Flurry_Consume_2`, socketables, class
  forms, seasonal `S14_Mythic_*` appended at the tail), all `gbid =
  0xFFFFFFFF`; it does **not** contain the core attributes (`Strength`,
  `Armor`, the conditional-damage family) the name map covers. The real
  finding: the raw `AttributeId` is a **registry ordinal the engine
  renumbers every build** — Season 14 moved `Armor` 481→482,
  `Damage_Bonus_At_High_Health` 1120→1123, `Damage_Bonus_To_Near`
  1102→1105, `Barrier` 1124→1127 — so any hardcoded `id→name` map (the
  CL-78 `LabelByAttributeId`) silently rots each season. The durable key
  is the `Generic_<Rarity>_<Token>` **node-name token**, which never
  changes. CL-88 makes `Diablo4Storage.GetAttributeName` scan the live
  `Generic_` nodes once (cached) for the current build's `id→token` map,
  then maps token → `AttributeDescriptions` label via the season-stable
  `AttributeNames.LabelByToken`, reusing the existing sno-4080
  localization. The FR-C28 compound (tag-conditional) map is likewise
  re-keyed onto `(baseLabel, ParamPlus12)` via
  `AttributeNames.CompoundBaseLabelById` /
  `NameByCompoundLabelKey`, so a base-id renumber no longer strands the
  tag names. `LabelByAttributeId` is retained only as a defensive
  fallback. Also corrected a latent curation bug the new coverage test
  surfaced: `BlockChance`'s label is `Block_Chance`, not the absent
  `Block_Chance_Bonus`. 135/135 green on `3.1.1.72836`; the resolver
  tracks every season's id shift with no code change. Devlog 0083.

- **CL-87 — `AffixDefinition.Name` + `Diablo4Storage.TryReadAffixName`
  (FR-C30 on `casc-fr#42`).** The §11.3 affix reader already resolved
  the sibling `Affix_<snoName>` table's `Desc` label; the same table
  also carries a `Name` label (the localized display name) that was
  simply never surfaced. CL-87 adds `AffixDefinition.Name` (read
  alongside `Description` in `ReadAffix`) and the standalone
  `TryReadAffixName(int, out string, locale)` — the affix analogue of
  the §6.4 `TryReadParagonBoardName`, name-keyed via `CoreToc`, raw
  value only, honest `false`/`Empty` when the affix has no `Name`.
  Consumer motivation: aspect display names (`"Aspect of …"`) are absent
  from Maxroll's `data.min.json` (desc-only) and from `CoreTOC.dat` (only
  the internal slug), making this the sole first-party name source; the
  consumer owns the `"Aspect"` composition around the raw fragment. On
  `3.1.1.72836` (Season 14) 1,464 / 6,145 group-104 affixes carry a
  `Name`; the remainder are unnamed system/internal affixes → honest
  empty. Anchors: `578755` `Legendary_Barb_110` → `of Limitless Rage`,
  `1199626` `Legendary_Barb_109` → `Devilish`, charm `2586362` Desc-only
  → empty `Name` + `false`. No byte-layout change — pure sibling-label
  surface. Devlog 0082.

- **CL-86 — `ParagonGlyphDefinition.LocalizedTitle` sibling-StringList
  pattern switch (FR-C24 Headhunter counter-round on `casc-fr#36`).**
  CL-79 picked the <c>Item_ParagonGlyph_&lt;SnoName&gt;</c> sibling
  table for the localized title (and stripped the universal
  <c>"Glyph: "</c> prefix); that table is emitted only for the
  numbered <c>Rare_&lt;NN&gt;_&lt;Stat&gt;_&lt;Slot&gt;</c> shape and
  is **missing** for the <c>Rare_&lt;Stat&gt;_Generic</c> shape —
  glyph sno `2117207` `Rare_Will_Generic` (in-game title
  `"Headhunter"`) had no `Item_ParagonGlyph_Rare_Will_Generic`
  sibling, so its `LocalizedTitle` returned empty. CL-86 swaps the
  lookup to the **non-prefixed** sibling
  <c>ParagonGlyph_&lt;SnoName&gt;</c> which exists for every glyph
  and carries the bare title directly (no `"Glyph: "` prefix to
  strip). For numbered glyphs both tables exist (`Item_`-prefixed
  carries `"Glyph: Guzzler"` + a `Description` label; non-prefixed
  carries `"Guzzler"`); for the `_Generic` shape only the
  non-prefixed table exists. The non-prefixed table is the canonical
  source. 127/127 tests green on `3.0.2.71886`. Devlog 0081.


- **CL-85 — tag-conditional `(AttributeId, ParamPlus12)` attribute-
  name resolution (FR-C28 — `casc-fr#40`).** The CL-78 honesty note
  flagged that `AttributeId 259` (`DamageBonusTag`) returned `null`
  from `GetAttributeName` because the same id covers many distinct
  display strings (Abyss / Demonology / Conjuration / Hellfire / …
  Damage) — the per-tag identity lives in `ParamPlus12`, not the
  AttributeId. CL-85 ships a parallel
  `AttributeNames.LabelByCompoundKey: IReadOnlyDictionary<(int, uint),
  string>` keyed on the raw tuple, plus a
  `Diablo4Storage.GetAttributeName(int, uint, string)` overload, and
  threads `ParamPlus12` through `ParagonNodeInfoBuilder.ResolveStatName`
  so every multi-attribute node picks up the resolved name
  automatically (the FR's anchor: `Warlock_Rare_006` attr 259,
  `ParamPlus12 = 0x32ABA6FB` → `"Demonology Damage"`). Coverage: 100+
  curated entries across 17 AttributeIds (the full set surfaced by the
  affix + node tuple scan on the live `3.0.2.71886` build). Hash
  recovery: 19 skill-tag GBIDs cracked via the `Skill_<TagName>`
  lowercased-DJB2 pattern (`Skill_Demonology` = `0x32ABA6FB`,
  `Skill_Abyss` = `0x6A1F0A80`, etc.), appended to
  `docs/d4-hash-dictionary.md`. The remaining ~30 GBIDs (Archfiend /
  Conjuration / Companion / Corpse / Earthquake / etc.) don't match
  the `Skill_<Name>` pattern — engine-internal key is something else;
  empirical names carried from the affix / node sno-name convention
  (every `<Tag>Damage_*` affix + every `Generic_Magic_Damage<Tag>` node
  confirms the same display string). 127/127 tests green on
  `3.0.2.71886`. Devlog 0080.

- **CL-84 — `ParagonGlyphAffixDefinition` structural decode of the
  affix-side fields (FR-C24 slice 2b — `casc-fr#36`).** Closes the
  affix half that CL-83 left open. The `.gaf` record carries an
  op-coupled byte layout: the `ptAttributes` descriptor (an
  8-byte packed `(int AttributeId, uint ParamPlus12)` pair) sits at
  payload `+16/+20` on Op-1 (Attribute), `+64/+68` on Op-2
  (NodeAmplification), `+104/+108` on Op-4 (AttributeConversion),
  and is absent on Op-5 (Power — magnitude lives in the linked
  `snoPower` PowerDefinition at `+88`, group 29). The
  per-op-fixed `Tags` GBID list at `+120/+124` carries the affix's
  classification anchors (the universal `0xD4A1BC54`
  "ParagonGlyphAffix root" + a class-attribute anchor + the
  per-skill-tag selector — Abyss `0x6A1F0A80`, Archfiend
  `0x945652E5`, Demonology `0x32ABA6FB`, etc. — names tracked in
  `docs/d4-hash-dictionary.md`). `flDisplayFactor@+84` is a
  per-op engine constant (`100` on Op-1/4, `500` on Op-2, `1` on
  Op-5), surfaced verbatim. The byte at `+24` (`eAffectedNodeRarity`)
  is universally `0` across all 314 live affixes; the existing field
  doc was overspecified (no record authors a 1/2/3 value); the typed
  `AffectedRarityKind` projection returns `null` for the "any
  rarity" sentinel. Two fields the FR asked for are **not encoded in
  the `.gaf` bytes**: the per-class "+40 Willpower" / "+25
  Intelligence" gate on Op-2 main affixes and the "unlocks at
  Level 50" gate on Op-4 `Mult*_Legendary` are engine-coupled
  constants (memory `[[project_engine-controller-code-encrypted]]`)
  — runtime-bound on the same axis as the encrypted controller
  code, library cannot ship them. Surface:
  `ParagonGlyphAffixDefinition.OperationKind` (typed enum), `.DisplayFactor`,
  `.AffectedAttributes` (`IReadOnlyList<GlyphAffixAttributeRef>`),
  `.Tags` (`IReadOnlyList<uint>`), `.LinkedPowerSnoId` (`int?`),
  `.AffectedRarityKind` (`ParagonRarity?`); new typed
  `ParagonGlyphAffixOperation` enum + `GlyphAffixAttributeRef`
  record struct. The §7.4 table is corrected verbatim
  (formatHash was decimal — added the hex form `0xB460195F`; the
  doc-asserted `eAffectedNodeRarity` 1/2/3 mapping is now noted
  as the sentinel `0` everywhere; the op enum names re-pinned —
  4=`AttributeConversion`, 5=`Power`, correcting an inverted pair
  in the prior comment). Acceptance: live matrix exercises one
  affix per op (Op-1 `Nodes_BonusToMinion` 1031882 = 27 attribute
  grants; Op-2 `DamageWhileHealthy_Intelligence_Side` 1068542 = 2
  attrs + 3 tags + DF=500; Op-4 `MultCritDmgPercent_Legendary`
  2111927 = 1 attr + DF=100; Op-5 `DamageElite__Strength_Legendary`
  2098405 → `LinkedPowerSnoId=2072755` = `ParagonGlyph_DamageElite`).
  127/127 tests green on `3.0.2.71886`. Devlog 0079.

- **CL-83 — `ParagonGlyphDefinition` engine constants for radius +
  cap (FR-C24 structural slice, glyph half).** The Optimizer's
  CL-79 consume-verify (`casc-fr#36`) counter-roundered for the
  8 structural fields deferred from slice 1. This CL closes the
  3 glyph-side fields (`BaseRadius`, `RadiusUpgradeLevels`,
  `MaxLevel`); the affix-side 4 fields (`DisplayFactor`,
  `AffectedAttributes`, `SkillTagSelector`, `Requirements`)
  stay on the FR for a focused follow-on CL. Decode finding:
  the `.gph` record carries no per-glyph variance — payload
  ends at the affix-array descriptor with no `nStartingSize` /
  `arSizeUpgradeLevels` / `nMaxLevel` fields. Empirical cross-
  validation against the Optimizer's Warlock-21 oracle confirms
  every glyph uses the same values (`BaseRadius=3`,
  `RadiusUpgradeLevels=[25, 50]`, `MaxLevel=150`); the
  `ParagonGlyphExperienceTable` (sno `810212`, GameBalance
  type 49) ships a 201-entry XP curve but no cap field. Surface:
  `ParagonGlyphDefinition.BaseRadius` / `RadiusUpgradeLevels` /
  `MaxLevel` exposed as instance properties (forward-compat
  shape — if a future season ships per-glyph variance, the
  property migrates from constant-return to record-decode
  without consumer API churn). Pattern parallels
  `ParagonPowerBudget` (CL-68 budget multipliers). Appendix D
  re-verify trigger updated. Acceptance: live matrix asserts
  the three constants on a sample glyph. 126/126 tests green on
  `3.0.2.71886`. Devlog 0078.

- **CL-82 — `ParagonTooltipChrome.Divider`: `Center_Divider_White`
  (1559055) — Optimizer-validated structural pick (FR-C26).**
  After CL-77 / 80 / 81 nailed down the chrome stack, the
  remaining open chrome piece was the horizontal divider line.
  Owner directive 2026-05-23: *"approximate without graphic-
  picking from the owner"* — so the four candidate TiledStyles
  (`Center_Divider_White` 1559055, `HorizontalDivider_CenterGem`
  1559057, `Divider_Header_Decorative_Edges` 2151092,
  `HorizontalDivider` 478966) got a structural pattern-match
  pick on `casc-fr#38`: `Center_Divider_White` is the only WHITE
  candidate; the other three are dark-teal and would render
  invisible against the tooltip's dark backdrop. Surface:
  `ParagonTooltipChrome.Divider` (typed `AssetRef`, sno
  `1559055`) — same record-position discipline as the other
  chrome layers (no consumer-side SNO hard-coding;
  forward-proof if a future season ships a different divider).
  Resolved via `CoreToc.TryGetId(SnoGroup.UiStyle, "Center_Divider_White")`.
  Acceptance: live matrix asserts `Divider.Sno == 1559055` +
  name + `TryGet<TiledStyleDefinition>` round-trip. **FR-C26
  closes pending consumer visual-close** — the bullet glyph
  (Unicode `◆` U+25C6 procedural fallback per Optimizer accept)
  and icon bezel (deferred consumer-owned residual) are the
  FR-C7 §6-equivalent residuals; per the engine-controller
  encryption finding (Optimizer's note 2026-05-24), Phase C-
  style EXE RE for the runtime-bound bullet/bezel is permanently
  impossible. 126/126 tests green on `3.0.2.71886`. Devlog 0077.

- **CL-81 — `ParagonTooltipChrome.SkillIconAtlas`: the
  `2DUI_Tooltip_Icons` inline-skill-tag icon set (FR-C26 chrome
  side, slice 3).** Recon on the FR-C26 thread surfaced
  `2DUI_Tooltip_Icons` (sno `2119840`) — a 61-frame texture
  atlas the engine composites inline in tooltip BODY prose
  (Druid mark, Demonform goat, Demonology / Hellfire / Abyss /
  Archfiend skill marks, etc.) wherever the
  `{c_important}{u}…{/u}{/c}` keyword tokens appear in glyph
  affix description templates (FR-C24). Not chrome in the
  strict panel-layout sense — surfaced on `ParagonTooltipChrome`
  alongside the chrome layers because it's the consumer's
  sibling resource when rendering glyph-affix bodies. Resolved
  via `CoreToc.TryGetId(SnoGroup.Texture, "2DUI_Tooltip_Icons")`
  + `AssetProviders.AtlasRef` (the existing
  `AssetKind.TextureAtlas` shape — no new asset kind needed).
  The 61 individual frame handles + UVs are accessed via the
  existing `Catalog.TryGet<TextureDefinition>` decode path
  (`td.Frames`). The semantic keyword→handle mapping (which
  frame is "Demonology", etc.) is engine-coded; the library
  surfaces the atlas, the consumer calibrates the mapping.
  Acceptance: live matrix asserts `SkillIconAtlas.Sno == 2119840`
  + name + `TryGet<TextureDefinition>` round-trip with
  `Frames.Count == 61`. 126/126 tests green on `3.0.2.71886`.
  Devlog 0076.

- **CL-80 — `ParagonTooltipChrome` multi-layer composite — base /
  rarity / ornate-frame / variants (FR-C26 recon shipped chrome
  side).** The CL-77 chrome surface was only the per-rarity panel
  — one layer of what is actually a multi-layer engine composite.
  Recon on `casc-fr#38` confirmed the engine stacks (1) a
  universal `TooltipBaseBackground` (sno 602266) — the dark
  backdrop, atlas `2DUI_TooltipBaseBackground` 602265; (2) a
  per-rarity `TooltipBackgroundRarity_<R>` overlay — the
  CL-77 layer, atlas `2DUI_TooltipBackgroundRarity_<R>`; (3) a
  universal ornate spiky border via `TooltipFrame` (sno 602013) or
  `TooltipFrameLight` (sno 603057), atlas
  `2DUITiled_TooltipFrame` 369421's 8 corner+edge frames at
  143×143/144 with the centre coming from
  `2DUI_BackgroundSquares` (sno 141461, handle `0xD756FD92`). The
  smaller `DefaultTooltip` (sno 478952) and `TextTooltip`
  (sno 478948) live in the same atlas as alternative compact
  variants (9-slice at y≥1161, 28×28 corners). Plus
  `TooltipBanner_Map` (sno 734179) and `TooltipBanner_Town`
  (sno 967402) for non-tooltip banner placements. Surface:
  `ParagonTooltipChrome` extended with `BaseLayer`,
  `OrnateFrame`, `OrnateFrameLight`, `DefaultFrame`, `TextFrame`,
  `BannerByPlacement`. Each new `AssetRef` decodes via the
  existing `Catalog.TryGet<TiledStyleDefinition>` path
  (verified in the live matrix). Visually confirmed via
  `build/AtlasExport frame` extraction on the 8 perimeter pieces
  of `2DUITiled_TooltipFrame` (artifacts at
  `artifacts/fr-c26-tooltip-recon/`): 4 corners + 4 edges of a
  clean dark-teal ornate spiky 9-slice (centre transparent,
  rendered with `0xD756FD92`). The Optimizer's "red+blue spikes"
  description matches the **per-rarity overlay** colors layered
  ON TOP of the universal dark-teal frame. Still NOT located in
  data: bullet glyph / divider / icon bezel — those need a
  separate recon pass with owner visual-close on screenshot
  candidates (next task on `casc-fr#38`). 126/126 tests green on
  `3.0.2.71886`. Devlog 0075.

- **CL-79 — ParagonGlyph + GlyphAffix sibling-StringList projection
  (FR-C24, slice 1 of N).** The Optimizer's `casc-fr#36` asks for
  the full glyph + glyph-affix display projection — eleven new
  fields total — so the App can render glyph tooltips with zero
  Maxroll fallback. This CL ships the **sibling-StringList slice**:
  `ParagonGlyphDefinition.LocalizedTitle` (via
  `Item_ParagonGlyph_<SnoName>`, label `Name`, with the universal
  <c>"Glyph: "</c> prefix stripped library-side so the consumer
  gets the bare title — `"Guzzler"` not `"Glyph: Guzzler"`);
  `ParagonGlyphDefinition.Rarity` (from the SnoName leading-token
  convention — every glyph on the live build is
  `Rare_<NN>_<Stat>_<Slot>`, forward-looking for Magic / Legendary
  if the engine adds them); `ParagonGlyphAffixDefinition.Description`
  (via sibling `ParagonGlyphAffix_<SnoName>`, label `Desc`, raw
  template — color tags / underline tags / `[{GlyphAffixScalar}|…|]`
  value placeholders all preserved for the consumer's renderer).
  New storage overloads `ReadParagonGlyph(int, locale)` /
  `ReadParagonGlyphAffix(int, locale)` (the no-locale overloads
  forward to the default `enUS`). **Honest partial — five fields
  remain deferred**: `BaseRadius` / `RadiusUpgradeLevels` /
  `MaxLevel` on the glyph (need byte-layout RE of `nStartingSize`
  / `arSizeUpgradeLevels` / `nMaxLevel` fields); `DisplayFactor` /
  `AffectedAttributes` / `SkillTagSelector` / `Requirements` on the
  affix (need byte-layout RE — the variable-length `Requirements`
  list especially). Per protocol §3 the structural-fields slice
  will split into a fresh issue if the deferred fields turn out
  larger than expected; the Optimizer can drive priority via the
  delivery counter-round. Acceptance: live anchor on the
  Optimizer's Warlock-21 row 13 — `Rare_011_Intelligence_Side`
  (sno 1023194) → `"Guzzler"` with `Rarity == Rare`; the glyph's
  affix 1068542 carries a non-empty `Description` containing the
  `[{GlyphAffixScalar}` template marker. 126/126 tests green on
  `3.0.2.71886`. Devlog 0074.

- **CL-78 — `Diablo4Storage.GetAttributeName(int, locale)` from
  `AttributeDescriptions` (sno 4080); retires the CL-76 basic-four
  hardcode in `ParagonNodeStat.StatName` (FR-C25).** The eventual
  canonical path called out in the CL-69 honesty note. Pipeline:
  `AttributeId → label key` (clean-room curated map in
  `AttributeNames.LabelByAttributeId`, ~40 entries covering every
  `attrmap`-observed id and every Optimizer FR-C25 anchor case)
  `→ AttributeDescriptions template` (sno 4080 via existing
  per-locale StringList machinery) `→ stripped name`
  (`[{VALUE…|…|}]` placeholders, standalone `{VALUE…}`/`{c_*}`
  tags, orphan `+`/`-` sign chars, leading bracket markup all
  removed; whitespace collapsed). Examples on build
  `3.0.2.71886`: `9 → "Strength"`, `133 → "Maximum Life"`,
  `481 → "Armor"`, `950 → "Damage to Elites"`,
  `275 → "Critical Strike Chance"`, `237 → "Cooldown Reduction"`.
  Curated map omits ambiguous ids where the stat identity lives in
  the node name (e.g. `481` returns the canonical "Armor"; the
  per-stat disambiguation — ArmorPercent / DamageReductionFromElite
  / etc. — still surfaces via `ParagonNodeInfoBuilder`'s
  node-name-token fallback). `ParagonNodeInfoBuilder.ResolveStatName`
  rewired with a new storage overload that routes through
  `GetAttributeName` first; the CL-76 hardcoded basic-four kept as
  a defensive offline fallback (synthetic-test path + locale-bundle
  missing). Unmapped ids and missing locale bundles surface as
  `null` from `GetAttributeName` (honest sentinel), then fall
  through to the token / `"Attribute &lt;id&gt;"` chain.
  Acceptance: 8 Theory cases on `AttributeNames.StripTemplate`
  (anchor templates from sno 4080 + the orphan-sign cleanup case);
  13 SkippableFact-attached Theory cases on
  `Diablo4Storage.GetAttributeName` against the Optimizer's anchor
  ids (basic-four + 133/481/950 + 6 more); honest-null on an
  unmapped id. 126/126 tests green on `3.0.2.71886`. Devlog 0073.
  Surface: `AttributeNames.LabelByAttributeId` (public — the
  curated map, inspectable);
  `AttributeNames.StripTemplate(string)` (public — the helper);
  `Diablo4Storage.GetAttributeName(int, locale)` (public);
  `Diablo4Storage.AttributeDescriptionsSno = 4080` (public const).

- **CL-77 — `Catalog.GetParagonTooltipChrome()` (FR-C23 Option A,
  chrome inventory).** Optimizer-confirmed scope after the #35
  recon (Option A — chrome-only — chosen; full layout RE split off
  as FR-C26 / `casc-fr#38` per protocol §3). The engine authors
  eight `TooltipBackgroundRarity_*` 9-slice TiledStyles
  (`TiledWindowPieces` variant, FR-C14 R10 / CL-62 shape) — four
  paragon-relevant rarities (Common / Magic / Rare / Legendary,
  SNOs 602975 / 602972 / 602274 / 602942) plus four item-side
  (Unique / Set / Mythic / Season, SNOs 602974 / 602973 / 2004596 /
  2417490). Surface: `ParagonTooltipChrome(PanelByRarity,
  ItemSidePanelByRarityName)`. `PanelByRarity` is the paragon
  consumer's primary map keyed by `ParagonRarity`; the item-side
  dictionary is future-proofing keyed by the engine string token
  (paragon nodes don't carry Unique / Set / Mythic / Season
  rarities, so they can't share the same key type). Resolved via
  `CoreToc.TryGetId(SnoGroup.UiStyle, "TooltipBackgroundRarity_*")`
  — **decode-free**; the consumer renders the 9-slice via the
  existing `ReadTiledStyle` / `Catalog.TryGet<TiledStyleDefinition>`
  path. Catalog-cached for the storage lifetime (identity round-
  trip verified — repeat call returns the same reference). Bullet
  glyph / divider line / icon bezel + slot rects + typography +
  per-state binding all live on FR-C26 (the controller-RE thread).
  Acceptance: live matrix asserts all 8 panels resolve to the right
  TiledStyle SNOs by name + decode round-trips through
  `TryGet<TiledStyleDefinition>` + cache identity holds. 104/104
  tests green on `3.0.2.71886`. Devlog 0072.

- **CL-76 — `ParagonNodeStat.StatName` prefers the canonical
  AttributeId map over the node-name token (FR-C21 multi-row
  defect fix).** Optimizer CL-74 consume-verify caught a defect:
  every Gate-stat row returned `StatName == "Gate"` because the
  CL-69 builder fed the same shared node-name token to every row.
  Single-stat nodes like `Generic_Magic_Armor` always worked
  (token "Armor" → "Armor"); multi-stat nodes like `Generic_Gate`
  (which carries 4 attribute rows with AttributeIds 9 / 10 / 11
  / 12 sharing the token "Gate") didn't. **Fix:** make the
  canonical AttributeId map the **primary** source — for the
  basic-four (9 → "Strength", 10 → "Intelligence",
  11 → "Willpower", 12 → "Dexterity") the id IS the stat
  identity, so use it directly. The node-name token stays as the
  **fallback** for budget-category attributes where the stat
  identity lives in the name (`AttributeId 481` — Armor /
  ArmorPercent / DamageReductionFromElite share the id; the
  token disambiguates). Unchanged behaviour for every previously-
  green node — single-row Generic_Normal_{Str,Int,Will,Dex} get
  the same canonical names from the id-map that the token-map
  used to produce. `AttributeDescriptions` (sno `4080`)
  integration is the eventual canonical path for every id;
  today the map covers only the basic-four (the Optimizer's
  accepted minimum scope for the Gate fix). Acceptance: 12
  Theory cases (basic-four × 2 paths × token shapes; budget-
  category 481 with two tokens; class-specific honest-fallback)
  plus the live Gate matrix tightened to assert per-row
  `StatName == "Strength"/"Intelligence"/"Willpower"/"Dexterity"`.
  104/104 tests green on `3.0.2.71886`. Devlog 0071.

- **CL-75 — `ParagonNodeInfo.LocalizedTitle` from the
  `ParagonNode_<SnoName>` sibling StringList (FR-C22).** Engine
  authors a per-node title StringList for every node that has its
  own tooltip header — Start nodes (`StartNodeBarb` etc.) →
  "Paragon Starting Node", Gate (`Generic_Gate`) →
  "Board Attachment Gate", class-specific rares
  (`Warlock_Rare_006` → "Binding"), named legendary nodes. The
  generic `Generic_<Rarity>_<Token>` stat-node family
  (`Generic_Magic_DamageToElite` etc.) has no sibling and surfaces
  as `string.Empty` — the consumer composes their label from
  `Stats`/`Kind`/`Rarity`. Resolution via the §6.7 sibling-
  StringList convention (the same pattern used for board
  names / power names / affix descs, CL-15 / CL-20 / CL-22).
  Surface: new `Diablo4Storage.TryReadParagonNodeTitle(int sno, out
  string name, locale)` (low-level, mirrors
  `TryReadParagonBoardName`); `ParagonNodeInfo.LocalizedTitle`
  populated by `ParagonNodeInfoBuilder`. Per the Optimizer's
  precedent note on the FR-C22 thread, this is the same
  (data-mine token, localized projection) pair already shipped on
  `ParagonBoard.Name` (CL-15) and `ParagonNodeInfo.PassivePowerName`
  (CL-69) — natural completion of the localized-display pair on
  the projection. Acceptance: live `GetNodeInfo` returns the
  engine-displayed title for Gate (994337 → "Board Attachment
  Gate"), Start (`StartNodeBarb` 830650 → "Paragon Starting Node"),
  and a class-specific rare (`Warlock_Rare_006` 2451111 →
  "Binding"); empty for `Generic_Magic_Armor` (671247) — the
  honest sentinel. `TryReadParagonNodeTitle` low-level surface
  exposed symmetrically (returns false + `string.Empty` on the
  no-sibling case). 92/92 tests green on `3.0.2.71886`. Devlog
  0070.

- **CL-74 — Gate (`Board Attachment Gate`) node stats projection
  fix (FR-C21 game-oracle correction).** Owner in-game observation
  (relayed via the Optimizer 2026-05-23): the node-kind I'd been
  calling "gate" — engine string `Board Attachment Gate` — grants
  **+5 to each of the four basic stats** (Strength / Intelligence /
  Willpower / Dexterity = `AttributeId` 9 / 10 / 11 / 12,
  `FlatValue 5`, `Unit Flat`). CL-69's bucketing dropped these — I
  conflated Gate with Start / Socket as "structural-only" when the
  ptAttributes array is in fact populated (sampled
  `Generic_Gate` 994337 carries `dataSize == 352 == 4 * 88` on
  `ptAttributes@32`, four full attribute specifiers). Fix:
  `ParagonNodeInfoBuilder` drops Stats only for
  `ParagonNodeKind.Start`/`Socket`; `Gate` now flows through
  `BuildStats` like any other kind. `IsGate` still carries the
  structural meaning (per the Optimizer's note,
  *"the `IsGate` flag stays — it's a true structural marker"*).
  Cross-link: the user-facing string in
  <c>AttributeDescriptions</c> / scene strings is
  *"Board Attachment Gate"*; the library's internal vocabulary
  (`IsGate`, `ParagonNodeKind.Gate`) is unchanged for
  back-compat, but the XML docs now cite the engine term.
  Acceptance: live `GetNodeInfo(994337)` returns `Kind = Gate`,
  `IsGate = true`, `Stats.Count == 4` keyed to `AttributeIds`
  `{9, 10, 11, 12}`, each `(FlatValue 5.0, Unit Flat)`.
  92/92 tests green on `3.0.2.71886`. Devlog 0069.

- **CL-73 — Item type/rarity/class facets from the engine's
  first-party item-naming convention (FR-C20 #32 deferred extra 3 —
  the last of three).** The Optimizer flagged that
  `Catalog.Find(Item)` didn't carry the localized-name composition
  data. Three patterns observed across the live build's group-73
  items: weapons + armor
  (`<Type>_<Rarity>_<Class>_<NN>[_<Variant>]` —
  `1HAxe_Unique_Druid_100`, `Helm_Rare_Barb_Crafted_47`); cosmetics
  (`Cosmetic_<Class>_<Name>` — `Cosmetic_Barbarian_*`, dominant by
  count); classless (`<Type>_<Rarity>_Generic_<NN>` —
  `1HAxe_Magic_Generic_001`). The first underscore-bounded token
  always serves as the type; rarity emits only when the second
  token is in the closed set (`Normal | Magic | Rare | Legendary |
  Unique | Any`) — that gate keeps non-rarity tokens like
  `Cosmetic`/`Charm`/`Journey`/`MSWK` (which occupy the same slot
  in non-weapon/armor patterns) from leaking into the rarity facet
  (they already surface as type). Class tokens are normalized to
  the canonical PlayerClass SnoName so both the abbreviated authored
  forms (`Barb`, `Sorc`, `Necro`) and the full forms (`Barbarian`,
  `Sorcerer`, `Necromancer`) collapse to a single facet value;
  `Generic` is the engine's "no class" sentinel and produces no
  class facet on purpose. Surface: `Catalog.Facets(itemRef)` now
  emits up to three facets (`type:<T>`, `rarity:<R>`,
  `class:<SnoName>`) with `FacetSource.NameConvention`;
  `Catalog.FindByFacet(Item, "class", "Druid")`
  / `FindByFacet(Item, "rarity", "Unique")` /
  `FindByFacet(Item, "type", "1HAxe")` all light up. Internal helper
  `Catalog.ParseItemConvention(string)` (static, alias-aware) for
  unit-testing the dispatch. Acceptance: 9 known item-name cases
  cover all three patterns + the `Generic` sentinel + the cosmetic
  case + fallbacks (empty, no-underscore, unrecognized leading
  token), plus a live `FindByFacet(Item, "class", "Druid")`
  round-trip that confirms each result name contains `_Druid`.
  92/92 tests green on `3.0.2.71886`. Devlog 0068. **#32 backlog
  complete** — three deferred extras (codec tail, power → class,
  item NameConvention) all resolved.

- **CL-72 — Power → class facet from the
  `<ClassSnoName>_<SkillName>` name convention (FR-C20 #32 deferred
  extra 2).** Owner directed CASC to investigate the
  power-class linkage; the earlier "no cheap source" note (CL-59)
  was a blanket claim that overlooked the engine's first-party
  naming convention. Across the live build's ~2,500 group-29
  power SNOs, ~1,700 active class-skill powers carry the prefix
  (`Barbarian_*` 128, `Sorcerer_*` 153, `Druid_*` 295,
  `Rogue_*` 262, `Necromancer_*` 153, `Paladin_*` 240,
  `Warlock_*` 200, `Spiritborn_*` 274) — the same eight SnoNames
  that anchor §6.5's PlayerClass roster. The dispatch matches the
  power name's first-underscore prefix against the cached
  CoreTOC-resolved SnoName roster (`PlayerClass` group), so it is
  **decode-free** (no `PowerDefinition` parse needed) and honest
  about partial coverage: monster powers (`MorluCaster_Fireball`),
  item-affix powers (`1HAxe_Unique_Druid_100`), unnamed debug
  stubs, and any power whose first token isn't a class SnoName stay
  unfaceted. Surface: `Catalog.Facets(power)` now returns
  `class:<SnoName>` with `FacetSource.NameConvention`;
  `Catalog.FindByFacet(AssetKind.Power, "class", "Sorcerer")` yields
  every matching skill power. New internal helper
  `Catalog.TryGetPowerClassFromName(string)` for unit-testing the
  dispatch. Acceptance: a handful of well-known class-skill powers
  (`Barbarian_Bash`, `Barbarian_Whirlwind`,
  `Necromancer_BloodLance`, `Sorcerer_Fireball`) resolve correctly,
  non-class names resolve to `null`, and `FindByFacet` round-trips
  the inverse — every Power tagged `class=Sorcerer` starts with
  `"Sorcerer_"`. 92/92 tests green on `3.0.2.71886`. Devlog 0067.
  **#32 extra 2 of 3 resolved**; one extra remaining (item
  `NameConvention` facets).

- **CL-71 — `TexFrame` inner UV decode (FR-C20 #32 codec-tail
  investigation).** Owner-directed RE of the texture combined-meta
  decode tail (one of the three deferred extras called out on
  `casc-fr#32`). Each on-disk frame is 36 bytes; CL-55 surfaced the
  first 20 (handle + outer UV rect at `+4..+19`). The trailing 16
  bytes at `+20..+35` are a **second UV rect** — the inner /
  9-slice-middle rect. Validated across the live build's 140,197
  texture definitions via the new `SnoScan framescan`/`framediv`
  recon: ~83 % of frames author the inner rect equal to the outer
  rect (no trim — outer is the content), ~17 % (24,082 atlases)
  author a genuinely inset inner rect (sprite trim for tighter UV
  sampling, or a 9-slice middle for stretchable UI tiles), and a
  small remainder author a degenerate point at the outer rect's TL
  (the "no inner rect" sentinel). Surface:
  `TexFrame.InnerU0/InnerV0/InnerU1/InnerV1`,
  `TexFrame.InnerPixelRect(width, height)`,
  `TexFrame.HasDistinctInner`. The existing `U0`/`V0`/`U1`/`V1` +
  `PixelRect` stays the outer rect (the unchanged public semantic).
  Acceptance: live matrix scans the combined-meta and asserts at
  least one frame exposes `HasDistinctInner == true`; every frame's
  `InnerPixelRect` stays non-empty (the degenerate cases floor
  width/height at 1). 92/92 tests green on `3.0.2.71886`. Devlog
  0066. **#32 codec-tail extra resolved**; the other two extras
  (power → class facet, item NameConvention facets) tracked
  separately.

- **CL-70 — `Catalog.GetBoardNodes` hot path + `EnumerateNodes`
  (FR-C21, third build slice).** The consumer-facing batch API the
  Optimizer's multi-board B&B search hits in its inner loop, and the
  global enumerator for catalog scans. `GetBoardNodes(int boardSno)`
  walks the authored 21×21 grid row-major (row 0 = top, col 0 = left;
  empty cells skipped) and pairs each placed node SNO with its
  `ParagonGridCell(Row, Col)` and the resolved
  `ParagonNodeInfo` (§7.7). Three caches converge on the hot path:
  the board def (`ConcurrentDictionary<int, ParagonBoardDefinition?>`),
  the per-cell infos (the §7.7 SNO-keyed cache), and the projected
  `(cell, info)` list **per board SNO** — repeat queries return the
  same list reference (verified by `Assert.Same` on the live matrix;
  the optimizer's perf contract). Missing/undecodable boards memoize
  as an empty list. `EnumerateNodes(AssetQuery?)` streams every
  paragon node through the same SNO-keyed cache (lazy); the query's
  `Kind`/`Kinds` are overridden to `ParagonNode`, other facets
  (`NameContains`, `Where`, `OrderByName`, …) apply as usual.
  Surface: `Catalog.GetBoardNodes`, `Catalog.EnumerateNodes`,
  `ParagonGridCell`. Acceptance lives on the existing live matrix:
  `GetBoardNodes(2458674)` returns `>60 and <441` placements (the
  Warlock_00 oracle), every pair carries in-range coordinates +
  a non-null info, cache-identity holds, distinct-def count lands in
  the Optimizer's `~17–21` band; `EnumerateNodes()` streams non-empty
  samples; `EnumerateNodes(new AssetQuery { NameContains = "..." })`
  honours the filter. 92/92 tests green on `3.0.2.71886`. Spec §7.8,
  devlog 0065. **FR-C21 consumer-signed-off backlog now complete**
  (CL-68/69/70) — the Optimizer can consume `GetBoardNodes` /
  `GetNodeInfo` / `EnumerateNodes` end-to-end; awaits their
  verification before sign-off.

- **CL-69 — `ParagonNodeInfo` projection + `Catalog.GetNodeInfo` +
  SNO-keyed decode cache (FR-C21, second build slice).** The public
  surface the Optimizer renders from. Builds on CL-68's magnitude
  evaluator: each `ParagonNodeStat` carries the formula-resolved
  `FlatValue` + an inferred `Unit` (`Flat | Percent | Multiplier`)
  alongside the raw `(AttributeId, Variant)` and a humanized
  `StatName` derived clean-room from the
  `Generic_<Rarity>_<Token>` node-name convention (CamelCase split +
  small abbreviation table; class-specific names without the
  `Generic_` prefix fall back to `"Attribute <id>"`). The node-level
  shape carries the visual archetype as `Kind`
  (`ParagonNodeKind`: `Normal | Magic | Rare | Legendary | Start |
  Socket | Gate`) — a distinct axis from `Rarity` (the raw
  `eRarityOverride`); `Stats` is empty for `Start`/`Socket` (revised
  in CL-74 — `Gate` does carry stats; see Appendix A CL-74).
  Catalog refs for the icon atlases (`Icon`, `IconMask`) and the
  granted passive power (`PassivePower`, with the sibling-StringList
  localized name on `PassivePowerName`) are pre-resolved.
  **Caching:** `ConcurrentDictionary<int, ParagonNodeDefinition>` and
  `ConcurrentDictionary<int, ParagonNodeInfo>` on `Catalog`; the
  ~1 MB `AttributeFormulaTable` (sno `201912`) is read once and
  held; missing/undecodable SNOs memoize as `null`. **`Sno` is the
  canonical stat-aggregation key** (Optimizer-signed-off correction
  from `casc-fr#33`, 2026-05-23) — three nodes can share
  `(AttributeId 481, NParam 0)` while displaying three distinct
  stats. Surface: `ParagonNodeInfo`, `ParagonNodeStat`,
  `ParagonNodeKind`, `StatUnit`, `Catalog.GetNodeInfo(int sno)`.
  Acceptance: `B9_stat_name_resolves_from_node_name_token` (13
  Theory cases), `B9_stat_name_falls_back_to_attribute_id_for_non_generic_names`,
  `B9_stat_unit_inferred_from_token_and_attribute_id` (9 Theory
  cases), and the live matrix's `GetNodeInfo` round-trips against
  `Generic_Magic_Armor` (671247), `Warlock_Rare_006` (2451111),
  `Generic_Socket` (681756) — plus a cache-identity check and the
  missing-SNO ⇒ `null` memoization. 92/92 tests green on
  `3.0.2.71886`. Spec §7.7, devlog 0064. Next CL-70 —
  `Catalog.GetBoardNodes(int boardSno)` hot path with cell
  coordinates + `EnumerateNodes`.

- **CL-68 — Paragon magnitude resolution: budget-multiplier intrinsics
  + formula evaluator (FR-C21, first build slice).** The Optimizer
  signed off on the node-SNO-as-canonical-key correction +
  full-resolution scope expansion (#33, 2026-05-23), so the FR-C21 build
  begins with the part that has no upstream dependencies: the math
  that turns a node's stored formula text into the displayed
  magnitude. The six **budget multipliers** the engine intrinsics
  resolve to (`MagicDefensive=10`, `MagicOffensive=2.5`,
  `RareMajorDefensive=RareMinorDefensive=4`,
  `RareMajorOffensive=RareMinorOffensive=5`) are absent from every
  shipped GameBalance data table — they're formula-DSL intrinsics in
  engine code. Pinned empirically and baked as a clean-room
  calibration table (`ParagonPowerBudget`). The **magnitude evaluator**
  (`ParagonMagnitudeFormula.Evaluate`) is a strict subset of the
  engine's formula DSL — numeric literal, zero-arg intrinsic call,
  binary `+ - * /`, parens — built on the existing internal
  `PowerScriptFormulaEvaluator` (the FR-C13 power-script evaluator)
  with a function resolver that delegates to
  `ParagonPowerBudget.TryGetMultiplier`. Eight worked validations
  round-trip exactly to the in-game oracle (Armor `0.75 × 10 = 7.5`,
  DamageToElite `3 × 2.5 = 7.5`, AllRes `0.75 × 4 = 3.0`,
  MaxLife `1 × 4 = 4`, Damage `2 × 5 = 10`, Demonology bonus
  `3.5 × 5 = 17.5`, CritDamage `3 × 5 = 15`, Normal-rarity bare
  constant `"5" = 5`). Future-build trip wire: unknown intrinsic ⇒
  `NaN`, never a fabricated value. Library boundary update: the
  FR-C21 carve-out is now documented in Appendix C — the magnitude
  evaluator + calibration table are now in-scope for the node-info
  surface (other formula domains stay the consumer's). Acceptance:
  `B8_magnitude_formula_evaluates_to_expected_displayed_value` (8
  Theory cases), `B8_magnitude_formula_unknown_intrinsic_yields_NaN`,
  `B8_power_budget_tryget_round_trips_all_six`, and a live matrix
  assertion that decodes `Generic_Magic_Armor` (671247)'s shipped
  formula text and evaluates it back to `7.5`. 69/69 tests green on
  `3.0.2.71886`. Spec §7.6 + Appendix C boundary amendment, devlog
  0063. Next CL-69 — `ParagonNodeInfo` / `ParagonNodeStat` projection
  + `Catalog.GetNodeInfo` + decode cache.

- **CL-67 — `ParagonNodeDefinition` rare bonus mechanic (`@48`/`@64`) +
  `StatTagDefinition` (group 124) typed surface (FR-C21 deferred RE).**
  Closing the remaining `ParagonNode` field debt called out by CL-66.
  Two node-level descriptors that are populated **only on rares** were
  previously unmodelled: **`@48`** is a `DT_VARIABLEARRAY[DT_SNO]` with a
  single slot whose engine field name is unrecovered — across every rare
  sampled the value is `0`, so the surface
  (`ParagonNodeDefinition.BonusPassivePowerSno`) exposes the raw int
  with `-1` reserved for "descriptor missing" (non-rare nodes); **`@64`**
  is a `DT_VARIABLEARRAY[DT_SNO]` referencing **group 124 `StatTag`**
  records (`ParagonNodeDefinition.BonusStatTagSnoIds`) — class-specific
  rares list one tag, class-generic rares list two or three class-keyed
  alternatives. The referenced records (`StatTagDefinition`, §7.5) carry
  a formula-text descriptor at payload `@64` whose evaluation yields the
  stat threshold the bonus requires (`WillpowerMain2` →
  `"760 + (455 * ParagonBoardEquipIndex)"`, cross-validated by the
  Fathomless / `EquipIndex == 3` ⇒ `2125` Willpower in-game oracle).
  Composite-tag variants (`Barb_Strength+Dexterity`) and glyph-keyed
  variants (`Glyph_Willpower_Main`, threshold `"40"`) decode through the
  same shape. Library boundary holds — text only; evaluation belongs to
  the consumer (Appendix C). Open follow-ups: the bonus stat's identity
  + magnitude (the `+Z%` half of "Bonus: another +Z% [stat] when N
  [stat] met") — the @88 GBID array is one entry larger than
  `ptAttributes.Count` on every rare sampled, the strongest candidate
  but not yet verified; composite-tag sub-records; `ParagonBoard.payload
  +32` as the likely `ParagonBoardEquipIndex` source. Surface:
  `ParagonNodeDefinition.BonusPassivePowerSno` /
  `.BonusStatTagSnoIds`; `StatTagDefinition.ThresholdFormulaText`;
  `Diablo4Storage.ReadStatTag` / `TryReadStatTag`;
  `SnoGroup.StatTag = 124`. Acceptance:
  `B2_node_decodes_bonus_passive_and_stat_tag_arrays`,
  `B2_node_without_bonus_descriptors_returns_empty_tags_and_minus_one_power`,
  `B7_stat_tag_decodes_formula_text`,
  `B7_stat_tag_missing_descriptor_yields_empty_formula`, and the live
  matrix's `Warlock_Rare_006` / `Generic_Rare_001` / `Generic_Magic_Armor`
  assertions. 58/58 tests green on `3.0.2.71886`. Devlog 0046.

- **CL-66 — `ParagonNodeDefinition`: the two undecoded fields decoded —
  `eNodeType@16` and the per-attribute GBID array @88 (FR-C21 foundation).**
  Closing the "RE all fields of every data type we use" debt on the node
  record. `eNodeType` (payload `+16`, previously skipped) is a distinct axis
  from `eRarityOverride`: `5`=Start (verified on all seven class start boards),
  `3`=Magic, `0`=Normal/structural/gate/rare — the reliable start-node marker.
  A **second** `DT_VARIABLEARRAY[DT_UINT]` (descriptor @88, `dataOffset@+8`/
  `dataSize@+12`) holds **one GBID per `ptAttributes` element, same order**;
  it is a stable per-`eAttribute` key (`eAttribute 9` → `0x1E663884`
  everywhere) whose canonical name did not crack against any tested DJB2/GBID
  candidate, so it is surfaced raw on `NodeAttribute.AttributeGbid`. Earlier
  near-miss this confirms: the start marker is `eNodeType=5` at `+16`, **not**
  `eRarityOverride=5` (all class start nodes are `RarityOverride 0`) — reading
  `+16` is what distinguishes them. Surface: `NodeTypeRaw`/`NodeType`
  (`ParagonNodeType`)/`IsStart` + `NodeAttribute.AttributeGbid`.

- **CL-64 — `ReadNodeSelectionHighlight()`: the AUTHORED node hover recipe
  (`ContextualHighlight_Square`) + its corner art (FR-C19 #30).** Resolution of
  the CL-62/63 saga, owner-directed ("find the existing recipe, don't invent").
  The named authored recipe **is** `ContextualHighlight_Square` (TiledStyle
  2434982) — a `TiledWindowPieces` with **exactly 4 piece handles** (then zeros),
  `ImageScale` 0.5: the engine's "square contextual (hover) highlight." Its own 4
  handles are engine-internal (resolve to no texture frame — scanned all 140,197
  group-44 textures), like the #24 rim. Owner-approved pairing (a): surface
  `ContextualHighlight_Square` as the recipe with the **drawable corner art** from
  `SelectionRectangleInset`'s window-pieces (the 4 corners of
  `2DUITiled_SelectionHighlight` 585030, verified by decoding + viewing each:
  TL `0x95DA4E78`, TR `0x5192E52B`, BR `0xEA71A5AD`, BL `0xB1C206BA`).
  `ReadNodeSelectionHighlight()` → `NodeSelectionHighlight(RecipeSno, RecipeName,
  TL, TR, BR, BL)`. **Drawing recipe (owner-validated):** a hollow square border
  sized to the node perimeter — draw the **4 corners only**, each in its quadrant
  (each piece is a full quadrant, so the four meet); **no edges, no centre fill**
  (the node shows through). The earlier failures were a wrong piece→position
  mapping (row-major vs corners-CW) + drawing edges/centre. Acceptance:
  `ReadNodeSelectionHighlight_pairs_recipe_with_corner_art`. New `SnoScan
  findhandle` + `AtlasExport compose` recon. 53/53 tests green on `3.0.2.71886`.
  Devlog 0061.

- **CL-63 — RETRACT the CL-62 `TiledWindowPieces` 3×3 placement recipe; the
  composition is engine-side, not in the record (FR-C19 #30).** The consumer
  composed CL-62's 9 pieces per the stated "corners at native, edges stretched,
  centre fill" recipe and got **9 overlapping brackets, not one border** — the
  pieces are **quadrant/edge brackets** (e.g. `0x95DA4E78` is a full top+left
  quadrant), not pre-cut 3×3 thirds. Verified by extracting + compositing the
  pieces three ways (zone-anchored / full-cell-overlay / corners-only) — all
  produce a mess. And the record **ends at ~`0x98`**: after the 9 handles it
  carries only `ImageScale` (0.6), `nPadding`, and 3 tile flags `(0,1,1)` at
  `+0x88/+0x8C/+0x90` — **no per-piece crop/placement geometry.** So the exact
  composition (crop/anchor/blend) is engine UI code, not recoverable from the
  data — same class as the CL-53/#24 fire-rim (a mesh/material/VFX effect, not a
  data-authored frame). `WindowPieces` stays correct (the authored handle set);
  its doc no longer asserts a placement recipe. **Resolution is owner-gated**
  (#30, `needs:owner`): calibrate the composition against the live render, or
  accept a procedural selection border (the #24 precedent). The
  `build/AtlasExport compose` mode is the calibration harness.

- **CL-62 — `TiledWindowPieces` 9-slice fully decoded (FR-C19 #30 / FR-C14
  R10).** Owner visual-close: the stretched-source selection highlight rendered
  only a top-left corner. Root cause: `SelectionRectangleInset` (585031) is the
  **`TiledWindowPieces`** variant (typeTag `0x02E46583`), not NSlice — so its
  composition was never decoded (`partial=True`, no usable insets). Finding: the
  variant stores its **9-slice as 9 explicit piece handles at +0x60..+0x80,
  row-major 3×3** `[TL,T,TR,L,C,R,BL,B,BR]` — verified: index 4 (`0xD756FD92`)
  resolves to the 100² centre fill (atlas `2DUI_BackgroundSquares`), the other 8
  to the 64² corner/edge slices in `2DUITiled_SelectionHighlight` (585030). New
  `TiledStyleDefinition.WindowPieces` (the 9 handles) surfaces them; decoding
  them clears `HasPartialDecode` for this variant. The consumer composes a true
  9-slice (corners at native × `ImageScale`, edges stretched between, centre
  filling) instead of stretching one frame. Acceptance:
  `ReadTiledStyle_decodes_TiledWindowPieces_9slice` (9 pieces, [0]=`SourceImageHandle`,
  [4]=centre, all resolve). 52/52 tests green on `3.0.2.71886`. Devlog 0060.

- **CL-61 — Starter node base disc renders disk-sized, not full-cell (FR-C12
  #22).** Owner visual-close: the Start node's disk + filigree read oversized.
  Root cause (the FR-C18 oversize class, on the Starter template): the Starter
  base `0xF8312CA8` has an **all-zero authored rect**, which `ResolvePlacement`
  stretched to **full-cell 100²**, vs every other node's base disc at the
  canonical **inset-7 86²** (`Node_IconBase`). Fix (general, not a patch): a
  template base child whose rect is unspecified (all-zero) inherits the
  **base-disc inset** (the rarity pair's co-sized inset where present, else
  `Node_IconBase`'s inset → 86²) — never full-cell. Explicitly-sized children
  keep their own rect (the Starter filigree's authored 140² overscan, the gate
  ornate's inset-3), so the OK-looking gate/socket don't regress (their handle
  children are all non-empty). Native-size check confirmed rendered size comes
  from the authored rect, not the frame's native resolution. Acceptance:
  `ReadParagonNodeRecipe_…` asserts the Starter base at inset-7/86². 51/51 tests
  green on `3.0.2.71886`. Devlog 0059.

- **CL-60 — `Catalog` authored relationship traversal (FR-C20 P5).** The last
  consensus item: `Catalog.Related(AssetRef)` → `IReadOnlyList<AssetLink>` where
  `AssetLink(Role, AssetRef Target)` makes **board → node → power** and **glyph
  → affix / class** first-class instead of hand-assembled (each `Target` is a
  full `AssetRef`, so traversal chains). Authored FK edges only, from the
  decoded definitions:
  - `ParagonBoard` → `node` (distinct `Cells`).
  - `ParagonNode` → `power` (`SnoPassivePower`, when **> 0** — the legendary-node
    passive; negative/zero is the no-power sentinel).
  - `ParagonGlyph` → `affix` (`AffixSnoIds`) + `class` (`UsableByClassSnoIds`).
  A socket node ↔ glyph is **runtime** slotting, not an authored FK, so it is
  deliberately not a link — find candidate glyphs for a class via `FindByFacet`.
  All target SNO ids filtered `> 0` (drops sentinels). Acceptance:
  `Catalog_discovers_…` extended (board→node→power chain; glyph→affix/class).
  51/51 tests green on `3.0.2.71886`. Devlog 0058. **Closes the FR-C20 consensus
  backlog** (power→class deferred — no consumer need).

- **CL-59 — `Catalog` provenance-marked categorical facets (FR-C20 P2b, marked-A).**
  Per the consumer's "A now + B upgrade" decision: `Catalog.Facets(AssetRef)` →
  `IReadOnlyList<Facet>` where `Facet(Key, Value, FacetSource)` carries
  provenance (`NameConvention` / `Decoded` / `SceneField`, mirroring
  `NodeActivationSource`); `Catalog.FindByFacet(kind, key, value)` filters by it.
  Shipped facets:
  - **`ParagonGlyph` → `class`** (one per usable class), `Decoded` from
    `ParagonGlyphDefinition.UsableByClassSnoIds` → `PlayerClass` name (the
    consumer's prioritised facet; glyphs filter by class).
  - **`TextureAtlas` → `codec`**, `Decoded` (decode-free meta).
  - **`Power` → `class`: no cheap source** — `PowerDefinition` carries no class,
    `PlayerClass` carries no power list, and power names don't encode class
    (most of the 9,781 are engine/AI powers). Not faked; needs a skill/balance
    table RE (raised on #32). **Item** type/rarity/class (`NameConvention` from
    `<Type>_<Rarity>_<Class>` SNO names) deferred — consumer-deprioritised
    (items ≠ paragon critical path).
  Acceptance: `Catalog_discovers_…` extended (glyph class = `Decoded`;
  `FindByFacet`). 51/51 tests green on `3.0.2.71886`. Devlog 0057.

- **CL-58 — `Catalog` query ergonomics: `DecodableOnly` + `OrderByName` (FR-C20
  Q2); `AssetRef` identity/stability documented (Q4).** `AssetQuery.DecodableOnly`
  yields only assets that decode (drops the "Bad Data" board; cost = a decode
  per asset — `Find<T>` remains the lazy decodable-only path). `AssetQuery.OrderByName`
  orders by (Kind, ordinal Name) (buffers; not lazy). Q4: documented that
  `(Group, Sno)` is the canonical key, build-stable *within a build* (diff bakes
  against `.build.info`), with `Name` the most patch-durable identity
  (re-resolve via `TryResolve`). Acceptance: `Catalog_discovers_…` extended.
  51/51 tests green on `3.0.2.71886`. (P2b item/power/glyph facets remain
  pending the consumer's name-convention-vs-decode-fields A/B call on #32.)

- **CL-57 — `Catalog` iteration 2: frame-pixel retrieval (FR-C20 P3) + a
  TexFrame-from-handle convenience.** The Optimizer consume-verified CL-56
  cleanly and named P3 its critical path (it unblocks the #30 selection-cursor
  dogfood AND folds in the #31 atlas browser). Shipped:
  - **P3 `TryGetFrameImage(uint handle, out DecodedImage)`** — decode the single
    frame a handle names, cropped from its owning atlas mip0; and
    **`TryGetAtlasImage(AssetRef, out DecodedImage)`** — the whole atlas mip0
    (decode-once for the browser; crop frames via `Frames[i].PixelRect`).
    Exception-safe: unsupported codec (only BC1/BC3 decode), absent payload, or
    non-atlas ref → `false`.
  - **`TryResolveFrame(uint handle, out AssetRef atlas, out TexFrame frame)`** —
    the requested convenience: the handle's `TexFrame` (UV rect) directly,
    saving the `TryGet(atlas)`+`Frames[i]` step.
  Completes the **discover → peek → retrieve-pixels** browse loop (FR-T1 #31).
  Acceptance: `Catalog_discovers_…` extended (frame/atlas decode, RGBA length,
  TexFrame round-trip). 51/51 tests green on `3.0.2.71886`. Devlog 0056.

- **CL-56 — `Catalog` iteration 1 from consumer feedback: handle reverse-lookup
  + decode-free atlas facets + typed enumerator (FR-C20 P1/P2/P4).** The
  Optimizer consume-tested CL-55 (34,268 assets / 14 kinds) and prioritised
  gaps. Shipped:
  - **P1 `TryResolveHandle(handle, out AssetRef atlas, out int frameIndex)`** —
    reverse a raw texture handle to its owning `TextureAtlas` asset + frame
    index (over `TryGetIconFrame`). Sentinel `0`/`0xFFFFFFFF` → `false`. Retires
    the recurring "what is this handle?" round-trip.
  - **P2 `TryPeek(ref, out AssetFacets)`** — decode-free atlas facets
    (`Width/Height/FrameCount/Codec`) from the preloaded combined-meta, plus a
    filterable `codec:<codec>` tag on every `TextureAtlas` ref — filter the 4.7k
    atlases without decoding pixels. (Item/power/glyph categorical facets are
    deferred to P2b pending a cheap authored source.)
  - **P4 `Find<T>(query)`** — typed lazy enumerator yielding decoded `T`,
    skipping non-matching kinds and undecodable blobs.
  Acceptance: `Catalog_discovers_and_retrieves_assets_by_kind_filter` extended.
  51/51 tests green on `3.0.2.71886`. Devlog 0055.

- **CL-55 — `Catalog` asset discovery/retrieval API (`d4.Catalog`, FR-C20).**
  A facade so the consumer can **find / enumerate (filtered) / retrieve** any
  catalogued recipe or definition without hardcoding SNO ids/names or knowing
  which typed reader to call — replacing the "one bespoke `ReadX()` per thing"
  surface for discovery. Shape:
  - `Find(AssetQuery)` → lazy `AssetRef(Kind, Group, Sno, Name, Tags)` stream;
    `OfKind`, `TryResolve(kind,name)`. `AssetQuery` filters by kind/kinds, name
    substring, tag(s), `RenderRecipesOnly`, or a `Where` predicate.
  - `TryGet(ref, out object)` / `TryGet<T>(ref, out T)` → the **real** decoded
    type (e.g. `TiledStyleDefinition`, `ItemDefinition`) — pattern-match it or
    ask for `T`. No closed wrapper union, so a new family is one provider +
    one `AssetKind`, zero facade edits (future-proofs to weapons/armor/etc.;
    `Item`/`Affix` seeded). Exception-safe: a malformed/sentinel blob (e.g. the
    leading "Bad Data" board) yields `false`, never throws.
  - Backed by an internal `IAssetProvider` registry (one per kind), each
    delegating decode to the existing typed reader. Kinds: render recipes
    (`ParagonNodeRender`, `TiledStyle`, `SelectionHighlight`,
    `ParagonBoardGrid`, `TextureAtlas`) + paragon domain (`ParagonBoard/Node/
    Glyph/GlyphAffix`, `Power`, `PlayerClass`, `AttributeFormulas`) + broader
    (`Item`, `Affix`).
  - **Folds in CL-53** (the standalone selection-highlight surface): the
    `SelectionHighlight` provider discovers the shape-tagged selection
    TiledStyles; `ReadSelectionHighlight()` is now a typed shortcut over the
    Catalog. PR #39 (CL-53 standalone) is **superseded — closed unmerged**.
  Acceptance: `Catalog_discovers_and_retrieves_assets_by_kind_filter`. 51/51
  tests green on `3.0.2.71886`. Devlog 0054.

- **CL-53 — typed `ReadSelectionHighlight()` → authored selection-highlight
  TiledStyle recipes (FR-C19).** Selection highlight is authored as named
  `TiledStyle` 9-slice recipes (group 103) over `2DUI_SelectionHighlight`
  (337357) / `2DUITiled_SelectionHighlight` (585030):
  `SelectionRectangleInset` (square node) + `ControllerSelection{Rectangle,
  Circle,Diamond,TearDrop,APS}`. Shape comes from the authored TiledStyle name
  (corrects the eyeballed delivery: `0xBA7D2638`=TearDrop not "circle";
  `0x0BD8A829`=Circle not "diamond" — a `no-atlas-name-jumps` catch).
  **Folded into CL-55 (`Catalog`)** rather than shipped standalone; the
  surface (`SelectionHighlight`/`SelectionHighlightStyle`/`SelectionShape` +
  `ReadSelectionHighlight()`) is unchanged.

- **CL-54 — the SOCKET node's on-board type-disc is carried by the
  `Usage_Slot_*` widgets and must be remapped into the base-disc band, like
  a rarity type-disc (FR-C16 #26.4).** `Template_Node_Socketable` is empty
  (no handle, no children) — the socket disc art lives on the equipped-glyph
  side-panel widgets `Usage_Slot_1`/`Usage_Slot_2`, whose disc frames the
  engine reuses on-board (FR-C12 / CL-34). CL-52 routed them through the
  generic non-template branch, so they emitted at the widget's high scene-z
  (above the symbol and the purchased add-on) with the side-panel layout:
  - the full-cell socket disk (`0xF6443089` + inner `0x23F487F3` + ring
    `0xBED4CF21`) **painted over the cardinal arrows and connector bars**;
  - the widget's own `hImageFrame` `0x3084D186` (12² usage-pip bead — a
    side-panel equipped-glyph indicator) leaked on-board as a stray dot.
  Fix (interpretation, not a patch): treat `Usage_Slot_*` as the
  **`KindSocket` type-disc carrier**. Emit only its handle-bearing disc
  children, remapped into the base-disc band (`Node_IconBase`'s z), gated
  `[KindSocket]` + `bActive`-finalized — so the socket disc composes below
  the symbol/arrows/connectors like any base disc. The widget's own 12²
  pip is **not** part of the on-board node and is not emitted. Disc geometry
  stays authored (centred, ~100²) — not refit. Owner-oracle validated
  (2026-05-21). Acceptance: `ReadParagonNodeRecipe_surfaces_flat_zordered_components`
  (socket disc z above base, below symbol/arrows/connectors; `0x3084D186`
  absent). Devlog 0053.

- **CL-52 — flat `ParagonNodeRecipe.Components` + `bActive`-driven
  activation; owner-oracle-validated render model (FR-C16 R14).** Replaces
  the CL-50/51 layer/disc/slot nesting with a single z-ordered list of
  atomic `ParagonNodeComponent`s (`ZOrder, Source, ImageHandle, Rect, Alpha,
  Activation, DefaultActive, Tint`). Consumer rule: draw every component
  whose `Activation.Evaluate(facts)` holds, in order, at its rect/alpha/tint
  — no consumer dispatch. Decoded/validated this round (against the owner's
  in-game screenshots):
  - **Every widget's layers are emitted** (its own `hImageFrame` + each
    handle-bearing child), not just `Template_Node_*` — recovers the
    glyph-socket base/overlay nested in `Usage_Slot_2` (general fix).
  - **`bActive` is the authored default visibility** (`bActive=1`/unbound ⇒
    drawn at rest; `bActive=0` ⇒ default-off). A `bActive=0` layer that would
    fire in the resting state with no decoded trigger ⇒ `Never` (so it can't
    mask the base, e.g. the magic interior `0xFEC31E48`).
  - **Base disc = Unpurchased↔Purchased swap** (NOT selection): the no-ring
    disc is `bActive=1` `[kind, Unpurchased]`, the red-ring/brighter disc
    `bActive=0` `[kind, Purchased]`. `Node_Purchased` is literally its role.
    New `NodeFact.Unpurchased`.
  - **Node kind is one mutually-exclusive dimension** (`Kind{Common,Magic,
    Rare,Legendary,Socket,Gate,Start}`; engine `Purchase_Node_*` enum) —
    Common is a peer rarity, not a special case.
  - **Purchased add-on**: `Arrow_<dir>` `[Purchased, NeighbourPurchasable<dir>]`,
    `Connector_<dir>` `[Purchased, NeighbourPurchased<dir>]` (new neighbour-
    purchased facts); gate/start have no purchasable neighbours (consumer
    fact logic) so draw no arrows.
  - **`rgbaTint`** (multiply, ARGB) surfaced (grey socket base) and
    **`eVerticalAnchoring`** placement resolution (centred=3 / absolute=0;
    e.g. the 120² Located ring centres at `(-10,-10)`).
  - **Hash sanity fix**: `0x093CBAA8` was mislabelled `eGroupType` — it is
    `eHorizontalAnchoring` (real `FieldHash("eGroupType")`=`0x05862894`);
    caught by the new `build/SnoScan checkfields` field-hash validator
    (54 verified, 1 mismatch).
  - **Selection highlight is NOT in the node recipe** — it's a shared
    engine-applied topmost cursor (lead: `ContextualHighlight_Square`
    TiledStyle 2434982); spun off as its own FR.
  Acceptance: `ReadParagonNodeRecipe_surfaces_flat_zordered_components` +
  `_surfaces_exact_component_activation`. 50/50 tests green on `3.0.2.71886`.
  Devlog 0051.

- **CL-51 — typed per-layer activation surface; the scene encodes state by
  NAME, not by a condition field (FR-C16 R10/R11).** The owner ruled that
  the consumer must author *no* dispatch — predicates, draw order, and
  variant selection must all come from CASC. Foundational decode of scene
  657304 (recon `recdump`/`members`/`snorefs`): the scene stores **no**
  activation/condition/visibility field, **no** binding expression in the
  `DT_BINDABLEPROPERTY` value records (full 56-byte decode = literal value +
  44 zero bytes), and **no** condition-SNO reference (its only `DT_SNO`
  field, `snoTiledStyle`, points at group-103 style SNOs). The engine binds
  a widget's `bActive` to a runtime state **by the widget's name** in its
  C++ UI controller; the data-side representation of the association is the
  **naming convention** — per-state field-name suffixes (cracked this round:
  `hImageFramePressed` `0x0D75128C`, `hImageFrameMouseOver` `0x0B63D29B`,
  `hImageFrameDisable` `0x0DAEFCAA`, `hImageFrameIcon` `0x02330CBF`,
  `hText` `0x0789C1CD`) and per-state widget/asset names
  (`Node_Purchased`, `Node_Purchasable`, `Template_Node_Magic`, …). CASC
  decodes that convention into a typed `NodeActivation` (a closed
  `NodeFact` vocabulary + `Evaluate(factSet)`), surfaced on every
  `ParagonNodeRecipeLayer` and `NodeDiscLayer`, plus a `NodeSlot` for
  variant grouping (the grey/rarity/type base discs all share
  `NodeSlot.BaseDisc`). Each activation is **provenance-marked**
  (`NameConvention` where the name literally spells the state,
  `EngineBehavior` for documented inference, `SceneField` reserved) per the
  `feedback_widget-name-not-role` discipline. The consumer supplies its
  computed facts and evaluates; it authors no predicate. The selected-state
  brightness is **baked into the selected disc texture** (no scene tint op
  on the discs — the only `rgbaTint` `0x09A3F17B` bindings are on chrome).
  Acceptance: `ReadParagonNodeRecipe_surfaces_typed_activation_per_layer`.
  58/58 tests green on build `3.0.2.71886`. Devlog 0048.

- **CL-50 — node-template child sub-records decode structurally; per-child
  rect surfaced (FR-C16 R9 / FR-C18 #29 + #26 residuals).** CL-46/47/48
  harvested a parent template's anonymous child sub-records only as a
  **flat handle soup** (`UiWidget.ExtraLayerValues` / `CompositeHandles :
  uint[]`), discarding each child's authored rect. That single loss
  produced every reported render defect: the rarity disc oversized
  (FR-C18), the start filigree over-painted (#26.3), the gate ornate /
  locator mis-placed (#26.4). True grammar: each child is a **self-contained
  name-less mini-widget** — a class id + `0xFFFFFFFF` sentinel, then its
  own schema run + positionally-keyed value records (0x22 or tag-2),
  structurally identical to a named widget. `UiScene.Parse` now decodes
  them into `UiWidget.Children` (`UiWidgetChild{ClassId, Fields}`), so a
  child's `hImageFrame` stays paired with its `nLeft/nTop/…` rect. Findings
  (scene 657304):
  - **FR-C18 #29 — the all-zero rarity-template rect is faithful.** The
    `Template_Node_<rarity>` *parent* binds no usable rect (Magic:
    `bActive,nBottom=0,nRight=0`; Rare: nothing; Legendary:
    `nBottom=0,nRight=0`). The disc geometry lives on the **children**: the
    Magic/Rare disc carries inset **7** (= `Node_IconBase`'s 86² base-disc
    slot) on its selected child; Legendary carries inset **−3** (overscan,
    larger). The pair is co-sized — the unselected disc inherits the
    authored inset. Surfaced as `NodeSelectionDiscs.{Unselected,Selected} :
    NodeDiscLayer{ImageHandle, Rect, Active}` (was `uint`).
  - **#26.1 — symbol z-order.** The rarity disc child shares
    `Node_IconBase`'s inset-7 geometry ⇒ the rarity disc **substitutes the
    base disc** (it is the base, not a z=121 top layer). Blob order is the
    draw order for the shared state-widget run (z 96–120) but the
    `Template_Node_*` widgets (z 121–130) are mutually-exclusive *variant
    definitions* appended after; the chosen template's disc draws at the
    base-disc slot, with `Node_Icon` (the symbol, z=106) and overlays on
    top. No hidden z field; no `Node_Icon` z bug.
  - **#26.2 — `Template_Node_Socketable` is authored empty** (a 240-byte
    stub: 0 children, 0 fields, 0 handles). The empty composite is faithful;
    the socket visual is composed from the present `Node_Glyph` /
    `Usage_Slot_*` widgets (`0x3084D186` bead), not this template.
  - **#26.3 — `Template_Node_Starter`** filigree `0xA0F996FE` is a **140²
    layer at −18 overscan** (surrounds the base), base `0xF8312CA8` inherits
    the cell. Drawing each at its own rect (not both full-cell) is the fix.
  - **#26.4 — `Template_Node_Quest` IS the gate** (no separate
    `Template_Node_Gate`): filigree `0xA0F996FE` (−20), ornate
    `0xC2DF4786`/`0x0E6B6249` (inset 3), and a **conditional locator**
    `0x6D68F45F` (inset 22/26/24/24 — gate by predicate, don't drop). The
    owner's diamond `0xACDA0144`/`0x61FB8387` is **not bound anywhere in
    scene 657304** — a different scene/asset, flagged to the owner.
  Acceptance: `ReadParagonNodeRecipe_surfaces_ordered_state_widget_layers`
  extended (disc insets 7/7/−3, starter filigree −18/140², gate ornate +
  locator rects, socketable empty). 57/57 tests green on build
  `3.0.2.71886`. Devlog 0046.

- **CL-49 — `DecodeMip0` BC row-pitch is texture-specific (#28).**
  `DecodeMip0` hard-coded the stored BC block-row pitch as
  `Align(width, 64)`. That is wrong for atlases whose stored pitch is
  **128-aligned**: BC1 atlas **447106** (`2DUI_Paragon`, 1208×1464) is
  stored at a **1280**-px pitch (320 blocks/row), but `Align(1208,64)`
  gives 1216 (304 blocks/row) — so the decoder read 16 blocks too few
  per row and the row stride drifted, garbling the image (slanted
  banding + a glitch strip). Every frame from a non-64-aligned BC atlas
  was affected (the FR-C14 board background `0x2954DF0C` and the per-node
  cell tile `0xC1473C21` both live in 447106). The fix derives the true
  blocks-per-row from the **exact mip0 byte count**
  (`SerTex[0].SizeAndFlags ÷ (blockRows × blockSize)`), which is exact
  for every atlas (the 64-aligned ones derive identically, so no
  regression), with an `Align(width,64)` fallback + a `pitch ≥ width`
  guard when the size is unavailable. **Supersedes the CL-46-era claim
  (#26) that `0xC1473C21`'s consumer-side garble was a consumer decode
  bug** — it was this library bug; CASC's per-frame probe missed it
  because `0xC1473C21` is a near-uniform dark square, on which row drift
  is invisible. Acceptance:
  `DecodeMip0_uses_stored_row_pitch_for_non_64_aligned_bc1` (the shipped
  decode differs from a forced-1216 decode and is more row-coherent).
  44/44 tests green on build `3.0.2.71886`. Devlog 0045.

- **CL-48 — UI-scene tag-2 field-value encoding (FR-C16 R7).** The
  §10.3 instance-record model read only the 56-byte `0x22` record. There
  is a **second value-record encoding** — the 12-byte **tag-2 block**
  (`tag==2, +4==0, value@+8`) — that some widgets use *instead of*
  `0x22` for the same fields (and some mix them). The pre-R7 parser
  therefore **under-decoded** every tag-2-encoded field: (1)
  `Template_Board_Background_Center` reported an all-zero rect when its
  authored size is **`1200×1200`** (the FR-C11/§10.16 "chrome carries no
  authored sub-rect" claim was an artifact for the centre — the 4 rim
  sides genuinely bind no `nWidth`/`nHeight`, so *they* stay zero
  faithfully); (2) `Node_Icon`'s positional keying collapsed, landing the
  `hImageFrame` handle in `nBottom` (CL-47's `635087190` garbage) — its
  true rect is a symmetric **`28`-inset** symbol slot, so
  `RenderRatios.SymbolOverDisc` corrects from the buggy `100/86` (1.163,
  "fills the box") to **`44/86`** (0.512 — the class glyph inside the
  disc with margin). The `UiScene` parser now reads field values from
  **either** record shape, capped at the schema field count (the
  field-value run precedes any `0x58` layer-block), and confines a parent
  widget's field scan to the run before its first nested child marker.
  This **retires the CL-47 ±4096 rect guard** (the exact decode makes it
  unnecessary). 43/43 tests green on build `3.0.2.71886`. Devlogs 0043
  (grammar crack) + 0044 (shipped).

- **CL-47 — node recipe per-state disc split + sentinel/rect decode
  fixes (FR-C16 R5).** Three CL-46 decode-quality defects, found when
  the Optimizer's interpreter drew the recipe verbatim against the live
  board. (1) **Per-selection-state disc split.** CL-46's
  `CompositeHandles` flattened a rarity sub-template's *unselected* and
  *selected* disc into one list, so a verbatim draw painted the
  selected-state ring on unselected nodes. The
  `Template_Node_<rarity>` widgets are parent widgets whose anonymous
  child sub-records (`UIWindowStyle` + `0xFFFFFFFF` marker, name-less)
  bind one disc handle each; the first two handle-bearing children, in
  scene order, are the unselected then selected disc. Lifted into a new
  `ParagonNodeRecipeLayer.SelectionDiscs: NodeSelectionDiscs?`
  (`Unselected`/`Selected`) — `0x621CB6FF`/`0x72C29402` Magic,
  `0xB71BD068`/`0x03EDABAB` Rare, `0x232DF7F9`/`0xBD27FB7C` Legendary
  (all six match the owner FR-C12 oracle). The consumer draws `Selected`
  only on the currently-selected node. (2) **Sentinel exclusion.**
  CL-46's `v >= 0x10000u` composite filter let the 0x58-block's
  small-*negative* rect insets (`0xFFFFFFFD` = −3, `0xFFFFFFEE` = −18,
  `0xFFFFFFEC` = −20 — overscan for the larger 189² Legendary disc)
  through as bogus handles. `NodeRecipe` now requires the icon-catalog
  validator (`IsParagonTextureHandle`) to resolve each composite handle;
  the negatives — which are *rect insets*, never delimiters or state
  codes — are excluded. (3) **Implausible-rect guard.** `Node_Icon`
  (and other sparse-bound widgets) bind only a *subset* of their schema
  fields, which breaks the §10.3 positional record→field keying so a
  texture handle lands in a rect field (`Node_Icon.nBottom` decoded as
  `0x25DAA956` = 635087190). A projection-level guard rejects rect
  magnitudes beyond the design canvas (±4096), so the garbage no longer
  propagates. `Node_Icon` is the per-node symbol slot (runtime-filled
  `HIconMask`, drawn fit-to-disc-centre) — its template rect is not
  load-bearing. The root-cause fix (re-RE of the sparse instance-binding
  grammar so *every* rect decodes exactly) is tracked for a later round;
  this guard contains the symptom now. Acceptance:
  `ReadParagonNodeRecipe_surfaces_ordered_state_widget_layers` extended
  to assert the three rarity state-pairs, resolvable-only composites,
  and in-range rects. 43/43 tests green on build `3.0.2.71886`. Devlog
  0042.

- **CL-46 — node recipe per-rarity composite handles + substitution
  model (FR-C16 R4).** `ParagonNodeRecipeLayer` gains
  `CompositeHandles: IReadOnlyList<uint>` — the additional 0x58-block
  texture handles on a layer beyond its single `ImageHandle`. Answers
  the Optimizer's per-rarity-disc / per-node-symbol substitution
  question: it is a **hybrid**. (1) **Per-rarity disc = per-rarity
  sub-template:** the `Template_Node_<rarity>` layers carry their
  rarity disc composite in `CompositeHandles` — `Template_Node_Magic`
  → `0x621CB6FF` + `0x72C29402`, `Template_Node_Rare` → `0xB71BD068` +
  `0x03EDABAB` (exact match to the owner's FR-C12 rarity-disc oracle);
  the generic `Node_IconBase` (`0x1D166DC7`) is the default, drawn
  unless a rarity sub-template applies. (2) **Per-node symbol =
  substitution slot:** the `Node_Icon` layer's handle is the node's own
  `ParagonNodeDefinition.HIconMask` (runtime-filled; its template
  `ImageHandle` is 0). So the recipe defines WHERE/size/z; the node +
  rarity data define WHICH handle. Acceptance:
  `ReadParagonNodeRecipe_surfaces_ordered_state_widget_layers` extended
  to assert the Magic/Rare composites. 43/43 tests green on build
  `3.0.2.71886`. Devlog 0041.

- **CL-45 — paragon board grid metric (`ParagonBoardGrid`)
  (FR-C17 R3).** New `Diablo4Storage.ReadParagonBoardGrid()` →
  `ParagonBoardGrid(CanvasWidth, CanvasHeight, CellExtent, Pitch)`,
  read from game data (the UI scene): canvas `1920×1200`
  (`ParagonBoard_main`), node cell extent `100` ref units
  (`Template_Node_Common`), `Pitch = CellExtent` (adjacent cells —
  the `UIParagonBoardStyle` grid container is a style wrapper with no
  grid-layout fields, so there is no extra authored inter-cell gap).
  Replaces the consumer's empirical pixel pitch with the engine's
  authored cell metric; the consumer maps a board cell's
  `(gridX, gridY) → (gridX·Pitch, gridY·Pitch)` and scales the
  `1920×1200` canvas to its render resolution. **Validated against the
  owner's in-game measurement**: the empirical ~67.7px pitch =
  `CellExtent (100) × render-scale (≈0.677)`, so the authored
  100-unit cell reproduces the observed spacing. The per-board logical
  grid (dimensions + cell→node) stays `ParagonBoardDefinition`
  (`Width`/`Cells`/`CellAt`). Acceptance:
  `ReadParagonBoardGrid_surfaces_engine_cell_metric`. Tests green on
  build `3.0.2.71886`. Devlog 0041.

- **CL-44 — paragon node render program (`ParagonNodeRecipe`) +
  widget class-id cracks (FR-C16 R3).** New
  `Diablo4Storage.ReadParagonNodeRecipe()` →
  `ParagonNodeRecipe(IReadOnlyList<ParagonNodeRecipeLayer>)`. The
  engine's per-node composition is a flat, z-ordered list of named
  state-widget layers (each: `ZOrder`, verbatim `WidgetName`,
  `WidgetClassId`, `hImageFrame` `ImageHandle`, `Rect`, `dwAlpha`
  `Alpha`), drawn when the layer's predicate holds. **Hierarchy
  finding (R3):** the UI-scene blob serializes widgets in depth-first
  child order with **no explicit per-widget parent/z field** (the
  widget headers' pre-class + post-sentinel words are all zero), so
  **blob order = child order = draw/z-order** and the node subtree is
  the contiguous sibling run (anchored positionally on the engine's
  `Common_Node*` / `Template_Node_*` names). **No structural state
  predicate field exists** (R2 — every per-widget field is
  layout/anchoring/opacity; `0x0CDB00E9` uncracked is not a state
  code); the engine's widget *name* is the state discriminator, so it
  is surfaced verbatim (per `feedback_widget-name-not-role`) and the
  consumer carries the thin `name → runtime-predicate` glue (the
  Optimizer-confirmed pure-interpreter contract). Directional arrows
  (`Arrow_Top/Right/Bottom/Left`) + connectors are their own ordered
  layers. Also cracked the widget class-style ids via
  `blizzhackers/d4data` `!!D4Checksums.yml` and added to
  `Diablo4.KnownTypeNames`: `UIWindowStyle` (`0x1E3077C7`),
  `UIStackPanelStyle` (`0x112661D5`), `UIParagonBoardStyle`
  (`0x093D303F`), `UIBlinkerStyle` (`0x145F2056`), `UIRActorStyle`,
  `UITextStyle`, `UIScrollBoxStyle`, `UIListBoxStyle`, `UIButtonStyle`,
  `UIWrapPanelStyle`, `UIHotkeyStyle`, `UIControlStyle`. Acceptance:
  `ReadParagonNodeRecipe_surfaces_ordered_state_widget_layers` (z-order
  monotonic; `Node_IconBase → 0x1D166DC7` owner anchor; the 4 arrows).
  42/42 tests green on build `3.0.2.71886`. Devlog 0041.

- **CL-43 — NSlice TiledStyle full decode + variant/field cracks
  (FR-C14 R10).** §11.5. Extends CL-42's `TiledStyleDefinition` from
  "primary handle + scale + opaque suffix" to a full **NSlice**
  (9-slice) decode: `hSourceImage`, `eSliceStyle`, `nPadding`,
  `fTileCenter`, `fTileHorizontalBorders`, `fTileVerticalBorders`.
  `HasPartialDecode` is now `false` for the `NSlice` variant (tag
  `0xBC0D579E`); other variants (`TiledWindowPieces` `0x02E46583`,
  etc.) keep their tile flags at `-1` + `HasPartialDecode = true`.
  Cracked the variant + field names via the `blizzhackers/d4data`
  `!!D4Checksums.yml` type registry + `!NSlice.bc0d579e.yml` schema
  (intel-only). Type tags added to `Diablo4.KnownTypeNames`: `NSlice`,
  `TiledWindowPieces`, `TiledStyleDefinition`,
  `HorizontalTiledWindowPieces`, `VertTiledWindowPieces`,
  `WindowPieces`, `WindowPiecesBase`, `UIImageHandleReference`
  (the `0x6B1C5D9C` texture-handle field type). NSlice field names
  added to `Diablo4.KnownFieldNames`. **Correction:**
  `Vignette → InnerShadow (843662)` has `fTileCenter = 0` (a
  stretched inner-shadow, not a tiled pattern) — so it is NOT the
  paragon-board pattern overlay (the CL-42 R9 working hypothesis is
  retracted). No `SnoGroup.UiStyle` record sources the board pattern
  `0x22FF3AF6` (411 scanned); that pattern renders via the
  Stack-widget `ExtraLayerValues` path, separate from TiledStyle.
  Acceptance: `ReadParagonRenderModel_surfaces_tiled_style_bindings`
  asserts Vignette's NSlice decode (all tile flags 0) +
  Frame_AbilityPoints's tiled NSlice (`fTileCenter = 1`). Tests green
  on build `3.0.2.71886`. Devlog 0040 (R9) + this entry (R10).

- **CL-42 — UI tile-style (`.uis`) typed surface + per-widget
  `snoTiledStyle` binding (FR-C14 R9).** §11.5 + new
  `ParagonBoardChrome.TiledStyleBindings`. Adds:
  - `SnoGroup.UiStyle` = 103 (group format hash `0x80504E18`).
  - `TiledStyleDefinition` typed record with magic
    `0xDEADBEEF`, self-`SnoId`, `TypeTag` (polymorphic-variant tag
    — `0xBC0D579E` for the common `HorizontalTiledWindowPieces`-shape,
    `0x02E46583` observed on `BagBackground` 603760), `ImageScale`
    (`flImageScale`), and `PrimaryHandle` (the +0x60 piece handle).
    `HasPartialDecode = true` on records whose variant-specific
    trailing data is not yet decoded.
  - `TiledStyleBinding(WidgetName, WidgetClassId, TiledStyleSnoId, Style?)`
    surfaced on `ParagonBoardChrome.TiledStyleBindings` for every
    scene-657304 / 964599 widget whose `snoTiledStyle` field
    (`FieldHash` `0x07DB38D3`) is bound to a non-zero, non-sentinel
    SNO id. Small sentinel ids (1, 3, 20) are surfaced with
    `Style = null` so the consumer can distinguish "real binding"
    from "default style".
  - `Diablo4Storage.ReadTiledStyle(int)` / `TryReadTiledStyle`.
  - `Diablo4.KnownFieldNames` / `KnownTypeNames` /
    `FormatFieldHash` / `FormatTypeHash` — cumulative cracked-hash
    registry (also persisted in
    `docs/d4-hash-dictionary.md`) per memory
    `feedback_cumulative-hash-decode`.
  Driven by FR-C14 R8's hash crack of `snoTiledStyle` via the
  `blizzhackers/d4data` `!!D4FieldChecksums.yml` upstream registry
  (cited as intel; see memory `feedback_third-party-re-as-intel`).
  Acceptance: `ReadParagonRenderModel_surfaces_tiled_style_bindings`
  verifies the `Vignette → SNO 843662` (InnerShadow,
  `flImageScale = 1.0`) anchor + 9 other scene-657304 widget names
  appear in `TiledStyleBindings`. 46/46 tests green on build
  `3.0.2.71886`. Devlog 0040.

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
  `..._decodes_proven_structure` connectorBar assertion). (d) **R7 —
  select/deselect brightness/colour: not authored.** `rgbaTint`
  (`0x09A3F17B`) is bound only on non-node widgets; no
  `rgbaTintSelected`/`rgbaTintLit`/`flBrightness` exists; the only
  authored per-widget brightness is `dwAlpha` (`NodeElement.Alpha`).
  Selection = a widget swap (`States`) under the fixed engine shader
  pass (§2.3/§10.7) — `Tint`/`LitTint=null` is the decoded answer.
  Definitive #3 (§10.13).

- **CL-25 — `NodeAvailableGlow` is the selectable glow, not the
  per-rarity ornate; FR-C7 r3/r4 attribution corrected (FR-C8 R9).**
  §10.13. FR-C7's `Project()` read `Elem("NodeAvailableGlow")`
  (`0x4A901508`) as the Rare/Legendary "gold ornate" — the CL-23
  projection gap again (it never read `Template_Node_Rare`/
  `_Legendary`'s own `0x58`-bound layer). Decisive: `NodeAvailableGlow`
  (ClassId `0x145F2056`) binds `0x4A901508` (unique) + a rect and is
  the **selectable/available glow** (state-driven, any rarity — owner
  oracle); the genuine Rare ornate is `Template_Node_Rare`'s own
  `0xB71BD068`. Shipped: r3/r4 now carry `disc` + their template's own
  catalog-validated ornate (`0x4A901508` removed from the baked rows);
  new **`overlay.availableGlow`** State (handle + Rect, one perimeter
  frame) ⇒ §7.2 matrix = **21 rows** (later FR-C12 R2 / CL-34 added
  `overlay.locatedHighlight` + `overlay.equipGlow` — pre-publish
  contract amendments; FR-C8/C12 unreleased). Cross-check:
  `0x4A901508` is **not** a distinct
  rare ornate — it was the mis-labelled glow, now its own row, distinct
  from `0xB71BD068`. Asserted by
  `ReadParagonRenderLayout_decodes_proven_structure`.

- **CL-26 — bound-layer block over-fit; raw decode now lossless +
  structurally gated (FR-C9).** §10.14. The CL-23 `0x58` block model
  required `ownerClassId @+0x20` + `0xFFFFFFFF @+0x28`; those are not
  universal (other blocks carry a pointer/zeros) and a widget's last
  block straddles the next `nameStart` — so a class of real bindings
  (e.g. grey ring `0x87A89F86`, FR-C7 "not in data") was still dropped.
  Generalised the CL-24 lesson: the only stable marker is
  `tag==2, +4==0, value@+8`; `UiScene.Parse` captures every such value
  bounded on the value field (no straddle drop) ⇒ raw `ReadUiScene` is
  **lossless** for texture bindings. New `ReadParagonRenderModel()`
  (exhaustive per-scene `{handle, rect, alpha}` for 657304/964599) +
  `IsParagonTextureHandle` (the shared structural test: handle-
  magnitude ≥`0x10000` ∧ catalog-resolvable). **Coverage gate**
  `ParagonRenderModel_covers_every_bound_atlas_handle` asserts (shape-
  agnostically) every handle-magnitude atlas-resolvable u32 in the raw
  scenes is surfaced — a future gap fails casc CI, not consumer
  eyeballs. Schema published in §10.14.

- **CL-27 — per-binding-record gate complements the handle gate
  (FR-C9 R3).** §10.14. The CL-26 handle-level gate dedups by atlas
  handle: when a state row dropped by `Project()` shared its handle
  with another widget, the gate stayed green. Added
  `ParagonRenderLayout_every_enumerated_state_has_layers`, the
  per-binding-record gate asserting every enumerated state row carries
  at least one bound layer **or** is explicitly marked
  `StateElements.Unresolved = true`. The two gates are complementary:
  CL-26 catches *handle-level* drops (a new binding shape orphaning a
  handle); CL-27 catches *record-level* drops (a state row enumerated
  but unpopulated without acknowledgement). Initial CL-27 also tried
  to map `overlay.selectionRing` to `Node_SearchResultHighlight`
  (`0x49FDA722`) — the only widget-name candidate from a pure
  record-name search — but the visual oracle later proved that widget
  is the search-result decoration (a spiked corona, not a smooth red
  ring); see CL-28.

- **CL-28 — `overlay.selectionRing` is not a separate scene-widget
  overlay; row marked Unresolved (FR-C9 R4).** §10.11 / §10.14.
  Reverted CL-27's mapping of `overlay.selectionRing` to
  `Node_SearchResultHighlight` after the visual oracle showed
  `0x49FDA722` is the search-result decoration (a spiked corona), not
  the smooth red selected-state ring. `Node_SearchResultHighlight`
  remains surfaced by `ReadParagonRenderModel().Scenes` via the
  exhaustive widget list — not under any `States` row. New record
  field `StateElements.Unresolved` is the structural exception the
  per-record gate honors. The selected-state red ring's actual
  scene-binding attribution is the per-rarity selected composites in
  §10.15 (CL-30 R2 corrected the initial mis-attribution to the
  standalone catalog frame `0xB732F921`, which is in the atlas but
  bound to no scene widget; the row stays Unresolved).

- **CL-29 — paragon node composite recipe (FR-C10 R1).** §10.15.
  Per-rarity nodes are not a shader-tinted shared disc; they are
  ordered atlas-frame composites. Initial CL-29 mapping covered
  Rare/Legendary correctly (interior fill + ornate frame, swapped on
  selected for the red-ring composite variant) but mis-attributed
  Magic/Common-selected to a standalone catalog frame; CL-30 corrects
  Magic/Common to their actual scene-bound selected composites
  (`0x72C29402` on `Template_Node_Magic`'s 0x58 block, `0xD3051CCA`
  on the separate `Node_Purchased` widget). `NodeElement` gained
  `AtlasSno`, `NativeWidth`, `NativeHeight` so consumers composite at
  the engine's authoritative native pixel scale without a second
  catalog walk. Per-rarity colour comes from the authored
  interior-fill frame, not a shader tint — `Tint` stays `null`.

- **CL-30 — Magic/Common selected attribution corrected; per-rarity
  layer scene-bindedness gate (FR-C10 R2).** §10.11 / §10.14 / §10.15.
  The R1 visual oracle passed (the consumer's own renderer uses the
  owner-identified composites per FR-C7 §6) but CL-29's projection
  was decode-wrong for Magic/Common-selected: the red ring on the
  game-correct selected disc sits at the *disc perimeter* (in the
  inter-ridge channel), not as a small centred 96² standalone ring.
  Root cause: CL-29 took a uniform "add standalone ring on selected
  for non-Rare/Leg rarities" path because the per-rarity 0x58 block
  for Magic was incompletely classified. In fact `Template_Node_Magic`
  binds `0x72C29402` (154² blue disc + perimeter ring composite) for
  the selected state — the natural parallel to Rare's `0x03EDABAB` /
  Legendary's `0xBD27FB7C`. For Common, the selected composite is
  bound on the separate `Node_Purchased` widget (`0xD3051CCA`, 153²
  dark disc + perimeter ring) — a different scene location than the
  per-rarity templates. CL-30 surfaces those scene-bound composites
  uniformly. Removed the CL-29 `NodeElement.EngineInternal` field
  (no remaining users — the structural distinction was based on the
  mis-attribution). Added gate
  `ParagonRenderLayout_per_rarity_layers_are_scene_bound`: every
  per-rarity layer's handle must appear in scene 657304's per-widget
  bindings (the exhaustive `Scenes` view) — catches a recipe layer
  that references an atlas frame no scene widget binds (the
  CL-29-class regression).

- **CL-31 — paragon board chrome render model (FR-C11 R1).** §10.16.
  Added `ParagonRenderModel.BoardChrome` (typed `ParagonBoardChrome`
  record) carrying the scene 657304 main board background and the
  scene 964599 board-select panel chrome. Re-scoped board chrome
  from "consumer-owned, not reproduced" (FR-C7 §6) to library-decoded
  per owner ruling (2026-05-19); per-node art boundary (§10.15)
  unchanged. Initial CL-31 surfaced only the centre background widget
  and reported the rim animation as engine-internal; CL-32 R2
  corrected the chrome to a 5-piece composite (centre + 4 cardinal
  rim sides) after deeper scene-widget inspection.

- **CL-32 — board chrome corrected to a 5-piece composite; rim sides
  surfaced; non-icon-catalog handles flagged via zero
  `AtlasSno`/native px (FR-C11 R2).** §10.16. R1's CL-31 missed the
  four cardinal-side widgets `Template_Board_Background_{Top, Right,
  Bottom, Left}`, each scene-bound via the standard `0x6B1C5D9C`
  texture-handle field — Top + Bottom share `0x900C7D87`, Left +
  Right share `0x225F2DA8`. These two handles are scene-bound but
  resolve through a non-icon-catalog texture path CASC does not
  currently index, so they are surfaced with `AtlasSno = 0` and
  native px `0` (consumer uses a different texture-resolution path
  or a procedural equivalent). `ParagonBoardChrome` is reshaped from
  a single `MainBoardBackground` field to a 5-piece composite
  (`BackgroundCenter` + `BorderTop` / `BorderRight` / `BorderBottom`
  / `BorderLeft`); pre-1.0-alpha additive-rename, no NuGet release
  carries the old shape. The §3 rim recipe is now record-sourced:
  geometry is 4-cardinal-side bands (Top/Bottom share one band;
  Left/Right share another; no corner widgets — not 9-slice); blend
  mode and animation order/timing remain not authored in scene data
  (engine-internal renderer behaviour on top of the scene-bound side
  bands). The scene-bind gate
  `ReadParagonBoardChrome_layers_are_scene_bound` extends to assert
  the 4 rim-side handles against the raw scene-657304 widget data
  (the icon-catalog-filtered `Scenes` view doesn't see them).

- **CL-36 — socket.socketed inner-well restored (FR-C12 R5).**
  §10.17. CL-35 tentatively dropped the inner spike-frame
  `0x23F487F3` from the `socket.socketed` row "pending visual-oracle
  confirmation of socketed-state inner-frame behavior". Owner
  visual-oracle on the rebuilt app (post-CL-35 consumer integration)
  ruled definitively: socketed looks exactly like selected, with the
  placed glyph icon additionally overlaid. The inner spike-frame
  stays on `.socketed`. Restored: `socket.socketed = [outerDisk,
  beadRing-via-Purchased, innerWell]` — identical 3-layer composite
  across all three socket states. The per-state activation policy
  (bead-ring pulse animation on `.unselected` only; static at
  opacity 1.0 on `.selected` / `.socketed`; placed-glyph-icon
  overlay on `.socketed` from `ParagonNodeDefinition.HIconMask`) is
  consumer-side per FR-C7 §6. Row no-phantom gate (CL-35) passes
  unchanged — `0x23F487F3` is bound on `Usage_Slot_2`, in the
  socket-authorized widget set.
- **CL-41 — Power Script Formula Phase 3: compiled-form AST decoder
  + cross-validation (FR-C13 R5, Phase 3).** §11.2. Optimizer-
  consume-verified-and-authorized R5 Phase 3 lift. Closes the FR by
  decoding the engine's compiled binary AST for expression-text
  slots (the case Phase 2 deferred) and surfacing
  `PowerDefinition.CompiledFormulas: IReadOnlyDictionary<string, double>`
  as the engine-truth `{SF_N → value}` map for cross-validation
  against `ResolvedFormulas`. Two decoder additions:
  - **48-byte type=`0x05` expression record** — the previously-
    undecoded shape that halted Phase 1/2's backward-walk on
    Demonic Spicules. Layout: `[pad@0..3, ASCII text@4..15
    (NULL-terminated), type=0x05@16..19, opcode@20..23 (opaque),
    pad@24..35, type=0x06@36..39, IEEE-754 single@40..43, trailing
    opcode@44..47 (opaque)]`. The 4-byte pad after the record
    explains the 52-byte backward stride. The decoder tries `-16`,
    `-20`, `-52` strides in order; the `-52` candidate must be a
    genuine type=`0x05` record start (a literal at `-52` would
    jump past the slot region into early-tail literal blocks).
  - **Binary-AST evaluation path** — `PowerScriptFormulaEvaluator.
    EvaluateWithBinaryLiterals` consumes binary IEEE-754 singles
    in left-to-right encounter order instead of re-parsing text
    literals. Demonic Spicules's `SF_2 = "SF_1 / 3"` evaluates as
    `SF_1 / binary_literal[0]` = `60 / 3.0f` = `20` (the `3.0f`
    from compiled bytes at +40, not from text `"3"`).
  Demonic Spicules previously decoded as 0 slots (the expression
  record halted the walk); Phase 3 decodes all 3: `SF_0 = "0.02"`
  Layout B literal, `SF_1 = "60"` Layout C literal,
  `SF_2 = "SF_1 / 3"` type=`0x05` expression → 20. R5 cross-
  validation gate (9 Warlock anchors, 24 SF_N keys total): all 24
  `ResolvedFormulas` ↔ `CompiledFormulas` agree to float
  precision; 72-power no-crash sweep passes (0 throws across
  every legendary). Acceptance:
  `PowerDefinition_phase3_compiled_formulas_match_resolved_for_9_warlock_anchors`
  and
  `PowerDefinition_phase3_decodes_demonic_spicules_expression_slot`.
  AST opcode interior markers (`0x07` after type=`0x05` and `0x0E`
  after the embedded literal) deliberately not decoded — the
  single-binary-operator + single-literal-operand record shape
  covers every expression-text slot in the 72-power live build;
  more complex AST shapes would need additional record-shape RE
  and additional test anchors. 45/45 tests green on build
  `3.0.2.71886`. Devlog 0039.

- **CL-40 — Power Script Formula Phase 2: resolved SF_N map +
  engine-function refs (FR-C13 R4, Phase 2).** §11.2. Owner-
  authorized R4 Phase 2 lift. Extends the Phase 1 slot decoder to
  handle Layout B (4-char ASCII + pad + type + float) and Layout C
  (zero-prefix + ASCII@+4 + type + float) records, mixed 16/20-byte
  stride backward walks for tables that interleave 20-byte Layout B
  value records with 16-byte Layout A sentinels (Greater Hex), and
  stripping of multiple trailing `("10", 10.0)` sentinels (vs the
  single-sentinel Phase 1 strip). Adds two typed surfaces to
  `PowerDefinition`:
  - `IReadOnlyDictionary<string, double> ResolvedFormulas` — the
    positional slot table re-keyed by `"SF_N"`. Trivial-numeric
    slots promote `LiteralValue` directly; expression-text slots
    are evaluated by the internal `PowerScriptFormulaEvaluator`
    (recursive-descent parser supporting `+`, `-`, `*`, `/`,
    parens, SF_N refs both bare and braced as `{SF_N}`, function
    calls, and unary minus).
  - `IReadOnlyList<PowerFunctionRef> FunctionRefs` —
    engine-function references the power's localized
    `Description` format-string contains. Surfaced structurally
    (name + resolved-arg values); the consumer registers a
    per-name resolver delegate to substitute the
    engine-runtime value (R4 ask 2 option A — typed
    `FunctionRef` + consumer-side resolver). Barbarian
    *Warbringer* is the canonical anchor with `PlayerHealthMax()`.
  Acceptance: `PowerDefinition_resolves_phase2_formulas_and_function_refs`
  verifies the 8 Layout-A-clean Warlock legendaries + Greater
  Hex (Phase 2 lift) + Warbringer (FunctionRef). Demonic
  Spicules's expression-text record (`"SF_1 / 3"` for SF_2 = 20)
  uses a non-16-byte structure not yet lifted; deferred to
  Phase 3 (compiled-form AST decode per d4parse
  `DT_STRING_FORMULA.CompiledOffset/Size`). 43/43 tests green on
  build `3.0.2.71886`.

- **CL-37 — Power Script Formula slot table — Phase 1 (FR-C13 R3,
  Phase 1).** §11.2. Surfaces `PowerDefinition.ScriptFormulas` —
  the positional slot table the engine resolves through
  `[SF_<i>n</i>...]` placeholders in localized `Description`
  format strings. Phase 1 lifts the Layout-A records (3-character
  ASCII text + type tag `0x06` + IEEE-754 float + zero pad in a
  16-byte block) by walking the blob backward from the universal
  `("0", 0.0)` terminator and stripping the universal `("10", 10.0)`
  sentinel. Anchored against 6 of 9 Warlock legendaries (Pyrosis,
  Fathomless, Overmind, Ritualism, Chaos, Dynamism, Dominion);
  Greater Hex and Demonic Spicules deferred to Phase 2 (Layout-B
  records with 4-character ASCII chunks + Layout-C records with
  pad-prefixed ASCII + the expression-text records like Demonic
  Spicules's `"SF_1 / 3"` for SF_2 = 60/3 = 20). No-crash sweep
  across all 72 legendary powers (8 classes × ~9 each) passes —
  the decoder returns empty for Layout-deferred powers rather than
  fabricating (honest sentinel, parallel to the FR-C9 no-fabrication
  paragon-render gates). Phase 2 will lift the format-string
  expression evaluator (consumer-side currently) into the library
  and surface `IReadOnlyDictionary<string, double>` resolved
  `{SF_N → value}` map; Phase 3 will decode the binary Compiled-form
  AST (per d4parse `DT_STRING_FORMULA.CompiledOffset/Size` model)
  for engine-truth cross-validation. CL-37 + Phases 2/3 jointly
  deliver the FR-C13 R3 option (b-parsed) API shape.

- **CL-35 — socket-row phantom-layer correction + row no-phantom
  gate (FR-C12 R3).** §10.17. CL-34's socket rows incorrectly
  prepended the shared per-rarity grey-base disc `0x1D166DC7` on
  the (false) assumption it was universal across node classes.
  Owner visual-oracle on the rebuilt app proved the engine NEVER
  composites `Node_IconBase` for socket cells in any state — the
  154² grey base would project a ~9.5 px ring beyond the 135² ornate
  disk's silhouette, and that ring is absent in-game. Dropped from
  all three `socket.*` rows. The corrected socket recipe is the
  three game-visible layers only: `outerDisk → beadRing → innerWell`.
  Per-state variations (whether the bead-ring pulse stays on
  selected, whether socketed adds visible glyph art at the inner
  well, whether the inner-well frame stays on socketed) remain
  `needs:owner` for the next visual oracle. New gate
  `ParagonRenderLayout_socket_rows_have_no_phantom_layers` asserts
  every layer in a `socket.*` row is bound on a widget in the
  socket-authorized widget set (`GlyphNodeGlow_Revealed` /
  `GlyphNodeGlow_Purchased` / `Usage_Slot_2`) — anything else is a
  phantom and FAILS at CI time. This is the dual of CL-34's
  row-completeness gate: completeness catches drops, no-phantom
  catches fabrications/contamination. The "follow the recipe"
  directive (consumer-side `feedback_follow-full-game-recipe`) is
  thereby sharpened: *the* recipe is what the engine actually
  composites, not a §7.2 row that includes layers the engine
  doesn't dispatch.

- **CL-34 — special-node socket composite + row-completeness gate
  (FR-C12 R2).** §10.17 + §7.2. CL-33 §1's "the bead ring is
  the complete socket scene-bound art" stands for the bead-ring axis
  but **missed the larger composite**: the on-board per-node socket
  uses three concentric atlas frames in `2DUI_Paragon_transparentElements`
  scene-bound on the `Usage_Slot_2` side-panel widget's 0x58-block
  plus `GlyphNodeGlow_Revealed`'s standard texture-handle field — the
  engine reuses the same atlas frames for both contexts. Back→front
  per owner atlas-frame oracle + CASC's own frame extraction:
  `0xF6443089` (135² ornate outer disk with center opening) →
  `0xBED4CF21` (135² red glowing bead ring — the pulsing animation
  layer) → `0x23F487F3` (136² inner spike-frame with center
  depression where the per-node `HIconMask` glyph icon seats). Plus
  the shared per-node grey-base `0x1D166DC7` behind all three. The
  narrow CL-33 §1 probe filtered widget names by
  `Glyph/Socket/Ring/Pulse` and missed the outer disk + inner
  spike-frame because their binding widget (`Usage_Slot_2`) doesn't
  match those tokens — the CL-31→32 lesson applied to the socket
  axis. Also surfaced two state-overlay widgets the prior probe
  missed: `Node_Located` `0x87A89F86` (135²) → new row
  `overlay.locatedHighlight`; `Node_EquipGlow` `0xFC806F42`
  (91×90) → new row `overlay.equipGlow`. Per-rarity row gaps
  (parallel R2-class finding from the same broad probe):
  `Template_Node_Magic` 0x58 binds `0x621CB6FF` (153² magic base
  composite previously dropped); `Template_Node_Legendary` 0x58
  binds `0xCC3E3B25` (135² in `2DUI_ParagonNodesIcons_Rogue` — the
  **first class-specific atlas surfaced in §10.15**). Both added to
  their respective `RarityComposite()` rows. **Per-state variations
  between socket.unselected/.selected/.socketed** (e.g. whether the
  bead-ring pulse stays on selected, whether the glyph icon swaps in
  at the inner well on socketed) are not yet decoded — left as
  `needs:owner` per the next visual oracle. The §7.2 matrix grew
  19 → 21 rows; FR-C8/C12 is unreleased so the contract is amendable
  (pre-publish, per CL-25 precedent). New gate
  `ParagonRenderLayout_row_layers_cover_every_scene_bound_row_widget_handle`
  asserts the REVERSE direction of the per-rarity / special-node
  scene-bind gates: every scene-bound handle on a row-bearing widget
  in scene 657304 must appear in some row (CL-32 row-completeness
  parity, applied per-row). This catches the exact gap CL-34 §1 fixed
  (a row that omits a scene-bound layer) on future rounds. CASC's
  own frame extraction artifact (`socket-composite-stack.png` in
  `e:/tmp/scene-probe`) is the cross-verification of the recipe
  against owner's atlas-frame oracle.

- **CL-39 — `NodeCellBackground` → `CommonNodeRevealedLayer` rename +
  role-claim retraction (FR-C15 R2).** §10.17. CL-33 surfaced the
  `Common_Node_Revealed` scene-binding (handle `0xC1473C21`,
  authored rect L=R=T=B=3 in the 100-pitch `NodeTemplate`) as
  `ParagonRenderLayout.NodeCellBackground` — the field name
  asserted a role: "per-node cell background tile". The binding
  traversal was correct (the widget genuinely binds this handle
  through the standard `0x6B1C5D9C` texture-handle field), but the
  proposed visual role was wrong: consumer plumbed the binding
  end-to-end and the resolved `0xC1473C21` atlas frame renders as
  a **horizontal ember-strip / cell-reveal glow pattern**, not the
  clean rounded darker square owner sees in-game as the persistent
  per-node tile. Likely the widget represents a transient
  cell-reveal animation (engine renders the ember glow as a cell
  becomes visible), not the steady-state per-node background.
  Rename `NodeCellBackground` → `CommonNodeRevealedLayer` to remove
  the role assertion from the field name; surface the binding facts
  (handle, rect, atlas) without asserting visual role. New memory
  `feedback_widget-name-not-role` captures the lesson: a widget's
  name is not authoritative evidence of its visual role. The
  persistent per-node cell tile owner sees remains unidentified in
  CASC's current decode — needs further RE under owner direction
  (FR-C15 R2 standing). Acceptance: existing
  `ReadParagonRenderLayout_surfaces_common_node_revealed_binding`
  test (renamed from `*_surfaces_per_node_cell_background`) asserts
  only the binding facts; no role assertion. Pattern parallel:
  CL-38 (atlas-name jump retracted 2026-05-20).

- **CL-33 — per-node-cell background + special-node addendum (FR-C11
  R3 §2/§3, FR-C12 §1/§2/§3/§4).** §10.17. Added
  `ParagonRenderLayout.NodeCellBackground` (single `NodeElement`)
  carrying `Common_Node_Revealed`'s binding (handle `0xC1473C21`,
  authored rect `L=R=T=B=3` inside the 100-pitch `NodeTemplate` →
  94×94 cell tile, ~6-ref inter-cell gap, semi-transparent alpha in
  the atlas frame). Documented `NodeAvailableGlow`'s authored rect is
  genuinely all-zero (`NodeTemplate`-inherit) but the bound atlas
  frame is 325 × 326 — over 3× cell pitch — so the consumer must
  compose at `NodeElement.NativeWidth/Height` (the CL-29 fields,
  already populated) not at the cell rect; drawing at 1 cell
  under-draws the glow. Documented start/gate composite recipes in
  §10.17 (parity with the §10.15 per-rarity table) and confirmed
  CL-30 selected-state attribution unchanged. Honest CL-28-grade
  report on the glyph socket: `GlyphNodeGlow_Revealed → 0xBED4CF21`
  is the **complete** scene-bound socket ring — the perimeter bead
  decorations are baked into the 135² atlas frame itself; no
  additional glyph-socket perimeter widget binds in 657304, and the
  icon-catalog-filter pattern from CL-32 does not apply
  (`0xBED4CF21` is catalog-resolvable and already surfaced). Added
  gate `ParagonRenderLayout_special_node_layers_are_scene_bound` —
  parity with the per-rarity gate (CL-30) and the board-chrome gate
  (CL-31/32), cross-references every `RarityOverride < 0` row's
  layers against the raw scene 657304 widget data via `ReadUiScene`
  (the icon-catalog-filtered `Scenes` view drops some special-node
  bindings; raw widget data is the authoritative source).

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
the scoring/objective model, the relight/disc+symbol composite
calibration, or the app's bundled-JSON schema.

**FR-C21 carve-out (2026-05-22, owner-directed) — the boundary moves
for the paragon node-info surface only.** The library now also owns:
the six paragon-node budget-multiplier intrinsics
(<see cref="ParagonPowerBudget"/>, baked as a clean-room calibration
table from owner-validated in-game readings on build `3.0.2.71886`;
they're formula-DSL intrinsics absent from every shipped GameBalance
data table); and **formula evaluation for paragon-node magnitudes**
(`constant × multiplier`; `ParagonMagnitudeFormula.Evaluate`). The
displayed magnitude is what the FR-C21 surface returns, ready to render
— consumers no longer evaluate the formula text themselves for this
surface. The narrower boundary (§8) still holds elsewhere: power-script
formulas, glyph rank/radius scaling, item/affix value resolution, and
general `AttributeFormulaTable` evaluation remain the consumer's.

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
