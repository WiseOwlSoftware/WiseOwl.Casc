# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
Semantic Versioning once it reaches `1.0.0`.

## [0.3.0-alpha] — 2026-05-21

Adds the full paragon-board render model, the power script-formula
reader, several decode corrections, and one texture-transport bug fix.
All decoded clean-room and verified against a live install
(`3.0.2.71886`); the library still returns raw decoded data only — no
scoring, imaging, or composition policy.

### WiseOwl.Casc — transport

- **Fixed `DecodeMip0` BC row-pitch for non-64-aligned atlases.** The
  stored BC block-row pitch is texture-specific (64- and 128-aligned
  both occur); it is now derived from the exact mip0 byte count rather
  than a hard-coded `Align(width, 64)`, which drifted the row stride and
  garbled every frame of a 128-aligned atlas (e.g. `2DUI_Paragon`,
  1208×1464, stored at a 1280-px pitch).

### WiseOwl.Casc.Diablo4 — paragon render model

- **Per-node render program:** `ReadParagonNodeRecipe()` →
  `ParagonNodeRecipe` / `ParagonNodeRecipeLayer` — the engine's ordered,
  z-sorted node state-widget layers (verbatim widget names as the
  consumer's predicate keys), with `NodeSelectionDiscs` splitting each
  rarity disc into its unselected/selected pair.
- **Board grid metric:** `ReadParagonBoardGrid()` → `ParagonBoardGrid`
  (design-canvas extent + node-cell extent + pitch), validated against
  the in-game measurement.
- **UI tile-style:** `ReadTiledStyle` / `TryReadTiledStyle` →
  `TiledStyleDefinition` (incl. the NSlice 9-slice variant) +
  `ParagonBoardChrome.TiledStyleBindings`; `SnoGroup.UiStyle`.
- **Board chrome + node composites** (FR-C8..C12): `ReadParagonRenderModel`,
  the exhaustive scene-binding model + completeness gate, the 5-piece
  board chrome, per-rarity node composites, special-node (socket / start
  / gate) recipes, directional arrows + connectors, the per-node cell
  binding, and the available-glow overlay.
- **Completed the UI-scene field grammar:** instance values bind via
  **either** a 56-byte `0x22` record **or** a 12-byte tag-2 block; the
  prior 0x22-only reader under-decoded tag-2-encoded fields (the board
  chrome centre's authored 1200² rect, sparse node fields). Parent
  widgets confine their field scan past nested anonymous child records.
- **Hash recovery:** `Diablo4.KnownFieldNames` / `KnownTypeNames` +
  `FormatFieldHash` / `FormatTypeHash` (the cumulative hash dictionary).

### WiseOwl.Casc.Diablo4 — power script formulas (FR-C13)

- `PowerDefinition` now surfaces `ScriptFormulas` (`PowerScriptFormula`
  slot table — literal vs expression), `ResolvedFormulas` (the resolved
  `SF_N → value` map), `FunctionRefs` (`PowerFunctionRef` engine-function
  references), and `CompiledFormulas` (the decoded compiled-form AST),
  cross-validated text-vs-binary. Raw decoded values only — the library
  still ships no general formula evaluator beyond these resolved slots.

### Notes

- Public API for the surfaces above is frozen for this release. See
  `docs/casc-diablo4-format.md` Appendix A (CL-23 … CL-49) for the
  decode-correction history and `docs/devlog/0016`–`0045` for the
  narrative.

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
