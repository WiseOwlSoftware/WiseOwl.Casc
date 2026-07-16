# 0090 — LIB-3 R2: the affix value range (idx16 → item-power roll formula)

**Date:** 2026-07-16
**Work item:** casc-fr#45 (LIB-3 R2 — the Optimizer's value-range counter-round)
**CL:** CL-94 · `AffixEffect.FormulaGbid` + `AttributeFormulaTable.TryGetByGbid` + `AffixDefinition.StaticValues`

CL-92 shipped *which* attribute an affix modifies and recorded "the min/max
roll range is not a literal pair in the record — it's item-power-curve driven,
keyed by the `idx16` GBID." The Optimizer accepted the negative result but
reopened the FR: the *value range is the half their users ask about* (their KB
prints `[roll]` across 305 uniques / 596 aspects). This slice decodes it.

## idx16 keys the AttributeFormulas table

The modifier `idx16` (byte `+64`) is the **`GbidHash` of an `AttributeFormulas`
entry** (SNO 201912 — the same GameBalance table the paragon nodes reference by
`FormulaGbid`, §8). The library already read that table; adding a direct
`AttributeFormulaTable.TryGetByGbid(gbid, out AttributeFormula)` closes the
loop. The match is exact and semantic:

| affix idx16 | AttributeFormulas entry | primary formula (low item power) |
|---|---|---|
| `0xF36193E0` (CritChance) | `GearAffix_CritChance` | `FloatRandomRangeWithInterval(1,0.5,1)/100` |
| `0x6B8CFC3C` (flat core stat) | `AffixCoreStat1x` | `(2 + ROUND((16/530)*(IPower()-10)) + FloatRandomRangeWithInterval(3,1,3)) - 2` |
| `0xDDAC2A95` (attack speed) | `GearAffix_AttackSpeed` | `(1.9 + 0.1*FloatRandomRangeWithInterval(31,1,31))/100` |
| `0xA1D941E6` (resistance) | `AffixInversePercentage2.33x` | `(1.5 + ROUND((8/510)*(IPower()-10)) + RandomInt(...)) / 100` |

The entry's `arRanges` carry one formula per `ItemPowerRangeStart`, so a
consumer computes the min/max a UI prints by **evaluating** the range's text at
a given `IPower()`. The library exposes the raw formula (it never evaluates —
the same boundary as the paragon magnitudes). So the value range is
**data-driven and decodable, not engine-coded** — the good terminal state.

## Static Values — the fixed set/unique numbers

Separately, the `"Static Value N"` `float32` VLA at fixed struct `+0xC0`
(`count = size/4`) holds the non-rolled scalars set/mythic/unique powers carry,
positionally matching the `Desc`'s `[Affix."Static Value N"]` placeholders
(`SetPower_Barb01_01 → [100 Fury, 50%, 20%, 120%]`). Shipped as
`AffixDefinition.StaticValues` — high value on its own (the exact mythic/unique
numbers a KB wants), empty for the rollable stat affixes.

## Closed the negative-id name gap

CL-93 added `TryGetDataAttributeName` but `AffixEffect.AttributeName` was still
empty for flag-namespaced (negative) ids. `ReadAffix` now routes negatives to
`TryGetDataAttributeName`, so a Berserking/Demonform effect resolves its
designer token (`Barb_Berserking_AttackSpeed`) instead of empty —
`IsDataDefinedAttribute` still flags it as a token, not a display name.

## Shipped

- `AffixEffect.FormulaGbid` (idx16) + `NoFormula` sentinel.
- `AttributeFormulaTable.TryGetByGbid(uint, out AttributeFormula)` — direct
  GBID → entry (name + per-item-power ranges), for affix *and* node formula refs.
- `AffixDefinition.StaticValues` (`IReadOnlyList<float>`).
- `AffixEffect.AttributeName` populated for negative ids (via CL-93's resolver).

Tests (live `3.1.1.72836`, content-snapshot): `ReadAffix_exposes_value_formula_
and_static_values` — crit → `GearAffix_CritChance`; SetPower → `[100,50,20,120]`;
Berserk → `Barb_Berserking_AttackSpeed`. 151/151 green. Spec §11.3; Appendix A
CL-94.
