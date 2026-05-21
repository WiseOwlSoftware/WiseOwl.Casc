# WiseOwl.Casc assembly

## WiseOwl.Casc namespace

| public type | description |
| --- | --- |
| class [CascContentNotFoundException](./WiseOwl.Casc/CascContentNotFoundException.md) | The requested content could not be located in this storage. |
| class [CascEncryptedContentException](./WiseOwl.Casc/CascEncryptedContentException.md) | The content is encrypted with a key this library does not have. BLTE `'E'` (Salsa20) chunks require a TACT key the consumer has not supplied. |
| class [CascException](./WiseOwl.Casc/CascException.md) | Base type for all errors raised by WiseOwl.Casc. |
| class [CascFormatException](./WiseOwl.Casc/CascFormatException.md) | The on-disk CASC layout could not be understood (corrupt or an unsupported format/version was encountered). |
| record [CascOpenOptions](./WiseOwl.Casc/CascOpenOptions.md) | Options for opening a [`CascStorage`](./WiseOwl.Casc/CascStorage.md). All properties are `init`-only; use a collection-style initializer. |
| class [CascStorage](./WiseOwl.Casc/CascStorage.md) | An opened, local Blizzard CASC storage. This is the game-agnostic transport entry point: it resolves content by [`EncodingKey`](./WiseOwl.Casc/EncodingKey.md) or [`ContentKey`](./WiseOwl.Casc/ContentKey.md), reads the archive envelope, and BLTE-decodes it. Game-specific name/SNO resolution lives in the game modules (e.g. `WiseOwl.Casc.Diablo4`). |
| struct [ContentKey](./WiseOwl.Casc/ContentKey.md) | A content key (CKey): the MD5 of a file's logical, fully-decoded bytes. The game's `root`/TVFS maps game-meaningful names to CKeys; the `encoding` table maps a CKey to one or more [`EncodingKey`](./WiseOwl.Casc/EncodingKey.md)s. |
| struct [EncodingKey](./WiseOwl.Casc/EncodingKey.md) | An encoding key (EKey): identifies the BLTE-encoded, stored form of content (compressed and possibly encrypted). CASC archives and local indices are addressed by EKey (the local index keys on the first 9 bytes). |
| struct [Md5Key](./WiseOwl.Casc/Md5Key.md) | A 128-bit CASC key (the MD5-derived hash CASC uses to address content). Stored as two UInt64 halves so the type is a small, allocation-free value with O(1) equality. |
| class [SnoNotFoundException](./WiseOwl.Casc/SnoNotFoundException.md) | A specific SNO (by group/id and folder) could not be resolved. Distinct from [`CascContentNotFoundException`](./WiseOwl.Casc/CascContentNotFoundException.md) so callers can cleanly tell "this SNO legitimately has no such content — skip it" from a real transport failure. |

## WiseOwl.Casc.Configuration namespace

| public type | description |
| --- | --- |
| class [BuildConfiguration](./WiseOwl.Casc.Configuration/BuildConfiguration.md) | A parsed CASC build configuration file (found at `Data/config/<k0k1>/<k2k3>/<buildKey>`). It is a simple `key = value [value …]` text file (`#` comments) naming the content/encoding keys of the storage's core manifests: the `encoding` table, the `root`, and the `vfs-root` / `vfs-N` TVFS manifests. |
| class [BuildInfo](./WiseOwl.Casc.Configuration/BuildInfo.md) | The parsed `.build.info` file at the root of a local Blizzard installation. It is a pipe-delimited table: a header row of `Name!TYPE:len` column descriptors, then one row per installed build. WiseOwl.Casc reads the active row and exposes the few fields the transport needs (build/CDN config keys, version, tags). |

## WiseOwl.Casc.Encoding namespace

| public type | description |
| --- | --- |
| static class [Blte](./WiseOwl.Casc.Encoding/Blte.md) | Decoder for the BLTE container ("Block Table Encoded"), the chunked codec wrapping every stored CASC blob. A blob is a `'BLTE'` magic, an optional chunk table, then one or more chunks; each chunk's first byte is its mode: `'N'` raw, `'Z'` zlib, `'F'` recursive BLTE, `'E'` encrypted (Salsa20/ARC4). |
| class [EncodingTable](./WiseOwl.Casc.Encoding/EncodingTable.md) | The CASC encoding table: maps a [`ContentKey`](./WiseOwl.Casc/ContentKey.md) (the hash of a file's logical bytes) to the [`EncodingKey`](./WiseOwl.Casc/EncodingKey.md)(s) of its stored, BLTE-encoded form(s). The table is itself stored as a BLTE blob; its own key comes from the build configuration. |

## WiseOwl.Casc.Indices namespace

| public type | description |
| --- | --- |
| struct [ArchiveLocation](./WiseOwl.Casc.Indices/ArchiveLocation.md) | Where a stored blob lives in the local archive set: which `data.NNN` file, at what byte offset, and how long. |
| class [LocalIndex](./WiseOwl.Casc.Indices/LocalIndex.md) | The CASC local index: the 16 `Data/data/<bucket>*.idx` files that map a 9-byte [`EncodingKey`](./WiseOwl.Casc/EncodingKey.md) prefix to an [`ArchiveLocation`](./WiseOwl.Casc.Indices/ArchiveLocation.md). Together they index every blob physically present in the local `data.NNN` archives. |

## WiseOwl.Casc.Internal namespace

| public type | description |
| --- | --- |
| static class [Bytes](./WiseOwl.Casc.Internal/Bytes.md) | Tiny endian-aware primitive readers over ReadOnlySpan. CASC mixes little-endian (most fields) and big-endian (BLTE/index sizes), so both are first-class here. Shared by the transport and the game modules so byte-level parsing is single-source. |
| static class [CascPathHash](./WiseOwl.Casc.Internal/CascPathHash.md) | The 64-bit path hash CASC/TVFS uses to key file paths. The underlying mixing function is Bob Jenkins' public-domain `lookup3` hash (the `hashlittle2` variant, 96 bits of internal state) — the algorithm every CASC implementation uses for path lookup. The type is named for what it does here, not after the algorithm's author; the lineage is credited in the docs and `NOTICE`. |

## WiseOwl.Casc.Tvfs namespace

| public type | description |
| --- | --- |
| class [TvfsManifest](./WiseOwl.Casc.Tvfs/TvfsManifest.md) | A parsed TVFS (TACT Virtual File System) manifest: the path→content tree newer Blizzard titles (including Diablo IV) use instead of a classic root. Walking it yields, for every file, the lookup3 path-hash mapped to the encoding key the storage is addressed by. |

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.dll -->
