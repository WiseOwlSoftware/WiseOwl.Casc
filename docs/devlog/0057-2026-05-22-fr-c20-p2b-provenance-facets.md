# 0057 — FR-C20 P2b: provenance-marked categorical facets

2026-05-22 · CL-59 · branch `fr-c20-facets-p2b`

## Consensus

The Optimizer's P2b A/B answer: **A now (provenance-marked) + B as the upgrade.**
Facets are for discovery/filtering, not authored render, so name-convention is
an acceptable *convenience* here — provided each facet's provenance is honestly
marked (the opposite stakes from render predicates, where name-convention is
forbidden). Priority: **glyph→class and power→class**, `Decoded` if cheap; items
are off the optimizer's critical path.

## Built

- `Facet(string Key, string Value, FacetSource Source)` + `FacetSource`
  {`NameConvention`, `Decoded`, `SceneField`} — mirrors `NodeActivationSource`.
- `Catalog.Facets(AssetRef) → IReadOnlyList<Facet>`.
- `Catalog.FindByFacet(kind, key, value)` — filter a kind by a categorical facet.

Facets surfaced:
- **`ParagonGlyph` → `class`**, `Decoded`: `ParagonGlyphDefinition.UsableByClassSnoIds`
  → `CoreTOC` `PlayerClass` name (Barbarian/Druid/…). The consumer's prioritised
  facet. Decodes the glyph (cheap; 562 in the group).
- **`TextureAtlas` → `codec`**, `Decoded` (decode-free combined-meta).

## Honest gaps

- **Power → class has no cheap source.** Checked all three candidates:
  `PowerDefinition` decodes name/description/formulas but **no class**;
  `PlayerClass` decodes only `SnoId`/`EClass` (**no power list**); and power SNO
  names don't encode class (most of the 9,781 are engine/AI powers like
  `AI_Wander`, not class skills). So power→class would need RE of a skill-kit /
  balance linkage — not faked. Raised on #32.
- **Item type/rarity/class** are derivable `NameConvention`-style from the
  `<Type>_<Rarity>_<Class>_<NNN>` SNO names, but the consumer deprioritised
  items (≠ paragon critical path); reachable via `Find<ItemDefinition>` + `Where`
  meanwhile. Deferred until convenient (will land as `NameConvention`-provenance
  facets, upgradeable to `Decoded` under B if the fields are RE'd).

## Acceptance

`Catalog_discovers_and_retrieves_assets_by_kind_filter` extended: a glyph's
`class` facets are `Decoded`; `FindByFacet(ParagonGlyph, "class", <name>)`
returns glyphs. 51/51 Diablo4 tests green on `3.0.2.71886`.
