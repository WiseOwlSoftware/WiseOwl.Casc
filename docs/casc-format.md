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
`CKey → encoding table → EKey → …`.

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
- **OPEN — per-SNO TVFS deep traversal.** Top-level `Base\*.dat`
  (`CoreTOC.dat`, `Texture-Base-Global.dat`) resolve and read correctly
  end-to-end. Per-SNO records (`Base\Meta\<grp>\<name><ext>`) do **not**
  resolve yet — they live below the top level, in a nested `vfs-N`
  sub-manifest the current walk does not fully descend (or the D4 root
  applies an additional name/shared-payload transform at this layer, cf.
  upstream §8.11 D4RootHandler `CreateSNOEntry`/shared-payloads). Next
  iteration: trace which `vfs-N` carries the SNO subtree and confirm the
  sub-manifest recursion + path-prefix accumulation. The upstream
  `0xABBA0003` shared-payload mapping (CL ref: d4-binary-formats §1, §8.11)
  is then layered on for texture payload de-duplication.

## Source / re-verification

- Clean-room from public TACT/CASC documentation (wowdev.wiki) and
  cross-checked against the permissively-licensed references in
  `THIRD-PARTY.md`. No third-party source is incorporated.
- Verified against Diablo IV build `3.0.2.71886`
  (`.build.info` Build Key `522f2f30f1eb0e32af225966b8ac91d1`).
- **Re-verify trigger:** the `.build.info` Build Key changes (seasonal
  patch). Re-run the integration tests; update this log on any drift.
