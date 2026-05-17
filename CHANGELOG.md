# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
Semantic Versioning once it reaches `1.0.0`.

## [0.1.1-alpha] — 2026-05-17

Maintenance prerelease: analyzer-clean build, one API refinement.

### Changed

- **`Diablo4Storage.SnoPath` is now `static`** (it is a pure path
  formatter and uses no instance state). Breaking vs `0.1.0-alpha`:
  call `Diablo4Storage.SnoPath(...)` instead of `instance.SnoPath(...)`.
  Acceptable as a pre-1.0 prerelease refinement.

### Fixed

- Resolved all analyzer warnings. `0.1.0-alpha` shipped with 6
  (`CA1711` on `NodeAttribute` and `CA1822` on `SnoPath`, each ×3
  TFMs) — the earlier "0 warnings" status was inaccurate. `NodeAttribute`
  keeps its name with a documented `CA1711` suppression (it is the
  serialized `eAttribute` domain term and matches the byte-format spec /
  ARTICLE-SOURCE vocabulary; it is a data record, not a `System.Attribute`).

### CI

- Bumped GitHub Actions to Node-24 majors (`actions/checkout@v6`,
  `actions/setup-dotnet@v5`) ahead of the 2026-06-02 Node-20
  runner deprecation.

## [0.1.0-alpha] — 2026-05-16

Initial release. (Note: built with 6 analyzer warnings; see
`0.1.1-alpha` above. No functional defect.)

### WiseOwl.Casc — CASC/TACT/TVFS/BLTE transport

- Opens a local Blizzard installation and resolves content by path or by
  content/encoding key: `.build.info` → build config → 16-bucket local
  `.idx` index → archive envelope → BLTE decode → encoding table → the
  TVFS path tree.
- Modern public API: `CascStorage`, typed `ContentKey`/`EncodingKey` value
  types, async reads, spans. Multi-targeted
  `netstandard2.0;net8.0;net10.0`.
- Verified end to end against a live Diablo IV install
  (`3.0.2.71886`), including a real ~100 MB multi-chunk `encoding` file
  and the closed-loop CKey→EKey→index path.

### WiseOwl.Casc.Diablo4 — Diablo IV game module

- `Diablo4Storage` facade: SNO read-by-id (`ReadSno`/`TryReadSno`),
  `CoreToc` name↔id↔group, shared-payload resolution, and
  `ReadGroup` streaming.
- The `0x44CF00F5` combined-meta family: `TextureDefinition` with
  image-library-agnostic BC1/BC3 `DecodeMip0` → RGBA32, and the
  per-locale StringList catalog (`GetStrings`/`TryGetString`).
- Typed record decoders (raw fields only — no formula evaluator, no
  scoring; interpretation stays with the consumer):
  `ParagonBoardDefinition`, `ParagonNodeDefinition`,
  `ParagonGlyphDefinition`, `ParagonGlyphAffixDefinition`,
  `AttributeFormulaTable`, plus `TryGetIconFrame` and
  `Diablo4.GbidHash`.

### Packaging & documentation

- Both libraries publish `.nupkg` + symbol `.snupkg` with per-TFM
  assemblies and XML docs, per-package README, MIT license expression,
  Source Link, and correct dependency groups.
- Self-contained byte-format references:
  [`docs/casc-format.md`](docs/casc-format.md) (transport) and
  [`docs/casc-diablo4-format.md`](docs/casc-diablo4-format.md) (Diablo IV
  layer), each with its own correction log.
- Complete generated API reference under [`docs/api/`](docs/api/) (CI
  drift-guarded). See [`docs/devlog/`](docs/devlog/) for the narrative of
  how each piece was built and why.
