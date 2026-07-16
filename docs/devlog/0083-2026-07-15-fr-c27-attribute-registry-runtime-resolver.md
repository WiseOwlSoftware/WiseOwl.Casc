# 0083 — FR-C27: season-robust AttributeId → name (runtime token resolver)

2026-07-15 · CL-88 · branch `fr-c27-dataattributes-registry`

## Trigger

`casc-fr#39` (FR-C27) asked to decode `DataAttributes` (sno `1907204`) as
"the engine's full AttributeId registry" and retire the CL-78 curated
`AttributeNames.LabelByAttributeId` map. The Season-14 re-baseline
(devlog 0082 / CL-87 aftermath) had just shown *why* it matters: a glyph
affix's `AffectedAttributes[0]` moved `1120 → 1123` across the patch, i.e.
`AttributeId` is not a stable key.

## The premise was wrong

Decoded `DataAttributes` cleanly (header VLA at payload+80/84, 281 entries
× 360 stride). But it is **not** the registry the FR assumed:

```
[  0] Flurry_Consume_2      [  3] BSK_Bonus_Int     [ 26] Socketable_VilePhylactery
[253] Damage_..._While_Volatile   [279] S14_Mythic_CooldownReductionCDR
[280] S14_Mythic_UniquePotency        (all gbid = 0xFFFFFFFF)
```

It's the **designer/season-extensible** attribute table — skill consumes,
socketables, class-form bonuses, seasonal mechanics appended at the tail
(the `S14_Mythic_*` pair *is* the 278→281 growth). The core attributes the
name map covers — `Strength`, `Armor`, the `Damage_Bonus_To_*` family —
are absent. Decoding its offset would name the designer subset, not retire
the curated map.

## The real finding: AttributeId is a registry ordinal

Scanning the live `Generic_<Rarity>_<Token>` nodes + glyph affixes
(`SnoScan attrcover`) against the curated map exposed the full shift — the
engine renumbers `eAttribute` whenever it inserts entries:

| attribute | curated id | live S14 id |
|---|---|---|
| Armor | 481 | **482** |
| DamageToVulnerable | 735 | **736** |
| DamageToElite | 950 | **953** |
| DamageToNear | 1102 | **1105** |
| DamageToFar | 1104 | **1107** |
| DamageWhileHealthy | 1120 | **1123** |
| Barrier | 1124 | **1127** |

Even *Armor* moved. A hardcoded `id → name` map is unsalvageable. But the
node-name **token** (`Armor`, `DamageWhileHealthy`) never changes — and the
node carries whatever id the engine assigned this build.

## Fix: move the curation off the volatile id onto the stable token

`Diablo4Storage.GetAttributeName(id, locale)` now:

1. **runtime `id → token`** — scan the live `Generic_` nodes once (cached),
   recording each node's attributes under its name token. Season-robust:
   the tokens ride along with the renumbering.
2. **`token → label`** — `AttributeNames.LabelByToken` (season-stable, the
   durable half of the curation).
3. **`label → localized`** — the existing `AttributeDescriptions` (sno
   4080) template + `StripTemplate` pipeline (preserves per-locale names).

`LabelByAttributeId` is kept only as a defensive fallback. The FR-C28
compound (tag-conditional) map is re-keyed the same way: resolve the base
id → label, then key `NameByCompoundLabelKey` on `(baseLabel,
ParamPlus12)` (both durable) — derived programmatically from the existing
`(id, param)` map + `CompoundBaseLabelById`.

## Result (live `3.1.1.72836`)

```
482  -> Armor                              1123 -> Damage while Healthy
1105 -> Damage to Close Enemies            1107 -> Damage to Distant Enemies
953  -> Damage to Elites                   259+Demonology -> Demonology Damage
```

Every shifted attribute resolves at its *current* id — the legacy id-map
returns null for all of them. A new season-robust coverage test
(`GetAttributeName_resolves_every_live_generic_node_attribute`, no
hardcoded ids) walks the live nodes and asserts each curated-token
attribute resolves; it surfaced a latent curation bug — `BlockChance`'s
label is `Block_Chance`, not the absent `Block_Chance_Bonus` — now fixed.
The exact current-id → name anchors live in a `content-snapshot`-tagged
theory. 135/135 green.

## Boundary

Attributes not on a scannable `Generic_` node (none observed in the
curated set on this build) fall to the legacy id-map, which is stale for
shifted ids — acceptable, since the live data only ever references the
current ids. `DataAttributes` (the designer subset) remains undecoded for
naming; it isn't the core registry.
