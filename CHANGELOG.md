# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
Semantic Versioning once it reaches `1.0.0`.

## [0.3.0-alpha] — 2026-05-21

Adds the paragon-board render model, the power script-formula reader,
UI tile-style decode, and broader texture-atlas support. Decoded
clean-room and verified against a live install (`3.0.2.71886`); the
library returns raw decoded data only.

### WiseOwl.Casc.Diablo4 — paragon render model

- `ReadParagonNodeRecipe()` → `ParagonNodeRecipe` /
  `ParagonNodeRecipeLayer`: the engine's ordered, z-sorted per-node
  render program (verbatim widget names as predicate keys), with
  `NodeSelectionDiscs` exposing each rarity disc's unselected/selected
  pair.
- `ReadParagonBoardGrid()` → `ParagonBoardGrid`: the design-canvas
  extent, node-cell extent, and grid pitch.
- `ReadParagonRenderModel()` → `ParagonRenderModel` /
  `ParagonRenderLayout` / `ParagonSceneModel`: the board render layout,
  the exhaustive per-scene atlas-binding model, per-rarity node
  composites, special-node (socket / start / gate) recipes, directional
  arrows + connectors, the per-node cell binding, and the available-glow
  overlay.
- `ParagonBoardChrome`: the 5-piece board chrome (centre field + four
  rim sides) and `TiledStyleBindings`.
- `ReadTiledStyle` / `TryReadTiledStyle` → `TiledStyleDefinition`
  (including the NSlice 9-slice variant); `SnoGroup.UiStyle`.
- `ReadUiScene()` → `UiScene`: the raw UI-scene widget graph (names,
  class ids, and bound field values).
- `Diablo4.KnownFieldNames` / `KnownTypeNames` + `FormatFieldHash` /
  `FormatTypeHash`: recovered field/type names for decoded hashes.

### WiseOwl.Casc.Diablo4 — power script formulas

- `PowerDefinition.ScriptFormulas` (`PowerScriptFormula`),
  `ResolvedFormulas` (`SF_N → value`), `FunctionRefs`
  (`PowerFunctionRef`), and `CompiledFormulas` (decoded compiled-form
  AST). Raw decoded values only.

### WiseOwl.Casc — transport

- `DecodeMip0` supports BC1/BC3 atlases at any stored block-row
  alignment (the row pitch is determined per texture).

### Notes

- Public API for the surfaces above is frozen for this release.

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
