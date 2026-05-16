# WiseOwl.Casc — CASC + Diablo IV byte-format reference (canonical)

The single, definitive, self-contained byte-level reference for everything
`WiseOwl.Casc` / `WiseOwl.Casc.Diablo4` implement: the game-agnostic
CASC/TACT/TVFS/BLTE transport **and** the Diablo IV SNO record / `.tex` /
combined-meta / StringList layer. Implementable from this document alone
(the project's self-contained-specs rule). One file, one `CL-*` correction
log.

> **Spec authority (converged 2026-05-16).** All Diablo IV access/format
> code now lives in `WiseOwl.Casc`, so spec ownership follows code
> ownership. **This file is the canonical upstream.** The originating
> `e:\Paragon\docs\d4-binary-formats.md` §3–§8.15 is **SUPERSEDED for byte
> layouts** by this document — its format sections are frozen and retained
> only as project history + wiseowl.com article/devlog source. The verified
> truth is re-derived and recorded **here** (see §13 *Provenance &
> migration map*); ParagonOptimizer reads this file and never maintains
> layout docs.
>
> **Policy carve-out (referenced, never absorbed).** Consumer
> interpretation/policy stays authoritative in `e:\Paragon`: the 6
> calibrated engine-intrinsic power-budget multiplier *values*, the
> scoring/objective model (`scoring-model-design.md`,
> `paragon-optimization-objective.md`), the icon relight/composite
> calibration (`gen_selected.py`, §8.15 of the upstream history), and the
> app's bundled-JSON schema. This library decodes raw fields and **never**
> evaluates formulas, scales, scores, or emits app resources.

All multi-byte fields are little-endian unless marked **BE**. CASC/D4
byte-format facts in §§1–13 are clean-room and verified against Diablo IV
build `3.0.2.71886` (`.build.info` Build Key
`522f2f30f1eb0e32af225966b8ac91d1`).

---

## 1. `.build.info`

UTF-8 text at the install root. A `|`-delimited table: row 0 is headers
`Name!TYPE:len`; following rows are builds. Use the row whose `Active`
column is `1`. Needed columns: `Build Key` (hex, → build config),
`CDN Key`, `Version`, `Tags`, `Product`.

## 2. Build configuration

At `Data/config/<k0k1>/<k2k3>/<buildKey>` (lowercase hex). Text:
`key = v1 [v2 …]`, `#` comments. Relevant keys:

| Key | Meaning |
|---|---|
| `encoding` | `<CKey> <EKey>` of the encoding table |
| `root` | content key of the root (Diablo IV: **all zeroes** — uses TVFS) |
| `vfs-root` | `<CKey> <EKey>` of the TVFS root manifest |
| `vfs-1 … vfs-N` | `<CKey> <EKey>` of nested TVFS sub-manifests |
| `install`, `download`, `size` | other manifests (`<CKey> <EKey>`) |

## 3. Local index (`Data/data/*.idx`)

16 buckets `00*.idx … 0f*.idx`; within a bucket use the lexicographically
last file. **Open share read+write** — the running game / Battle.net agent
keeps these open.

```
i32  HeaderHashSize
i32  HeaderHash
byte[HeaderHashSize] header        // version, EKey/size field widths, …
pad to ((8 + HeaderHashSize + 0x0F) & ~0x0F)
i32  EntriesSize
i32  EntriesHash
entry[EntriesSize / 18]:
  byte[9] eKeyPrefix               // first 9 bytes of the EKey
  byte    indexHigh
  u32 BE  indexLow
  i32 LE  size                     // archive-envelope length
archiveIndex = (indexHigh << 2) | ((indexLow & 0xC0000000) >> 30)
offset       =  indexLow & 0x3FFFFFFF
```

First key wins on duplicates. The index keys on the **9-byte EKey prefix**.

## 4. Archive envelope (`Data/data/data.NNN`)

At `(archiveIndex, offset)` read `size` bytes:

```
byte[16] eKey, byte-reversed   (compare first 9 to the requested EKey)
i32 LE   size                  (== index size)
byte[10] reserved
byte[…]  BLTE blob             (size - 30 bytes)
```

## 5. BLTE

```
'BLTE' (0x42 0x4C 0x54 0x45)
u32 BE headerSize              // 0 ⇒ single headerless chunk
if headerSize > 0:
  byte 0x0F
  u24 BE chunkCount
  chunkCount × { u32 BE compSize; u32 BE decompSize; byte[16] md5 }
chunks follow, back-to-back; each: byte mode, then body
  'N' stored verbatim
  'Z' zlib (RFC-1950) stream
  'F' a nested BLTE blob (recurse)
  'E' encrypted (Salsa20/ARC4) — needs a TACT key (not shipped)
```

Headerless single chunk: everything after the 8-byte magic is one chunk
(mode byte then body).

## 6. Encoding table (BLTE-decoded)

```
[0..1]  'EN'
[2]     version (1)
[3]     CKeyLength (16)
[4]     EKeyLength (16)
[5..6]  CKeyPageSize  (KiB, BE)
[7..8]  EKeyPageSize  (KiB, BE)
[9..12] CKeyPageCount (BE)
[13..16]EKeyPageCount (BE)
[17]    unk1
[18..21]ESpecBlockSize (BE)         ← see correction CL-1
[22 …]  ESpec string block (ESpecBlockSize bytes)
        CKey page index: CKeyPageCount × 32 bytes (skipped)
        CKey pages, each padded to a 4096-byte chunk boundary:
          while keyCount = byte, keyCount != 0:
            u40 BE fileSize
            byte[CKeyLength] cKey
            keyCount × byte[EKeyLength] eKey   (first eKey is the one we want)
          (a 0 keyCount byte ends the page)
          remaining = 4096 - ((pos - chunkStart) % 4096)
          if remaining == 0xFFF: pos -= 1; page++; continue
          else pos += remaining
```

## 7. TVFS (`vfs-root`, BLTE-decoded)

Directory header (offsets within the decoded blob):

```
[0..3]  'TVFS' (0x53465654, LE u32)
[4]     formatVersion (1)
[5]     headerSize
[6]     eKeySize (9)
[7]     patchKeySize (9)
[8..11] flags (BE)
[12..15]pathTableOffset (BE)   [16..19]pathTableSize (BE)
[20..23]vfsTableOffset  (BE)   [24..27]vfsTableSize  (BE)
[28..31]cftTableOffset  (BE)   [32..35]cftTableSize  (BE)
[36..37]maxDepth (BE)          (EST table follows; unused)
cftOffsSize = 4 if cftTableSize>0xFFFFFF, 3 if >0xFFFF, 2 if >0xFF, else 1
```

Path table walk (recursive). Each entry:
optional `0x00` ⇒ pre-separator; `len`+`byte[len]` name (unless `0xFF`);
optional `0x00` ⇒ post-separator; then either `0xFF` + `i32 BE nodeValue`
(a node) or an implicit post-separator.

- `nodeValue & 0x80000000` ⇒ **folder**: recurse into the next
  `(nodeValue & 0x7FFFFFFF) - 4` bytes, keeping the appended name as a
  prefix; rewind the path on return.
- else ⇒ **file**: `nodeValue` indexes the VFS table.
  `vfs[nodeValue]`: `byte spanCount`, then spans of
  `{ i32 BE contentOffset; i32 BE contentLength; var(cftOffsSize) cftOffset }`.
  At `cft[cftOffset]` the first `eKeySize` (9) bytes are the file's EKey.

A leading `0xFF` + folder `nodeValue` prelude on the whole path table is
skipped before walking.

**Sub-manifests.** If a span's EKey (9-byte) is one of the build config's
`vfs-root`/`vfs-N` EKeys, that file is itself a nested TVFS manifest:
read+BLTE-decode it and recurse, keeping the path prefix.

**Path hashing.** A file's lookup key is the 64-bit Jenkins *lookup3*
(`hashlittle2`) of the assembled path, normalized `/`→`\` and ASCII
upper-cased, returned as `(c << 32) | b`. (Implemented as
`CascPathHash`; named for its role, not the algorithm's author —
attribution in `NOTICE`.)

## 8. Read path

`path → CascPathHash → TVFS → EKey → local index (9-byte) → archive
envelope → BLTE → bytes`. By content key:
`CKey → encoding table → EKey → …`. For a Diablo IV SNO the path is
`Base\<Folder>\<id>` (see correction CL-4); an empty `Payload` follows the
`0xABBA0003` shared-payload alias (CL-5).

---

## 9. Diablo IV StringList container (`.stl`, SNO group 42) — localized text

The definitive reference for Diablo IV localized strings. Fully
reverse-engineered and **validated bundle-wide** against build
`3.0.2.71886` (58,286 tables / 175,014 strings; the walk lands exactly at
EOF). Cross-checked against `alkhdaniel/diablo-4-string-parser` (the
standalone `.stl` parser) — this section adds the *consolidated container*
that parser does not cover.

### 9.1 Addressing

StringList content is **not** per-SNO path-addressable (`Base\Meta\<id>`
does not resolve for group 42 — same situation as texture *meta*). It is
delivered through per-locale consolidated bundles in TVFS:

| TVFS path | Contents |
|---|---|
| `base/StringList-Text-<locale>.dat` | The consolidated catalog for one locale (every table). |
| `base/StringList-Text-<locale>-0x<16-hex>.dat` | Per-locale content shards (analogous to the texture `…-0x<h>.dat` shards). |
| `base/StringList-Text-Global.dat` | Locale-independent container (tiny; usually a single placeholder table). |

Locales observed in the live build: `enUS deDE esES esMX frFR itIT jaJP
koKR plPL ptBR ruRU trTR zhCN zhHM` (the install also carries `zhTW`).
The consolidated `base/StringList-Text-<locale>.dat` is what a consumer
reads; resolve it through TVFS (`/`→`\`, upper-cased at hash time, like all
paths).

### 9.2 Container — the `0x44CF00F5` combined-meta family

The consolidated bundle is the **same `0x44CF00F5` combined-meta container
as `Texture-Base-Global.dat`** (§8 / texture catalog), with one key
difference in per-entry placement:

```
u32  magic   = 0x44CF00F5
u32  count
count × { i32 sno ; u32 size }            // the index, in table order

prevEnd = 8 + count*8                      // = end of index
for i in 0 .. count-1:
    B = alignUp8(prevEnd)                  // body base of table i
    prevEnd = B + size[i]                  // advance (size is body length)
    // sno for table i is index[i].sno  (POSITIONAL — not stored in the body)
```

> **Difference vs. the texture catalog (important).** The texture catalog
> places each descriptor at `alignUp8(prevEnd) + 8` and stores the SNO id
> at `descStart+0` (`TextureDefinition` body base = `descStart+4`).
> StringList uses **no `+8`** and stores **no SNO id in the body** — the
> body begins exactly at `B = alignUp8(prevEnd)` and the SNO id is taken
> positionally from the index. (Empirically: the texture `+8`/`snoId`
> convention yields all-zero bodies for StringList; `B = alignUp8(prevEnd)`
> decodes every one of the 58,286 tables.) See correction CL-7.

### 9.3 StringListDefinition body (relative to `B`)

```
B+0  .. B+15   header / pad (16 bytes)
B+16           u32  blockSize        (e.g. 0x20; not needed to read strings)
B+20           u32  infoLength       (byte length of the entry table)
B+24 .. B+31   pad (8 bytes)
B+32           entry[ infoLength / 40 ]      // 40-byte stride

entry (40 bytes):
  +0   i64 pad
  +8   u32 keyOffset      // B-relative
  +12  u32 keyLen         // bytes (includes a trailing NUL)
  +16  i64 pad
  +24  u32 valOffset      // B-relative
  +28  u32 valLen         // bytes (includes a trailing NUL)
  +32  i64 pad

label = UTF-8 at  B + keyOffset , keyLen bytes  (strip trailing NUL)
text  = UTF-8 at  B + valOffset , valLen bytes  (strip trailing NUL)
```

`entryCount = infoLength / 40`. Strings are **UTF-8** (values carry D4
markup, e.g. `{c_important}…{/c}`, `{VALUE}`, `[{VALUE2} * 100|1%|]`,
`{s1}`/`{s2}` substitution tokens). Labels are unique only **within a
table**; the table (SNO) is the domain bucket (`AttributeDescriptions`,
`Bnet_Chat`, skill/affix/item tables, …) — resolve the table by SNO
(name via CoreTOC group 42) then the label.

### 9.4 Verified anchors (build 3.0.2.71886, enUS)

- `count = 58286`; full walk `finalPrevEnd = 20,207,724`,
  `blobLen = 20,207,728` (4-byte trailing pad).
- table SNO `4080` = `AttributeDescriptions`, 646 entries.
- table SNO `4087` = `Bnet_Chat`: label `ChatLink_WhisperedTo` →
  `"{s1} whispers: {s2}"`.
- last table SNO `2646845` = `DungeonAffix_Positive_Torment_AncestralElites`:
  `AffixName` → `"{c_white}Dungeon Delve{/c}"`.

Implemented by `WiseOwl.Casc.Diablo4.StringListCatalog` /
`Diablo4Storage.GetStrings(locale)` / `TryGetString`.

---

## 10. Diablo IV SNO file wrapper & addressing

Every SNO blob (the bytes from `Diablo4Storage.ReadSno`) begins with a
16-byte `SNOFileHeader`; the **payload base is `0x10`** and *all* record
field offsets and `DT_VARIABLEARRAY` `dataOffset`s in §§12–14 are measured
from there.

```
SNOFileHeader (16 bytes):
  0x00 u32 dwSignature   (== 0xDEADBEEF)
  0x04 u32 dwFormatHash  (often 0 → resolve via CoreTOC EntryFormatHashes[group])
  0x08 u32 dwDummy
  0x0C u32 dwXMLHash
payload base = 0x10  (the SNO Id, DT_INT, sits here)
```

**Addressing (CL-4/CL-5).** A D4 SNO resolves through TVFS by the path
`<prefix>\<Folder>\<id>` (prefix `Base`; folder ∈
`Meta|Payload|PayLow|PayMed|Child`; numeric id; no group/name/extension; a
child sub-id appends `-<subId>`). An empty/absent `Payload` follows the
`0xABBA0003` `CoreTOCSharedPayloadsMapping` alias to the holder SNO.
CoreTOC (`0xBCDE6611`) supplies name↔id↔group + per-group format hash.

## 11. DT primitive encodings (record fields)

Field offsets in §§12–13 are payload-relative; read at `0x10 + offset`.

| DT type | Encoding |
|---|---|
| `DT_INT/UINT/DWORD/SNO/ENUM/GBID` | 4-byte LE at the field offset (`DT_GBID` `0xFFFFFFFF` = null) |
| `DT_FLOAT` | 4-byte IEEE-754 LE |
| `DT_CHARARRAY[n]` | inline NUL-terminated ASCII, `n` bytes reserved |
| `DT_STRING_FORMULA` | 32-byte struct: `i64 pad; i32 srcOffset@+8; i32 srcSize@+12; i32 compiledOffset@+16; i32 compiledSize@+20`. Text = ASCII at payload `srcOffset`, `srcSize` bytes (strip NUL/trim) |
| `DT_VARIABLEARRAY` (record form) | `i64 pad; i32 dataOffset@+8 (payload-relative); i32 dataSize@+12`. Element count = `dataSize / elementStride`; no count field |
| `DT_POLYMORPHIC_VARIABLEARRAY` | `i64 pad; i32 dataOffset@+8; i32 dataSize@+12; i32 count@+16; i32 pad2@+20`. An 8-byte type tag precedes the element struct |

> The combined-meta (`0x44CF00F5`) container variant of the variable-array
> descriptor is different (`i32 pad; i32 off@+4; i32 size@+8` for textures;
> StringList uses `B = alignUp8(prevEnd)`) — see §9 / §14, not this table.

## 12. Paragon record layouts (groups 106/108/111/112)

All offsets payload-relative (base `0x10`). RE-VERIFIED 2026-05-16 via the
B1–B4 typed readers against build `3.0.2.71886` (provenance: upstream
`d4-binary-formats.md §5`, the *VERIFIED* tables + glyph correction).

**`ParagonBoardDefinition`** (group 108, `.pbd`):
`snoId@0`; `nWidth@12` (DT_UINT); `arEntries@16`
(`DT_VARIABLEARRAY[DT_SNO]`; `dataOffset@+8` payload-rel, `dataSize@+12`).
Cells = `dataSize/4` LE u32 SNO ids, row-major (`index=row*Width+col`),
`0xFFFFFFFF`=empty. Cell count == `Width*Width` (21×21=441 on this build).

**`ParagonNodeDefinition`** (group 106, `.pgn`):
`snoId@0`; `hIcon@8` (DT_UINT); `hIconMask@12` (DT_UINT);
`eRarityOverride@20` (0=Common,2=Magic,3=Rare,4=Legendary);
`snoPassivePower@24` (DT_SNO group 29); `ptAttributes@32`
(`DT_VARIABLEARRAY[AttributeSpecifier]`; `dataOffset@+8`, `dataSize@+12`);
`bHasSocket@80` (DT_INT); `bIsGate@84` (DT_INT).
`AttributeSpecifier` stride **88**: `eAttribute@+0`; `nParam@+4` (DT_INT);
the distinct value `@+12`; inline formula `srcOffset@+24`/`srcSize@+28`
(payload-relative, used when GBID is null); `gbidFormula@+48` (DT_GBID;
`0xFFFFFFFF` ⇒ use the inline text).

**`ParagonGlyphDefinition`** (group 111, `.gph`):
`snoId@0`; up to three affix `DT_SNO` ids at `@104/@108/@112`
(`0`/`0xFFFFFFFF` slots omitted). Some group-111 SNOs are short
placeholder records — readers must bounds-check before `+104`.

**`ParagonGlyphAffixDefinition`** (group 112, `.gaf`, formatHash
353797140): `snoId@0`; `eAffectedNodeRarity@24` (1=Normal/2=Magic/3=Rare);
`eBonusOperation@48` (1/2/4/5); `flStartingBonusScalar@76` (DT_FLOAT, ==
Maxroll `base`); `flAddedBonusScalarPerLevel@80` (DT_FLOAT, == `perLevel`).

## 13. GameBalance `AttributeFormulas` (group 20, SNO 201912)

Payload-relative; RE-VERIFIED 2026-05-16 via B5 (provenance: upstream
`d4-binary-formats.md §7.3-VERIFIED`). Only `eGameBalanceType == 22`
(AttributeFormulas) is in scope; other GameBalance table types have
different element structs (deferred, feature-backlog C6).

```
GameBalanceDefinition: snoId@0; eGameBalanceType@8 (==22);
  ptData DT_POLYMORPHIC_VARIABLEARRAY @16 → dataOffset@+8 (payload-rel)
  tableBase = dataOffset + 8        (8-byte polymorphic type tag)
  AttributeFormulaEntry_Table: tEntries DT_VARIABLEARRAY @ tableBase+16
    → entries dataOffset@+8, dataSize@+12 ; ENTRY STRIDE 280
AttributeFormulaEntry (280):
  szName  DT_CHARARRAY[256] inline @ +0
  gbid    DT_GBID            @ +256   (in-record value is 0xFFFFFFFF/null)
  arRanges DT_VARIABLEARRAY  @ +264   (dataOffset@+8, dataSize@+12); RANGE STRIDE 48
AttributeFormulaRange (48):
  nItemPowerRangeStart i32 @ +0 ; rangeValue1 f32 @ +4 ; rangeValue2 f32 @ +8
  tFormula DT_STRING_FORMULA @ +16  (FormulaOffset@+8, FormulaSize@+12)
  formula text = ASCII @ payload FormulaOffset, FormulaSize bytes (trim)
```

**Identity:** the in-record `gbid` is null; an entry's identity is
`GbidHash(szName)` (case-insensitive DJB2 — §“GbidHash”, == `0x42C16A1B`
for `ParagonNodeCoreStat_Normal`). A node's `gbidFormula` (§12) equals
`GbidHash(formulaName)`; resolve `gbid → name → arRanges[0] text`. The
library returns **text + name/GBID indices only**; evaluation + the 6
calibrated intrinsics are the consumer's (carve-out).

## 14. Texture `.tex` / combined-meta / `ptFrame` / BCn

Texture pixel payloads are addressable by SNO id (§10); the
`TextureDefinition` *meta* is **not** per-SNO — it is consolidated into the
`0x44CF00F5` combined bundle `Base\Texture-Base-Global.dat` (the same
container family as the StringList per-locale bundle, §9). Per-entry the
texture container uses `descStart = alignUp8(prevEnd) + 8` with the SNO id
at `descStart+0` and the `TextureDefinition` body at `descStart+4` — *this
`+8`/in-body-snoId convention is what differs from StringList* (§9 / CL-7).
`TextureDefinition`: `eTexFormat@8`, `dwWidth@16`, `dwHeight@18`,
`serTex@64`, `ptFrame@80` (combined-meta variable-array form
`i32 pad; off@+4 (blob-rel from descStart); size@+8`). Paragon atlases are
**BC3** (`eTexFormat 49`), mip0 at payload offset 0, row width
`align(W,64)` then crop. `ptFrame` (`TexFrame`, 36 B:
`u32 ImageHandle; f32 U0,V0,U1,V1; …`) gives atlas sub-rects; pixel rect =
`floor(U·W)…ceil(U·W)`. **Node↔icon link is first-party:**
`ParagonNode.hIconMask`/`hIcon` == `TexFrame.ImageHandle` (no correlation
needed) — exposed via `Diablo4Storage.TryGetIconFrame`. RE-VERIFIED via the
texture/combined-meta readers + B6 (provenance: upstream §8.11–§8.15;
BCn decode is image-library-agnostic raw RGBA, §“boundary”).

## 15. Provenance & migration map

Auditable mapping of every upstream `d4-binary-formats.md §3–§8.15`
byte-format item to its destination here, so the spec-authority handoff
loses nothing. Status = RE-verified against build `3.0.2.71886`.

| Upstream §/topic | Destination here | Status |
|---|---|---|
| §1 CoreTOC `0xBCDE6611` | §10 (addressing) + CL-2/CL-4 | verified |
| §1 `0xABBA0003` shared-payload mapping | §10 + CL-5 | verified |
| §3 SNO addressing + `SNOFileHeader` (base `0x10`) | §10 | verified |
| §4 DT primitive encodings | §11 | verified |
| §5 ParagonBoard/Node/Glyph/GlyphAffix layouts | §12 | verified (B1–B4) |
| §5.1 / §8.13 node↔icon (`hIconMask==ImageHandle`) | §14 + B6 | verified |
| §7 / §7.3-VERIFIED GameBalance AttributeFormulas | §13 | verified (B5) |
| §8.1–§8.2 `TextureDefinition` / `eTexFormat` / BCn | §14 (+§9 container) | verified |
| §8.12–§8.14 `0x44CF00F5` bundle / `ptFrame` slice | §14 (+§9) | verified |
| §8.5 StringList per-locale bundles | §9 | verified (CL-7) |
| §7 the **6 calibrated intrinsic VALUES** | NOT absorbed → `e:\Paragon` policy | carve-out |
| §8.14/§8.15 relight & disc+symbol composite | NOT absorbed → `e:\Paragon` policy | carve-out |
| §3–§8 investigation narrative / dead-ends | `docs/devlog/*` + `ARTICLE-SOURCE.md` | history |

The upstream file is frozen for layouts and demoted to history/article
source (the demotion banner is the ParagonOptimizer session's edit to its
own repo; `e:\Paragon` is read-only here).

---

## Correction log

This log records errors/omissions found while implementing, and the true
content (per the user's standing instruction to correct the spec when wrong).

- **CL-1 (2026-05-16) — encoding header `ESpecBlockSize` offset.** It is at
  byte **18** (after the 1-byte `unk1` at 17), a BE `u32`. An initial
  implementation that read it at 17 misaligned the entire CKey-page region
  (the table still parsed *some* entries, so a naive size check passed but
  real content-key lookups failed). Verified by a closed-loop test:
  `install` CKey → encoding → EKey must be present in the local index.
- **CL-2 (2026-05-16) — `.idx`/archive-envelope/BLTE/TVFS not in upstream.**
  The upstream `d4-binary-formats.md` omits the CASC transport entirely
  (it delegated to CascLib). §3–§7 above are the clean-room-verified
  specification of that omitted layer, proven against build `3.0.2.71886`.
- **CL-3 (2026-05-16) — file sharing.** `.idx` and `data.NNN` must be
  opened `FileShare.ReadWrite`; the live game / Battle.net agent holds
  them open. A plain read share throws `IOException`.
- **CL-4 (2026-05-16) — per-SNO addressing RESOLVED (supersedes the prior
  "OPEN" item).** The TVFS walk was never the problem: it is complete
  (1,759,690 entries; the 37 nested `vfs-N` sub-manifests are descended;
  the full install tree resolves). Diablo IV addresses SNO content in
  TVFS by **`Base\<Folder>\<id>`** — folder ∈ {`Meta`,`Payload`,`PayLow`,
  `PayMed`,`Child`}, `<id>` the decimal SNO id, **no group folder, no
  name, no extension** (a child sub-id appends `-<subId>`). Empirically:
  `Base\Meta\2458674` resolves; the name-path
  `Base\Meta\108\Paragon_Warlock_00.pbd` and the CascLib.NET-era
  `base:meta\<id>` colon form both miss. So `ReadSno` builds
  `Base\<Folder>\<id>`. CoreTOC is needed only for name↔id/group, not for
  addressing.
- **CL-5 (2026-05-16) — texture payloads are mostly direct.** With the
  complete TVFS, `Base\Payload\<textureId>` resolves directly for the
  paragon atlases (incl. the per-class ones the upstream §8.5 census,
  taken through CascLib.NET's narrower view, reported as "no direct
  entry"). The `0xABBA0003` `CoreTOCSharedPayloadsMapping.dat`
  (`i32 magic; i32 count; count × {i32 snoId, i32 sharedSnoId}`; 35,616
  entries this build) is implemented as a **transparent fallback**: an
  empty/absent direct payload follows the alias to the holder SNO.
- **CL-6 — `CoreTOCReplacedSnosMapping.dat` deliberately not implemented.**
  Not needed on the current build (every paragon/board/node/gam id and
  the paragon atlases resolve without it). Implement only if a seasonal
  patch makes a known SNO 404 and it is found in the replaced map
  (gated, per the consumer assessment FR-6).
- **CL-7 (2026-05-16) — StringList container reversed (FR-13 DONE; no
  longer deferred).** §9 is the full, bundle-wide-validated spec.
  Key facts established here: (a) StringList is delivered via per-locale
  consolidated `0x44CF00F5` bundles, **not** per-SNO `Base\Meta\<id>`;
  (b) it shares the texture combined-meta container but the per-entry body
  is at `B = alignUp8(prevEnd)` with **no `+8`** and **no SNO id in the
  body** (positional, from the index) — the texture `+8`/`snoId@descStart`
  convention produces all-zero StringList bodies and is wrong here;
  (c) the StringListDefinition is `infoLength@B+20`, 40-byte entries at
  `B+32` (`keyOffset@+8,keyLen@+12,valOffset@+24,valLen@+28`), UTF-8
  strings at `B+offset`. Validated across all 58,286 tables (walk lands at
  EOF). The earlier "FR-13 deferred / needs its own RE workstream" note in
  `feature-backlog.md` is superseded — it is implemented and proven.
- **CL-8 (2026-05-16) — typed record readers + spec authority.** The
  converged boundary moved typed *record decoding* into the library
  (B1–B6): `ParagonBoardDefinition`/`ParagonNodeDefinition`/
  `ParagonGlyphDefinition`/`ParagonGlyphAffixDefinition`/
  `AttributeFormulaTable` + `Diablo4Storage.Read*` + `TryGetIconFrame`,
  raw fields only (no evaluation/scoring/emission — the library ships
  **no formula evaluator at all**, by decision). The §5/§7 layouts were
  re-derived and verified here (§§12–13; §7 acceptance matrix passes
  verbatim: board 2458674 W21/441; node 678776 sig 0xDEADBEEF;
  GameBalance 201912 = 1038 entries, `ParagonNodeCoreStat_Normal`→"5",
  `_Magic`→"7"). Spec authority transferred to this file; upstream
  `d4-binary-formats.md` §3–§8.15 frozen for layouts (§15 provenance
  map). `NodeAttribute` exposes both `NParam` (+4) and `ParamPlus12`
  (+12) raw so the consumer never re-parses the specifier. Glyph readers
  bounds-check (short placeholder group-111 records exist).

### Library boundary (FR-5/FR-16 — explicit)

`WiseOwl.Casc` / `WiseOwl.Casc.Diablo4` own: the **transport**, **CoreTOC**
(incl. name↔id index), the **`0x44CF00F5` combined-meta** /
`TextureDefinition`, **shared-payload** resolution, the **`SnoRecord`**
primitive reader (`U8/U16/U32/I32/F32`, `Ascii`, record-style
`DT_VARIABLEARRAY` = `{i64 pad, i32 off@+8, i32 size@+12}`,
payload-relative — distinct from the combined-meta variant
`{i32 pad, off@+4, size@+8}` blob-relative, owned internally by
`CombinedTextureMeta`), image-library-agnostic **BC1/BC3 decode**
(`DecodeMip0` → raw straight-alpha RGBA32; the caller crops with
`TexFrame.PixelRect` and owns any imaging/PNG/compositing), the game-wide
**`GbidHash`**, and the per-locale **StringList catalog** (§9 —
`StringListCatalog` / `Diablo4Storage.GetStrings`/`TryGetString`; a generic
D4-container concern, reusable across Blizzard games).

Typed paragon records (`ParagonBoard` 108 / `ParagonNode` 106 /
`ParagonGlyph` 111 / `ParagonGlyphAffix` 112) and **GameBalance
`AttributeFormulas`** (SNO 201912), including the 6 calibrated engine
intrinsics, are **domain logic and stay in the consumer**
(`d4-binary-formats.md` §5–§7). The library intentionally does not own
them; a consumer migrates only its CASC/`SnoRecord`/`TextureDefinition`
layer.

**FR-16 (reinforced).** Item / Affix / Power / Class / GameBalance →
stat-effect *modeling* is a ParagonOptimizer **domain spec** built on the
library's id-keyed read + `SnoRecord` + `GbidHash` (+ StringList once
FR-13 lands). The library will **not** grow typed game-record APIs. Round-2
feature requests and their disposition (incl. FR-13 deferred to its own RE
workstream) are tracked in `docs/feature-backlog.md`.

## Source / re-verification

- Clean-room from public TACT/CASC documentation (wowdev.wiki) and
  cross-checked against the permissively-licensed references in
  `THIRD-PARTY.md`. No third-party source is incorporated.
- Verified against Diablo IV build `3.0.2.71886`
  (`.build.info` Build Key `522f2f30f1eb0e32af225966b8ac91d1`).
- **Re-verify trigger:** the `.build.info` Build Key changes (seasonal
  patch). Re-run the integration tests; update this log on any drift.
