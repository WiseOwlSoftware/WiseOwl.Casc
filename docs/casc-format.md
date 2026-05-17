# CASC transport — byte-format reference

Game-agnostic **CASC / TACT / TVFS / BLTE** local-storage format, as
implemented by the `WiseOwl.Casc` package. Self-contained and implementable
from this document alone.

For the Diablo IV SNO / record / texture / StringList layer that sits on
top of this transport, see the companion **[`casc-diablo4-format.md`](casc-diablo4-format.md)**
(`WiseOwl.Casc.Diablo4`). Together these two documents are the canonical
byte-format reference; each owns its own layer and correction log.

---

## 1. Scope & status

- **Specifies:** reading a *local* Blizzard installation — `.build.info`,
  the build configuration, the local archive index, the archive envelope,
  BLTE decoding, the encoding table, and the TVFS path→content file
  system.
- **Does not specify:** CDN/online transport, write/patching, or any
  game-specific layer (Diablo IV specifics live in the companion doc).
- **Status:** clean-room from public TACT/CASC documentation
  (wowdev.wiki) and the permissively-licensed references in
  `THIRD-PARTY.md`; no third-party source incorporated. Every fact below
  was verified empirically against Diablo IV build `3.0.2.71886`
  (`.build.info` Build Key `522f2f30f1eb0e32af225966b8ac91d1`). See
  Appendix B for the re-verify trigger.
- **Authority:** this file supersedes, for the transport layer, the
  frozen upstream `e:\Paragon\docs\d4-binary-formats.md` (which deferred
  the transport to a third-party library). Errata are tracked in
  Appendix A.

## 2. Conventions

- Multi-byte integers are **little-endian** unless explicitly marked
  **BE** (big-endian).
- `u8/u16/u24/u32/u40/u64` = unsigned integer of N bits; `i32` = signed
  32-bit. `byte[n]` = n raw bytes.
- Offsets and sizes are in bytes. `x[a..b]` is the inclusive byte range.
- "Open share read+write" means the file must be opened allowing other
  writers — the running game / Battle.net agent keeps these files open
  (Appendix A, CL-3).

## 3. Overview — the read pipeline

A consumer addresses content either by a TVFS **path** or by a
**content key** (CKey). Resolution:

```
                    ┌─────────────┐
   path ──hash────► │    TVFS     │──► EKey ─┐
                    └─────────────┘          │
                          ▲                  │
                          │ (vfs-root EKey)  │
   CKey ──► encoding table ──► EKey ─────────┤
                                             ▼
                                   ┌──────────────────┐
                                   │ local index .idx │  EKey(9-byte)
                                   │  → archive, off  │
                                   └──────────────────┘
                                             │
                                             ▼
                              data.NNN @ offset, `size` bytes
                                   ┌──────────────────┐
                                   │ archive envelope │ 30-byte header
                                   │   → BLTE blob    │
                                   └──────────────────┘
                                             │ BLTE decode
                                             ▼
                                      logical bytes
```

Bootstrap order (each step names the keys for the next):
`.build.info` → build configuration → { encoding table, `vfs-root`
TVFS } ; the local index + archives are read directly from
`Data/data`. Sections 4–11 follow this dependency order.

## 4. `.build.info`

UTF-8 text file at the installation root. A `|`-delimited table: row 0 is
the header (`Name!TYPE:len` cells); each following row is an installed
build. Select the row whose `Active` column is `1`.

| Column | Use |
|---|---|
| `Build Key` | hex key of the build configuration (§5) |
| `CDN Key` | hex key of the CDN configuration |
| `Version` | build version string |
| `Tags` | install tags (locales/platforms) |
| `Product` | product code (e.g. the D4 product) |

## 5. Build configuration

Plain text at `Data/config/<k0k1>/<k2k3>/<buildKey>` (lowercase hex; the
first two and next two hex characters of the key form the directory
path). Lines are `key = v1 [v2 …]`; `#` begins a comment.

| Key | Value |
|---|---|
| `encoding` | `<CKey> <EKey>` of the encoding table (§9) |
| `root` | content key of the root (all zeroes for Diablo IV — it uses TVFS) |
| `vfs-root` | `<CKey> <EKey>` of the TVFS root manifest (§10) |
| `vfs-1` … `vfs-N` | `<CKey> <EKey>` of nested TVFS sub-manifests |
| `install`, `download`, `size` | other manifests (`<CKey> <EKey>`) |

## 6. Local index (`Data/data/*.idx`)

