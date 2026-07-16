# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
Semantic Versioning once it reaches `1.0.0`.

## [Unreleased]

## [0.5.0] — 2026-07-16

Adds the item and aspect **affix** data layer — what each affix does, and the
formula behind its rolled value — plus gear/item taxonomy, the Character-Sheet
stat inputs, no-argument install auto-detection, and names for the game's
conditional attributes. Continues to track the live game through Season 14
(build `3.1.1.72836`).

### Added

- **Install auto-detection** — `Diablo4Storage.Open()` (no arguments) now finds
  the Diablo IV installation automatically from the Windows registry (or the
  `WISEOWL_CASC_INSTALL` override), so consumers no longer have to hard-code the
  path. `TryLocateInstall` exposes the discovered path directly. The
  explicit-path overload stays for custom or non-Windows installs.
- **Gear/item taxonomy** — classify and enumerate items by category. Every item
  now exposes its base type (weapon / armor / jewelry / charm / other), decoded
  structurally from the game data; `EnumerateItems(ItemClass.Weapon)` lists every
  weapon in the game, `EnumerateItemTypes()` the full base-type dictionary, and
  the catalog gains an item-type `category` facet.
- **Character stat model** — the per-class rules that turn core attributes
  (Strength / Intelligence / Willpower / Dexterity) into Character-Sheet stats.
  For any class, read which core feeds its Skill Damage, Critical Strike
  Chance, and Resource Generation, plus the universal per-point conversion
  rates (Armor, Resistances, Dodge, Healing, …) and the inherent base stats —
  the inputs a build planner needs to compute a character's derived stats.
- **Affix effects** — every item and aspect affix now exposes which
  attribute(s) it modifies. `AffixDefinition.Effects` lists each modified
  attribute with its resolved display name and any parameter (a resistance's
  element, a skill tag), so a build planner can see what an aspect actually
  does — including multi-stat affixes like a dual-element resistance.
- **Affix values** — each affix effect exposes the formula that determines its
  rolled magnitude by item power: `AffixEffect.FormulaGbid` resolves through the
  game's `AttributeFormulas` table (`AttributeFormulaTable.TryGetByGbid`) to the
  per-item-power roll expression (e.g. a critical-chance affix's
  `FloatRandomRangeWithInterval(1,3,3.5)/100` at high item power). Set, mythic,
  and unique powers additionally expose their fixed numbers via
  `AffixDefinition.StaticValues`. Evaluating a formula stays the consumer's, as
  for paragon magnitudes.
- **Conditional-attribute names** — `Diablo4Storage.TryGetDataAttributeName`
  resolves the conditional/seasonal attributes (Berserking, Shadowform,
  Demonform, Volatile, kill-streak, per-power bonuses) that some nodes, glyph
  affixes, and item affixes reference through a separate designer table. These
  carry a high-bit flag on their attribute id; the new method reads their name
  where `GetAttributeName` (the engine-attribute resolver) returns null.

### Fixed

- **Wrong attribute names** — `GetAttributeName` no longer returns a stale,
  confidently-wrong name for an attribute id the game has renumbered across
  seasons (e.g. a damage-while-Healthy affix resolving to "Barrier
  Generation"). Renumbered ids now resolve to the correct current name or an
  honest null, never a wrong one. Its documentation is refreshed to match.

## [0.4.0] — 2026-07-15

First stable release. Adds a full Diablo IV Paragon data layer on top of the
raw decoders and keeps up with the live game through Season 14 (build
`3.1.1.72836`).

### Added

- **Paragon nodes** — read a node's name, localized title, icon, passive
  power, and full stat list (each stat's value, unit, and magnitude formula)
  in one call, plus a whole-board lookup for build planners.
- **Paragon glyphs** — glyph and glyph-affix display names, descriptions, and
  structured affix effects.
- **Tooltip chrome** — the panel, frame, divider, and skill-icon artwork the
  game composes behind a Paragon tooltip.
- **Attribute names** — resolve a stat's display name in any locale. The
  lookup follows the live game data, so it keeps working across seasons as the
  game renumbers its attributes.
- **Affix names** — the localized display name of any item or aspect affix.
- **Power formulas** — the resolved values behind a legendary power's tooltip.

### Changed

- Verified against Diablo IV Season 14 (`3.1.1.72836`).

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
