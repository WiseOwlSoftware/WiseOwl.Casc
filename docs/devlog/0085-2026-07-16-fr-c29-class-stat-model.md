# 0085 — FR-C29 Phase 1 delivered: Character-Sheet stat model

**Date:** 2026-07-16
**FR:** casc-fr#41 (FR-C29 — per-class character-stat derivation formulae)
**CL:** CL-89 · spec §12 · `PlayerClassDefinition` + `CharacterStatModel`

Follows the data-mine finding in devlog 0084 (Phase 1 coefficients are
engine-coded). Owner core-stat oracles turned that into a shippable Phase 1.

## What the oracles established

Owner captured live core-stat tooltips for four classes spanning all four
primary-attribute archetypes — Warlock (Willpower), Rogue (Dexterity),
Necromancer (Intelligence), Barbarian (Strength) — plus a high-Paragon Warlock
(Will 1876 → Skill Damage 234.6%, Int 930 → Resist 372, …).

**Two findings:**

1. **The coefficients are universal.** Every derived-stat rate is identical
   across classes (the data-mine already proved they're engine-coded — no SNO
   home). The high-Paragon capture pinned the small ones to clean values:
   Armor `2.0`/Str, Resist `0.4`/Int, Skill Damage `0.125%`/primary, Healing
   `0.035%`/Will, Dodge `0.006%`/Dex, Crit `0.0025%`, Resource Gen `0.005%`.
   Inherent base: Crit 5.0%, Crit Dmg 50%, Vuln 20%, Move 100%. Baked as
   `CharacterStatModel` constants (engine-constants pattern; re-verify trigger
   = a build whose tooltips disagree).

2. **The per-class map IS data.** What varies is *which core* feeds Skill
   Damage / Crit / Resource Generation. Found in the `PlayerClass` record:
   three `(coreIndex, weight)` arrays at payload `+0x40`/`+0x50`/`+0x60`, slot
   order [SkillDmg (weight 1.25), Crit, ResGen]. Decoded structurally by
   `PlayerClassDefinition` — matched all four oracle tooltips exactly, then
   decoded the other four classes from the same bytes.

## Why reading the array (not a rule) matters

The first three classes fit "Crit = the core opposite the primary in the
Str·Int·Will·Dex cycle." It's a **coincidence** — Druid, Paladin, and
Spiritborn all break it (and Spiritborn ≠ Rogue despite both being
Dexterity-primary). A rule would have shipped wrong maps for 3 of 8 classes.
The array gets all 8 right. (Discipline: `feedback_re-all-fields` /
structural-evidence-over-inferred-rules.)

## Shipped

- `CoreStat` / `DerivedStat` / `ConversionUnit` / `CoreStatConversion`.
- `CharacterStatModel` — universal coefficients + base constants.
- `PlayerClassDefinition.{PrimaryAttribute, CriticalStrikeAttribute,
  ResourceGenerationAttribute, StatConversions}` — the per-class table,
  decoded from the record. Placeholder records → empty map.
- Tests: `FR_C29_class_stat_conversion_map_is_structural` (invariants, all
  classes) + `FR_C29_class_maps_and_coefficients_pinned_to_build_3_1_1`
  (content-snapshot: the four oracle maps + coefficient round-trips).
- Recon: `SnoScan classstats`. Boundary (§12.3): library returns the typed
  table + base constants; consumer composes the numbers.

## Still open (honest boundary)

Phase 2 base-Life curve (anchors L1=50 / L60=860 / L70=1526, class-independent
— decodable next), Phase 3 Toughness/DR composites (engine-coded), Phase 4
discrete Torment multipliers (engine/UI-coded; `DifficultyTiers` is a
per-monster-level curve). Small note: skill-damage per point reads lower at
level 1 and plateaus at 0.125% by ~L60 — the endgame value is the API constant.