Sixteen bucket files named `00*.idx … 0f*.idx`; within a bucket use the
**lexicographically last** file. Open share read+write (§2).

### 6.1 File structure

| Offset | Type | Field |
|---|---|---|
| 0 | i32 | `HeaderHashSize` |
| 4 | i32 | `HeaderHash` |
| 8 | byte[`HeaderHashSize`] | header (version, EKey/size field widths, …) |
| `pad` | — | pad to `((8 + HeaderHashSize + 0x0F) & ~0x0F)` |
| `pad`+0 | i32 | `EntriesSize` |
| `pad`+4 | i32 | `EntriesHash` |
| `pad`+8 | entry[`EntriesSize / 18`] | the index entries (§6.2) |

### 6.2 Index entry (18 bytes)

| Offset | Type | Field |
|---|---|---|
| 0 | byte[9] | EKey prefix (first 9 bytes of the EKey) |
| 9 | u8 | `indexHigh` |
| 10 | u32 **BE** | `indexLow` |
| 14 | i32 | `size` — archive-envelope length (§7) |

Derived location:

```
archiveIndex = (indexHigh << 2) | ((indexLow & 0xC0000000) >> 30)
offset       =  indexLow & 0x3FFFFFFF
```

The index is keyed on the **9-byte EKey prefix**; on duplicate keys the
**first** entry wins.

## 7. Archive envelope (`Data/data/data.NNN`)

`data.NNN` is `data.` followed by `archiveIndex` (§6.2). Seek `offset`,
read `size` bytes:

| Offset | Type | Field |
|---|---|---|
| 0 | byte[16] | EKey, byte-reversed (its first 9 bytes match the requested EKey) |
| 16 | i32 | `size` (equals the index `size`) |
| 20 | byte[10] | reserved |
| 30 | byte[`size`-30] | the BLTE blob (§8) |

## 8. BLTE

The container/codec of every stored blob.

```
+0  byte[4]  'BLTE'  = 0x42 0x4C 0x54 0x45
+4  u32 BE   headerSize        ; 0 ⇒ a single header-less chunk
   if headerSize > 0:
+8  u8       0x0F
+9  u24 BE   chunkCount
+12 chunk-table : chunkCount × { u32 BE compSize ; u32 BE decompSize ; byte[16] md5 }
   then the chunk bodies, back-to-back.
```

Each chunk body is `byte mode` then the mode's payload:

| `mode` | Meaning |
|---|---|
| `'N'` | stored verbatim |
| `'Z'` | zlib (RFC-1950) stream |
| `'F'` | a nested BLTE blob (recurse) |
| `'E'` | encrypted (Salsa20/ARC4) — requires a TACT key (not shipped) |

**Header-less form** (`headerSize == 0`): everything after the 8-byte
magic+headerSize is one chunk (mode byte then body).

## 9. Encoding table (BLTE-decoded)

Maps a content key (CKey) to its encoding key(s) (EKey). Header is
**big-endian** in its multi-byte fields:

| Offset | Type | Field |
|---|---|---|
| 0..1 | byte[2] | `'EN'` |
| 2 | u8 | version (`1`) |
| 3 | u8 | `CKeyLength` (`16`) |
| 4 | u8 | `EKeyLength` (`16`) |
| 5..6 | u16 BE | `CKeyPageSize` (KiB) |
| 7..8 | u16 BE | `EKeyPageSize` (KiB) |
| 9..12 | u32 BE | `CKeyPageCount` |
| 13..16 | u32 BE | `EKeyPageCount` |
| 17 | u8 | `unk1` |
| **18..21** | **u32 BE** | **`ESpecBlockSize`** (at byte 18 — Appendix A, CL-1) |
| 22 | byte[`ESpecBlockSize`] | ESpec string block |
| … | — | CKey page index: `CKeyPageCount × 32` bytes (skipped) |
| … | — | CKey pages (§9.1) |

### 9.1 CKey pages

Pages are padded to a 4096-byte boundary. Per page:

```
while (keyCount = u8) != 0:
    u40 BE       fileSize
    byte[CKeyLength] cKey
    keyCount ×   byte[EKeyLength] eKey      ; the first eKey is the wanted one
(a 0 keyCount byte terminates the page)
remaining = 4096 - ((pos - chunkStart) % 4096)
if remaining == 0xFFF:  pos -= 1 ; page++ ; continue
else:                   pos += remaining
```

## 10. TVFS (`vfs-root`, BLTE-decoded)

The path→content file system. The `vfs-root` EKey comes from the build
configuration (§5).

### 10.1 Directory header

