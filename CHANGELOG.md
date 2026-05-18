# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
Semantic Versioning once it reaches `1.0.0`.

## [0.2.0-alpha] — 2026-05-17

Initial public prerelease. A modern .NET library for Blizzard's CASC
content stack with a Diablo IV module. All Diablo IV formats were
decoded clean-room and verified against a live install (`3.0.2.71886`);
the library returns raw decoded fields only — no formula evaluator,
scoring, or imaging (interpretation stays with the consumer).

### WiseOwl.Casc — CASC/TACT/TVFS/BLTE transport

- Opens a local Blizzard installation and resolves content by path or by
  content/encoding key: `.build.info` → build config → 16-bucket local
  `.idx` index → archive envelope → BLTE decode → encoding table → the
  TVFS path tree.
- Modern public API: `CascStorage`, typed `ContentKey` / `EncodingKey`
  value types, async reads, spans. Multi-targeted
  `netstandard2.0;net8.0;net10.0`.

### WiseOwl.Casc.Diablo4 — Diablo IV game module

- `Diablo4Storage` facade: SNO read-by-id (`ReadSno` / `TryReadSno` /
  `ReadGroup` / `OpenSno`), `CoreToc` name↔id↔group, shared-payload
  resolution, `SnoFolder` incl. `Child` sub-blobs
  (`Base\Child\<id>-<subId>`). Static `SnoPath` formatter.
- The `0x44CF00F5` combined-meta family: `TextureDefinition` with
  image-library-agnostic BC1/BC3 `DecodeMip0` → RGBA32, and the
  per-locale StringList catalog (`GetStrings` / `TryGetString`).
- Game-wide D4 hashes: `Diablo4.GbidHash`, `Diablo4.TypeHash`,
  `Diablo4.FieldHash`.
- **Paragon.** Typed record decoders `ParagonBoardDefinition`
  (incl. first-party `ClassSnoId` / `ClassSnoName` / `BoardIndex`),
  `ParagonNodeDefinition`, `ParagonGlyphDefinition` (incl.
  `UsableByClassSnoIds`), `ParagonGlyphAffixDefinition`,
  `AttributeFormulaTable`, plus `TryGetIconFrame`. The paragon UI
  render layout: `ReadUiScene` (generic `0xE4825AB8` widget graph) and
  `ReadParagonRenderLayout` (typed projection — canvas reference,
  node/disc/symbol geometry, rotation quadrant, the 18-row state
  matrix; unitless ratios + the derivation rule, the absolute
  resolution/zoom scale stays the consumer's).
- **Classes & records.** `ReadCharacterClasses(locale)` →
  `CharacterClass(SnoId, SnoName, DisplayName)`; typed
  `ReadPlayerClass` / `ReadPower` / `ReadAffix` / `ReadItem` (identity
  + locale-aware localized text). Localized board/class/power/affix/
  item names are resolved first-party; the `PlayerClass` SNO id is the
  stable class key shared across boards, glyphs, and classes.

### Packaging

- Both libraries publish `.nupkg` + symbol `.snupkg` with per-TFM
  assemblies and XML docs, per-package README, MIT license expression,
  Source Link, and correct dependency groups.
- Self-contained byte-format references:
  [`docs/casc-format.md`](docs/casc-format.md) (transport) and
  [`docs/casc-diablo4-format.md`](docs/casc-diablo4-format.md) (Diablo IV
  layer); complete generated API reference under
  [`docs/api/`](docs/api/).
