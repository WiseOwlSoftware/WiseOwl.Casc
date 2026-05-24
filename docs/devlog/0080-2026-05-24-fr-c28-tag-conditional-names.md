# 0080 — FR-C28: tag-conditional `(AttributeId, ParamPlus12)` attribute-name resolution

2026-05-24 · CL-85 · branch `fr-c28-tag-conditional-names`

## Trigger

`casc-fr#40` filed from CL-78's honesty note: today CASC surfaces
`AttributeId 259` (`DamageBonusTag`) as `null` and the consumer falls
through to `"Attribute 259"`. The anchor is `Warlock_Rare_006`
(sno `2451111`), which has attr 259 with `ParamPlus12 = 0x32ABA6FB`
and should resolve to "Demonology Damage".

The FR was waiting on CL-84 to pin the `(AttributeId, ParamPlus12)`
shape on the affix; that landed, so the parallel labels map now has a
clear home.

## What I went looking for

A `Dictionary<(int AttributeId, uint ParamPlus12), string>` resolving
every observed compound key on the catalog. Two data sources:

- **Group 112 `ParagonGlyphAffix`** — every Op-1/2/4 affix's first
  `ptAttributes` entry pairs the AttributeId with a ParamPlus12 that
  carries the tag GBID / status enum / element enum / SNO ref. CL-84's
  `glyphaffixscan` enumerated all 314 records.
- **Group 106 `ParagonNode`** — every node's `ptAttributes` array. New
  `build/SnoScan nodetuplescan` enumerates the 163 entries where
  `ParamPlus12 ≠ -1`.

## What I found

**100+ distinct `(attr, param)` tuples** spanning 17 AttributeIds. The
per-id base templates (from `AttributeDescriptions` sno 4080):