| Offset | Type | Field |
|---|---|---|
| 0..3 | u32 (LE) | `'TVFS'` = `0x53465654` |
| 4 | u8 | `formatVersion` (`1`) |
| 5 | u8 | `headerSize` |
| 6 | u8 | `eKeySize` (`9`) |
| 7 | u8 | `patchKeySize` (`9`) |
| 8..11 | i32 BE | `flags` |
| 12..15 | i32 BE | `pathTableOffset` |
| 16..19 | i32 BE | `pathTableSize` |
| 20..23 | i32 BE | `vfsTableOffset` |
| 24..27 | i32 BE | `vfsTableSize` |
| 28..31 | i32 BE | `cftTableOffset` |
| 32..35 | i32 BE | `cftTableSize` |
| 36..37 | u16 BE | `maxDepth` (an EST table follows; unused) |

```
cftOffsSize = 4 if cftTableSize > 0xFFFFFF
              3 if          > 0xFFFF
              2 if          > 0xFF
              1 otherwise
```

### 10.2 Path-table walk (recursive)

A leading `0xFF` + folder `nodeValue` prelude on the whole path table is
skipped before walking. Each entry, in order:

1. optional `0x00` ⇒ **pre-separator**;
2. `len` + `byte[len]` **name** (absent if the next byte is `0xFF`);
3. optional `0x00` ⇒ **post-separator**;
4. then either `0xFF` + `i32 BE nodeValue` (a **node**) or an implicit
   post-separator.

Node value:

- `nodeValue & 0x80000000` ⇒ **folder**: recurse into the next
  `(nodeValue & 0x7FFFFFFF) - 4` bytes with the appended name as a path
  prefix; rewind the path on return.
- otherwise ⇒ **file**: `nodeValue` indexes the VFS table:

  ```
  vfs[nodeValue] : u8 spanCount
                   spanCount × { i32 BE contentOffset
                                 i32 BE contentLength
                                 var(cftOffsSize) cftOffset }
  at cft[cftOffset] : the first eKeySize (9) bytes = the file's EKey
  ```

### 10.3 Sub-manifests

If a span's 9-byte EKey is one of the build configuration's
`vfs-root` / `vfs-N` EKeys, that file is itself a nested TVFS manifest:
read + BLTE-decode it and recurse, keeping the accumulated path prefix.

### 10.4 Path hashing

A file's lookup key is the 64-bit Jenkins *lookup3* (`hashlittle2`) of
the assembled path, normalized `/`→`\` and ASCII upper-cased, returned as
`(c << 32) | b`. (Implemented as `CascPathHash`; named for its role, not
the algorithm's author — attribution in `NOTICE`.)

## 11. Read path (algorithm)

```
by path:  path → CascPathHash → TVFS → EKey
                                       → local index (9-byte prefix)
                                       → archive envelope → BLTE → bytes
by CKey:  CKey → encoding table → EKey → (as above from "local index")
```

The Diablo IV game module layers SNO addressing on top of this (a SNO
resolves to a TVFS path); see `casc-diablo4-format.md`.

---

## Appendix A — correction log (transport errata)

What was found wrong/omitted during empirical implementation, and the
true value. The structure sections above already state the corrected
truth; this log records the history.

- **CL-1 — encoding header `ESpecBlockSize` offset.** It is at byte
  **18** (after the 1-byte `unk1` at 17), a BE `u32` (§9). An earlier
  implementation read it at 17, which misaligned the entire CKey-page
  region — the table still parsed *some* entries (a naive size check
  passed) but real content-key lookups failed. Verified by a closed-loop
  test: an `install` CKey → encoding → EKey must be present in the local
  index.
- **CL-2 — transport not in the upstream record.** The upstream
  `d4-binary-formats.md` omits the CASC transport entirely (it delegated
  to a third-party library). §§4–10 are the clean-room-verified
  specification of that omitted layer, proven against build
  `3.0.2.71886`.
- **CL-3 — file sharing.** `.idx` and `data.NNN` must be opened
  share-read+write; the live game / Battle.net agent holds them open. A
  plain read share throws an I/O error.

## Appendix B — source & re-verification

- Clean-room from public TACT/CASC documentation (wowdev.wiki),
  cross-checked against `THIRD-PARTY.md`. No third-party source
  incorporated.
- Verified against Diablo IV build `3.0.2.71886` (`.build.info` Build
  Key `522f2f30f1eb0e32af225966b8ac91d1`).
- **Re-verify trigger:** the `.build.info` Build Key changes (seasonal
  patch). Re-run the integration tests; update Appendix A on any drift.
