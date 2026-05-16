# CASC transport — self-contained format spec

The byte-level reference for the **game-agnostic** CASC/TACT/TVFS/BLTE
layer `WiseOwl.Casc` implements. This is written to be implementable from
this document alone (per the project's self-contained-specs rule).

> **Relationship to the upstream record.** The originating ParagonOptimizer
> project's `e:\Paragon\docs\d4-binary-formats.md` §3–§8.15 is the
> authoritative reverse-engineering record for the **Diablo IV SNO / `.tex`
> layer** (CoreTOC `0xBCDE6611`, SNO payload base `0x10`, `DT_*`,
> shared-payload `0xABBA0003`, combined-meta `0x44CF00F5`, `TextureDefinition`,
> `ptFrame`, the node↔icon link, BC3). Cross-reference it; we do **not**
> duplicate it. **It deliberately does not specify the CASC transport** — it
> says "WoW-Tools CascLib already implements all of this." This document
> fills that omission: the transport is specified here, clean-room, verified
> against Diablo IV build `3.0.2.71886`.

All multi-byte fields are little-endian unless marked **BE**.

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

### Library boundary (FR-5 — explicit)

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
