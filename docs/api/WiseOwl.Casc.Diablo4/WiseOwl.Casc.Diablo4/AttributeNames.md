# AttributeNames class

FR-C25 — resolve a Diablo IV `AttributeId` (the raw `eAttribute` int on a [`NodeAttribute`](./NodeAttribute.md) / [`ParagonGlyphAffixDefinition`](./ParagonGlyphAffixDefinition.md)) to its in-game localized name via the engine's `AttributeDescriptions` StringList (sno `4080`) — the same source the tooltip renderer uses.

```csharp
public static class AttributeNames
```

## Public Members

| name | description |
| --- | --- |
| static [CompoundBaseLabelById](AttributeNames/CompoundBaseLabelById.md) { get; } | FR-C27 (CL-88) — the base `AttributeDescriptions` label for each compound (tag/element/resource-conditional) base `AttributeId`. These ids sit in the engine's stable low range (all &lt; 481; unmoved through Season 14, unlike the shifting single-id tail), so the compound resolver anchors them by id: it resolves the incoming id to its base label here, then keys [`NameByCompoundLabelKey`](./AttributeNames/NameByCompoundLabelKey.md) on `(label, ParamPlus12)` — the label + the stable tag/element GBID are both season-durable, retiring the id from the compound key. |
| static [LabelByAttributeId](AttributeNames/LabelByAttributeId.md) { get; } | The shipped clean-room curated mapping from `AttributeId` to its primary `AttributeDescriptions` label. See [`AttributeNames`](./AttributeNames.md) for the source of the curation + the ambiguity note. |
| static [LabelByCompoundKey](AttributeNames/LabelByCompoundKey.md) { get; } | FR-C28 (CL-85) — compound-key map resolving the tag-conditional attribute names where the same [`LabelByAttributeId`](./AttributeNames/LabelByAttributeId.md) entry can't disambiguate (e.g. `AttributeId 259` = `DamageBonusTag` — the same id covers Abyss / Demonology / Conjuration / Hellfire / ... damage, the per-tag identity carried in `ParamPlus12`). Keyed on the raw `(int AttributeId, uint ParamPlus12)` tuple; the value is the resolved display name as the engine renders it in the in-game tooltip (e.g. `(259, 0x32ABA6FB) → "Demonology Damage"`). Surfaced to the consumer via [`GetAttributeName`](./Diablo4Storage/GetAttributeName.md); ParagonNodeInfoBuilder consults it on every node stat row. |
| static [LabelByToken](AttributeNames/LabelByToken.md) { get; } | FR-C27 (CL-88) — the season-stable mapping from a `ParagonNode` name token (the `Generic_<Rarity>_<Token>` suffix) to its `AttributeDescriptions` label. This is the durable half of the resolver: the raw `AttributeId` is a registry ordinal that the engine renumbers whenever it inserts attributes (Season 14 moved `Armor` 481→482, `Damage_Bonus_At_High_Health` 1120→1123, `Barrier` 1124→1127, …), so the id is worthless as a durable key — but the node-name token never changes. [`Diablo4Storage`](./Diablo4Storage.md) scans the live `Generic_` nodes at runtime to learn the `id → token` map for the current build, then this table turns the token into a label the existing `AttributeDescriptions` (sno 4080) pipeline localizes. The result auto-tracks every season's id shifts with no code change. [`LabelByAttributeId`](./AttributeNames/LabelByAttributeId.md) is retained only as a defensive fallback for ids whose token isn't scannable. |
| static [NameByCompoundLabelKey](AttributeNames/NameByCompoundLabelKey.md) { get; } | FR-C27 (CL-88) — the season-robust re-key of [`LabelByCompoundKey`](./AttributeNames/LabelByCompoundKey.md) onto `(baseLabel, ParamPlus12)`. Derived at load from [`LabelByCompoundKey`](./AttributeNames/LabelByCompoundKey.md) (the source of the enUS strings) + [`CompoundBaseLabelById`](./AttributeNames/CompoundBaseLabelById.md) (the base id → label anchor): every `(id, param) → name` entry whose base id has a known label becomes `(label, param) → name`. The compound resolver keys on this so a base-id renumber (a future season shifting e.g. 259) doesn't strand the tag-conditional names — the label + the tag/element GBID are both durable. |
| static [StripTemplate](AttributeNames/StripTemplate.md)(…) | Strip an `AttributeDescriptions` template down to its bare display name — remove the `[{VALUE…|…|}]` placeholders, the standalone `{VALUE…}` variant tokens, the engine's color tags (`{c_label}`/`{/c}` etc.), the leading sign/bracket markup (`+`/`[`/`(`), the trailing colon, and normalise whitespace. The returned name is what the engine would render with all numeric / variant placeholders removed — e.g. `"+[{VALUE}*100|1%|] Damage to Elites" → "Damage to Elites"`. |

## Remarks

Pipeline. AttributeId → label key (clean-room curated map, see [`LabelByAttributeId`](./AttributeNames/LabelByAttributeId.md)) → templated body in sno `4080` via the existing per-locale StringList machinery → stripped name (templates removed, color tags removed, whitespace normalised; [`StripTemplate`](./AttributeNames/StripTemplate.md)). Examples (build `3.0.2.71886`):

**AttributeId**

**Label `→` Template `→` Stripped name**

**9**

`Strength → "[{VALUE}|~|] Strength" → "Strength"`

**133**

`Hitpoints_Max_Bonus → "[{VALUE}|~|] Maximum Life" → "Maximum Life"`

**481**

`Armor_Bonus → "+[{VALUE}] Armor" → "Armor"`

**950**

`Damage_Percent_Bonus_Vs_Elites → "+[{VALUE}*100|1%|] Damage to Elites" → "Damage to Elites"`

Coverage.[`LabelByAttributeId`](./AttributeNames/LabelByAttributeId.md) covers every `AttributeId` the Optimizer surfaced in their FR-C21 / FR-C25 probes plus the long-tail set observed via the `Generic_<Rarity>_<Token>` node-name convention (`SnoScan attrmap`; the empirical first-party observation). AttributeIds not in the map return `null` from [`GetAttributeName`](./Diablo4Storage/GetAttributeName.md) — honest sentinel (consumer falls back to `"Attribute <id>"`); future builds adding new attributes can extend the map without API changes.

Ambiguity. Some `AttributeId` values are power-budget categories shared by multiple distinct stats (e.g. `481` covers Armor / ArmorPercent / DamageReduction* — the CL-66 finding). The map returns the primary name ("Armor"); the per-node disambiguation lives on [`StatName`](./ParagonNodeStat/StatName.md) via the ParagonNodeInfoBuilder token fallback (the budget- category sub-stat is in the node name, not in the AttributeId).

## See Also

* namespace [WiseOwl.Casc.Diablo4](../WiseOwl.Casc.Diablo4.md)
* [AttributeNames.cs](https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main/AttributeNames.cs)

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.Diablo4.dll -->
