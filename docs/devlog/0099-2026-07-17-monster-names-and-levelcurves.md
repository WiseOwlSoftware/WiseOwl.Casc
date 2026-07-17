# 0099 — MonsterNames registry (FR-C35) + the MonsterLevelCurves non-curve (FR-C36)

**Date:** 2026-07-17
**Work item:** casc-fr#52 (FR-C35), casc-fr#53 (FR-C36)
**CL:** CL-105 · `Diablo4Storage.ReadMonsterNames` + `MonsterNameRegistry`

The Optimizer took me up on two monster tables I'd mapped + offered on #50.

## FR-C35 — MonsterNames: a real reader

The elite-monster naming affixes. The GameBalance registry (44325) holds the
tokens; the localized text is in the name-matched **`MonsterNames` StringList**
(group 42, 1,277 labels): `FrozenSuffix004` → "Frostburn",
`ElectricLanceSuffix001` → "Boltrend", `VampiricEnrageSuffix003` → "Bloodrazor".
The game composes an elite name from a base + prefix and/or suffix fragment — the
same pattern as affix display names. Shipped `ReadMonsterNames(locale)` →
`MonsterNameRegistry` (`Fragments`/`Prefixes`/`Suffixes`), each a
`MonsterNameFragment{Token, Text, Kind}`. Prefix/suffix is inferred from the
token spelling (honest — the composition rule is engine-side). Validated vs the
live game.

## FR-C36 — MonsterLevelCurves: a name registry, not a curve

The Optimizer asked to type it "by name with their per-level values." RE finding:
there are no per-level values. The table (1610053) is a 6-entry name registry
(`Raid_Tier_0..5`, 6 × 320 B) whose records are near-empty — the tier name plus
placeholder `1.0` floats, **identical between `Tier_0` and `Tier_5`**. It's the
same shape as `AffixFamilyList` / `TemperRecipeFamily` (name registries). The
actual per-monster-level scaling is `DifficultyTiers` (§8.3). So the honest
deliverable is the finding, not a reader — "not in the data" recorded with
evidence, not papered over. If the six tier names alone are wanted, they're a
trivial follow-up.
