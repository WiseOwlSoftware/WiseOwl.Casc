# 0088 — LIB-3: item/aspect affix effects (which attribute an affix modifies)

**Date:** 2026-07-16
**Work item:** casc-fr#45 (LIB-3 — comprehensive-data-exposure program)
**CL:** CL-92 · `AffixDefinition.Effects` / `AffixEffect`

The affix reader (CL-87) resolved an affix's localized `Name`/`Desc` but not
what it *does*. LIB-3 decodes the effect: **which attribute(s) each item/aspect
affix modifies**, mirroring the glyph-affix effect decode (§7.4, CL-84) into
the item-affix domain (group 104).

## The record

Each affix carries its effect in an `arModifiers` `DT_VARIABLEARRAY` at payload
`+0xB0` (descriptor `dataOff@+0xB0` / `byteSize@+0xB4`). The data is an array of
fixed **104-byte modifier records** — `count = byteSize / 104`, verified exact
across every one of the 5,867 authored arrays (sizes 104…2496, i.e. 1–24
modifiers). Within a 104-byte record (26 `int32` slots):

| slot | meaning |
|---|---|
| `idx4` (+16) | **the modified `AttributeId`** |
| `idx7` (+28) | attribute parameter (`ParamPlus12`) — element / skill-tag GBID / `-1` |
| `idx10/14/20/24` | magnitude-formula slots (`~472..640`), family-shared |
| `idx16` | family-shared formula/curve GBID |

## The discipline catch (held off a wrong decode)

The prior session's first read — "`idx10` (the `480`/`472` value) is the
modified stat" — was **disproven and never shipped**: the same `idx10` value
recurs across unrelated stats (`Resource_MaxMana` and `CoreStat_StrengthPercent`
both `480`). Those are *formula slots*, not stat identity.

The real key is **`idx4`**. Proof: every affix whose `Desc` carries a value
placeholder `[Token …]` names the attribute it displays; pairing `idx4` with
that token across **1,220 single-modifier affixes** gives a **1:1 map with zero
conflicts** — `275 → Crit_Percent_Bonus`, `142 → Hitpoints_Max_Percent_Bonus`,
`79 → Resistance_All_Bonus_Percent`, `482 → Armor_Percent`, and clean core-stat
sibling blocks (`4/5/6/7` flat Str/Int/Will/Dex, `13/14/15/16/17` percent).
`idx4` unifies with the runtime `eAttribute` space `GetAttributeName` already
resolves (275 → "Critical Strike Chance", 482 → "Armor", …), and is *finer*
than the coarse power-budget category a node carries (a node lumps Armor +
ArmorPercent + all DamageReduction under one id; the affix distinguishes
`482 = Armor%` from `1125 = Damage Reduction`).

## Two AttributeId namespaces (verified)

The high bit `0x80000000` on `idx4` selects the registry:

- **positive** → engine `eAttribute` (resolve via `GetAttributeName`);
- **negative** → a reference into the data-defined **`DataAttributes`** designer
  table (SNO `1907204`) by *ordinal* `idx4 & 0x7FFFFFFF`. Verified against the
  table itself: ordinal `84 = Barb_Berserking_AttackSpeed`,
  `82 = Barb_Berserking_DamageReduction`, `86 = …MovementSpeed` — exactly the
  `Desc` tokens of the corresponding affixes.

The namespaces are **disjoint** — negative-208 is a different attribute from
positive-208; the id must never be `abs()`-ed. `AffixEffect.IsDataDefinedAttribute`
/ `.DataAttributeOrdinal` expose the split. Sentinels `idx4 ∈ {0 (padding),
-1 (explicit "no attribute" marker)}` are skipped.

> **Cross-link for FR-C27/#2.** The affix `Desc` placeholders are a rich,
> season-robust first-party source for *both* attribute registries: they name
> ~86 positive engine attributes (many the node scan misses — `1125`, `1157`,
> `483/484`, …) **and** they name the `DataAttributes` ordinals directly. This
> is a concrete breach of the "attribute-registry-name wall" — the affix layer
> resolves attribute ids the node layer could not.

## Magnitude & operation — not in the record (verified boundary)

Adversarially checked over the full 6,145-affix corpus: there is **no
`(min,max)` float pair** at any structural position. For the bulk of stat
affixes (CoreStat / Resistance / DamageReduction) the roll magnitude is **not in
the affix at all** — it is item-power-curve driven, keyed by the `idx16` GBID
(the only in-record float is a constant `1.0` flag at the `+0x38` VLA). Explicit
in-record floats occur only as single fixed scalars (set/unique powers) — the
reliable one is the `"Static Value N"` `float32` VLA at fixed struct `+0xC0`.
**Operation** has no structural discriminator either: additive-vs-multiplicative
twins of the same stat (`252`/`253`, `707`/`708`, `736`/`737`) are byte-identical
in all 26 slots *except* `idx4` — the registry assigns a separate AttributeId per
operation variant, so combine semantics are intrinsic to the attribute identity
(`Multiplicative_*` / `_Percent`) plus the `Desc` format, not a modifier field.

Both stay implicit (via `AttributeName` + `Description`) per the durable
boundary (Appendix C). This matches the FR-C29 / glyph-threshold precedent: the
data declares *what* is modified; *how much* is engine/curve-driven.

## Shipped

- `AffixDefinition.Effects` → `IReadOnlyList<AffixEffect>` — one entry per
  modifier (a dual-element resistance has two).
- `AffixEffect(AttributeId, ParamPlus12, AttributeName)` +
  `HasParam` / `IsDataDefinedAttribute` / `DataAttributeOrdinal`.
- Names resolved by `ReadAffix` via the season-robust
  `GetAttributeName(int, uint, locale)` (compound overload → handles
  tag-conditional params); byte-only `Parse` leaves names empty.
- Confidence-gated: empty (never null) unless the `+0xB0` descriptor is
  well-formed and its byte size an exact multiple of 104.

Tests (live `3.1.1.72836`): structure (`ReadAffix_decodes_effect_modifier_list`
— modifier count, params, byte-only/localized split), exact ids/names under
`content-snapshot` (`ReadAffix_resolves_effect_attributes_on_live_build`), and
the pure namespace-helper logic (`AffixEffect_distinguishes_engine_and_data_
defined_namespaces`). Recon: `SnoScan affixcorpus` / `affixattrmap` /
`affixfloatscan` / `affixeffects`.

Independently cross-checked by a five-agent adversarial workflow over the static
corpus (idx4 falsification: HIGH, not falsified; value-encoding archetypes;
operation-field hunt: none). Spec §11.3; Appendix A CL-92.