| Attr | Base template label key                         | Per-key meaning |
|---|---|---|
| 161  | `Resource_Max_Bonus`                             | Resource enum (1=Fury, 5=Spirit, 6=Essence, 9=Faith, 10=Wrath, 11=Dominance) |
| 223  | `Attack_Speed_Percent_Bonus_Per_Skill_Tag`       | Skill-tag GBID |
| 238  | `Skill_Tag_Cooldown_Reduction_Percent`           | Skill-tag GBID |
| 254  | `Damage_Type_Percent_Bonus`                      | Element enum (0=Physical, 1=Fire, 2=Lightning, 3=Cold, 4=Poison, 5=Shadow, 6=Holy) |
| 258  | `Damage_Percent_Bonus_To_Targets_Affected_By_Skill_Tag` | Skill-tag GBID |
| 259  | `Damage_Percent_Bonus_Per_Skill_Tag`             | Skill-tag GBID (largest cluster — 39 distinct values) |
| 263  | `Damage_Percent_Bonus_Per_Weapon_Requirement`    | Weapon SNO ref |
| 290  | `Crit_Damage_Percent_Per_Skill_Tag`              | Skill-tag GBID |
| 390  | (resource-on-kill family)                        | Resource enum |
| 588  | (pet attack-speed family)                        | Pet enum |
| 708  | `DOT_DPS_Bonus_Percent_Per_Damage_Type`          | DoT type enum |
| 954  | `Damage_Percent_Bonus_Vs_CC_Target`              | CC type enum |
| 959  | `Damage_Percent_Bonus_Against_Dot_Type`          | DoT type enum |
| 962  | `Damage_Percent_Bonus_Per_Shapeshift_Form`       | Shapeshift form enum |
| 965  | `Damage_Percent_Bonus_While_Affected_By_Power`   | Power SNO ref |
| 981  | `Pet_Damage_Bonus_Percent_Per_Pet_Type`          | Power SNO ref (the linked-pet's power) |
| 991  | (power duration bonus)                           | Power SNO ref |
| 994  | (power potency bonus)                            | Power SNO ref |
| 1037 | (DustDevil size bonus)                           | Power SNO ref |

The map bakes the substituted enUS display string directly (e.g.
`(259, 0x32ABA6FB) → "Demonology Damage"`) — the template's
`{VALUE1}` placeholder gets substituted with the per-key resolved tag
name.

## Hash crack — the `Skill_<TagName>` pattern

Per `[[feedback_cumulative-hash-decode]]`, I brute-forced the
ParamPlus12 GBIDs against candidate tag names. Pattern hit was
**`Skill_<TagName>`** (lowercased DJB2, seed 0). 19 GBIDs cracked in
one in-process pass; appended to `docs/d4-hash-dictionary.md` "Gbid
hashes" section.

| GBID | Cracked label | Class context |
|---|---|---|
| `0x32ABA6FB` | `Skill_Demonology` | Warlock (the FR-C28 anchor) |
| `0x6A1F0A80` | `Skill_Abyss` | Warlock |
| `0x6D657409` | `Skill_Hellfire` | Warlock |
| `0xCEAEA388` | `Skill_Occult` | Warlock |
| `0xE43A2895` | `Skill_Trap` | Rogue |
| `0x12674CDC` | `Skill_Cutthroat` | Rogue |
| `0xE4B9B478` | `Skill_Marksman` | Rogue |
| `0xF4EE66C7` | `Skill_Mobility` | Rogue |
| `0x6A3673AE` | `Skill_Blood` | Necromancer |
| `0xE4303EA2` | `Skill_Bone` | Necromancer |
| `0xE43BC256` | `Skill_Wolf` | Druid |
| `0x0F479E53` | `Skill_Incarnate` | Druid |
| `0x6625AC6B` | `Skill_Shapeshifting` | Druid |
| `0x6B67A5C3` | `Skill_Shade` | (general) |
| `0x5FCEB9D4` | `Skill_Grenade` | Demon Hunter |
| `0xE87A54CD` | `Skill_Zealot` | Paladin |
| `0x8C8DF55A` | `Skill_Juggernaut` | Paladin |
| `0xFFFA158B` | `Skill_Disciple` | (general) |
| `0xD5D1FA40` | `Skill_Recast` | (general) |

**~30 additional GBIDs remain uncracked** — Archfiend (`0x945652E5`),
Conjuration (`0x730FE54D`), Companion (`0xCCA1AF65`), Corpse
(`0xACF2CA8D`), Earthquake (`0x8ED92461`), DesecratedGround, DustDevil,
IceSpike, Earth, Storm, Nature, etc. They do NOT match the
`Skill_<Name>` pattern — engine-internal key is something else. The
curated map carries the empirical names anyway (every affix's
sno-name + every `Generic_Magic_Damage<Tag>` node confirms the same
name); future hash-decode passes can migrate those rows in.

## What ships

```csharp
// AttributeNames.cs:
public static IReadOnlyDictionary<(int AttributeId, uint ParamPlus12), string>
    LabelByCompoundKey { get; }    // 100+ curated entries

// Diablo4Storage.cs:
public string? GetAttributeName(int attributeId, uint paramPlus12,
    string locale = DefaultLocale);

// ParagonNodeInfoBuilder.ResolveStatName: now threads ParamPlus12
// through the cascade — every multi-attribute node picks up the
// tag-conditional name automatically.
```

Cascade: compound-key map → single-id `LabelByAttributeId` →
hardcoded basic-four → node-name token → honest `"Attribute <id>"`.

## Tests

The live `Acceptance_matrix_against_live_install` adds:

- **FR-C28 anchor**: `GetAttributeName(259, 0x32ABA6FB) == "Demonology Damage"`.
- **Warlock_Rare_006**: the node's first stat row now resolves
  `StatName == "Demonology Damage"` (was `"Attribute 259"` pre-CL-85);
  the 17.5% magnitude assertion is unchanged.
- **Single-id fast path**: `GetAttributeName(9, NoParam) == "Strength"`.
- **Compound miss fallback**: `GetAttributeName(9, 0xDEADBEEF) == "Strength"`
  (real ParamPlus12 but not in the compound map → fall through to the
  single-id map, not null).
- **Element enum** (attr 254): Physical / Fire / Lightning / Cold /
  Poison / Shadow / Holy.
- **Resource enum** (attr 161): Fury / Spirit / Essence.

127/127 tests green on `3.0.2.71886`.

## What stays open

- **~30 uncracked GBIDs.** The empirical names ship today; the
  engine-internal hash keys (whatever the engine uses internally —
  not `Skill_<Name>` for the second cluster) is the next opportunistic
  hash-decode target. When cracked, no consumer-visible change — only
  the dictionary citation moves from "empirical sno-name" to "cracked
  hash".
- **Locale.** The compound map is enUS-only today. The single-id
  `LabelByAttributeId` already routes through `AttributeDescriptions`
  per locale; the compound case will follow the same pattern when
  needed.

## Recon tool committed

`build/SnoScan nodetuplescan` — enumerates every group-106 ParagonNode
record's `(AttributeId, ParamPlus12)` tuple where `ParamPlus12 ≠ -1`.
Same family as `glyphaffixscan` (CL-84) / `attrmap` / `nodeinfo`.
