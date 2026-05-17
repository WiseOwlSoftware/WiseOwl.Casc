# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
Semantic Versioning once it reaches `1.0.0`.

## [0.1.0-alpha] — 2026-05-16

Initial release.

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
