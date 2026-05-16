# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
Semantic Versioning once it reaches `1.0.0`.

## [Unreleased]

### Added
- Initial repository scaffold: `.slnx` solution, `WiseOwl.Casc` (multi-TFM
  core), `WiseOwl.Casc.Diablo4`, test projects, runnable sample, CI.
- Clean-room **Diablo IV byte parsers**: `SnoGroup`, `DataType` primitive
  decoders, `SnoFile` reader (16-byte header + payload base `0x10`),
  `CoreToc` (new-format `0xBCDE6611` header), and the `0x44CF00F5`
  combined-meta bundle / `TextureDefinition` parser — proven against a real
  extracted `CoreTOC.dat`.
- Clean-room **CASC transport** foundation in `WiseOwl.Casc`: modern public
  API surface (`CascStorage`, `ContentKey`/`EncodingKey`, async streams),
  `.build.info` / build-config parsing, local `.idx` index, BLTE decoder.

### Proven (vs live Diablo IV `3.0.2.71886`)
- Full lower transport end to end: `.build.info` → build config →
  16-bucket local `.idx` → archive envelope → BLTE (real ~100 MB
  multi-chunk `encoding`) → encoding table → closed-loop CKey→EKey→index.
- Clean-room **TVFS** resolves + reads `Base\CoreTOC.dat` and
  `Base\Texture-Base-Global.dat`.
- **CoreTOC `0xBCDE6611`**: 849,257 SNOs / 181 groups (stock CascLib NuGet
  overflows on this file; this parser does not).
- **Combined-meta `0x44CF00F5`**: 140,197 texture definitions;
  `2DUI_ParagonNodes` → BC3 4224×192, 31 ptFrames.
- 12 tests pass, 1 documented skip; solution builds with 0 warnings.

### Resolved — per-SNO read by id (the ParagonOptimizer migration blocker)
- D4 addresses SNO content in TVFS by **`Base\<Folder>\<id>`** (numeric id;
  no group/name/extension) — not the name-path, not `base:meta\<id>`. The
  TVFS walk was already complete (1,759,690 entries). See correction CL-4.
- `Diablo4Storage.ReadSno`/`TryReadSno` (Meta + Payload), throwing
  `SnoNotFoundException` vs. non-throwing `TryReadSno` for skippable
  absent SNOs.
- `0xABBA0003` `CoreTOCSharedPayloadsMapping` parsed and applied as a
  transparent payload-alias fallback (`SharedPayloads`,
  `TryGetSharedPayloadSource`).
- Image-library-agnostic `TextureDefinition.DecodeMip0` → raw
  straight-alpha RGBA32 `DecodedImage` (+ `Crop`); clean-room BC1/BC3.
- `SnoRecord.Ascii`/`AsciiAbsolute`; `CoreToc.TryGetId`/`GetId` name index;
  `CascStorage` archive `FileStream` handle cache for bulk by-id reads.
- 14 tests pass, 0 skipped. All P0/P1 consumer feature requests
  (FR-1…FR-9) adopted and proven; see `docs/devlog/0002`. `docs/casc-format.md`
  documents the library/consumer boundary (FR-5) and the deferred
  `CoreTOCReplacedSnosMapping` (FR-6).

### Round-3 — typed D4 record readers (B1–B6) + spec authority
- Converged design (consumer requirements + casc pushback, owner-approved).
  Library now owns typed *record decoding* (raw fields); ships **no
  formula evaluator** (decided — a format library is not an arithmetic
  engine). Evaluation + 6 calibrated intrinsics + scoring + JSON schema
  stay permanently with the consumer.
- `ParagonBoardDefinition`, `ParagonNodeDefinition` (+`NodeAttribute`
  with `NParam`/`ParamPlus12`, `ParagonRarity`, `SnoPassivePower`,
  `HIcon`/`HIconMask`), `ParagonGlyphDefinition` (bounds-safe),
  `ParagonGlyphAffixDefinition`, `AttributeFormulaTable`
  (`eGameBalanceType==22`; `ByName`/`TryGetFormulaText`;
  `TryGetNameByGbid` keyed on `GbidHash`); `Diablo4Storage.Read*` +
  `TryGetIconFrame` (first-party `hIconMask==ImageHandle`).
- Each type: static `Parse(ReadOnlySpan<byte>)` + facade. CI-safe
  synthetic tests + live §7 acceptance matrix (verbatim): board
  2458674→W21/441; node 678776 sig 0xDEADBEEF; GameBalance 201912 =
  1038 entries, `ParagonNodeCoreStat_Normal`→"5", `_Magic`→"7";
  `GbidHash`==0x42C16A1B. 27 tests pass, 1 unrelated skip, 0 warnings.
- **Spec authority transferred:** `docs/casc-format.md` re-scoped to the
  single canonical CASC + Diablo IV byte-format reference (new §§10–14
  D4 SNO/record/texture layer + §15 provenance map + CL-8). Upstream
  `d4-binary-formats.md` §3–§8.15 superseded for layouts (frozen).
  Policy carve-out referenced, never absorbed. Library scope FROZEN at
  "B1–B6 + existing". Narrative: `docs/devlog/0004`.

### Round-2 consumer feature requests (future / non-blocking)
- FR-11: `SnoGroup` names `Power/Item/PlayerClass/ItemType/Affix`;
  `Diablo4Storage.ReadSno(int groupId,…)`/`TryReadSno` int escape hatch.
- FR-12: `static uint Diablo4.GbidHash(string)` (case-insensitive DJB2;
  verified `== 0x42C16A1B`); GameBalance fully enumerable by group.
- FR-14: `SnoFolder.Child`/`PayLow`/`PayMed` served by the same id-keyed
  resolver (mechanism proven; a concrete Child id is gated on the deferred RE).
- FR-15: `Diablo4Storage.ReadGroup(group, folder)` streaming, reuses
  resident index/encoding/archive handles.
- FR-16: library/consumer boundary reinforced (no typed game-record APIs).
- FR-13 (StringList localized names): **reverse-engineered, implemented &
  proven** (RE workstream completed). Per-locale `0x44CF00F5` bundle
  `base/StringList-Text-<locale>.dat` (same container family as the texture
  catalog; body at `B=alignUp8(prevEnd)`, no `+8`, SNO positional from
  index; `infoLength@B+20`, 40-byte entries, UTF-8). Validated bundle-wide
  (58,286 tables / 175,014 strings). API: `Diablo4Storage.GetStrings(
  locale)` → `StringListCatalog` (cached) + `TryGetString`. Full spec:
  `docs/casc-format.md §9` + CL-7; narrative `docs/devlog/0003`.

See [`docs/devlog/`](docs/devlog/) for the narrative of how each piece was
built and why, and [`docs/casc-format.md`](docs/casc-format.md) for the
self-contained transport spec.
