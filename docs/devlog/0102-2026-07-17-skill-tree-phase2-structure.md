# 0102 — skill-tree phase-2: the node structure IS data-driven (SkillTreeRewards)

**Date:** 2026-07-17
**Work item:** skill-tree phase-2 (owner deferred RE — modifier groups / prereqs / category thresholds)
**Status:** RE finding (no library API yet). Recon: `SnoScan skilltreedump`.

Phase-1 (CL-104) shipped the modifier *text* (`PowerDefinition.Modifiers` from the
`Power_<snoName>` StringList). Phase-2 asked whether the three structural rules the
owner described are encoded in the data:
1. modifier groups — only one modifier selectable per group,
2. modifier prerequisites — a modifier needs ≥1 point in the skill it modifies,
3. category point-thresholds — a category (Core…) gates on points in the prior one.

They are — rules 1 and 2 explicitly.

## The table: `SkillTreeRewards` (g20 GameBalance, SNO 547685)

A 670 KB GameBalance table of **2,360 fixed 284-byte records** — one per skill-tree
node, across every class. Each record is a **256-byte inline name buffer** + **7
int32 fields**. The name is the node key: `<Class>_<Kind>_<Skill>[_<Modifier>]`,
e.g. `Rogue_Unlock_BladeShift`, `Rogue_Mod_BladeShift_UpgradeA`,
`Rogue_Talent_Cunning_T5_N1`.

Tail-field schema (offsets relative to record start, name buffer = 256):

| field | off | meaning |
|---|---|---|
| F0 | +256 | reserved (`-1`) |
| F1 | +260 | per-kind ordinal (Unlock 0 / Rank −1 / Mod 1 / Talent 2) — role unverified |
| **F2** | +264 | **modified skill Power SNO** — the node's skill (= the modifier prereq) |
| **F3** | +268 | **modifier GBID** (0 for non-modifier nodes) |
| **F4** | +272 | **node type** — 15 Unlock, 2 Rank, 3 Modifier, 1 Talent/passive, 5 Spiritborn-node, 12 default |
| **F5** | +276 | **modifier group id** |
| F6 | +280 | reserved (`-1`) |

Node-type (F4) population: Modifier 1421, Unlock 216, Spiritborn-node 397, Rank 172,
Talent 153, default 1.

## Rule 1 — modifier groups: SOLVED (F5)

Every modifier node carries a **group id (F5)**; modifiers sharing `(F2 skill, F5
group)` are the mutually-exclusive choice set. Blade Shift (`skill=399111`):

```
Rogue_Mod_BladeShift_UpgradeA/B/C   type=3  group=0   (pick one of A/B/C)
Rogue_Mod_BladeShift_Side1/2/3/4    type=3  group=1   (pick one of Side1..4)
```

That's the "one modifier per group" rule as a first-class field — not a naming
convention, not UI-only. Cross-checked on Barbarian WeaponThrow (UpgradeA/B/C grp0,
Upgrade1-4 grp1) and Spiritborn (same schema under node-type 5). It also matches the
Blade Shift power's own formula idiom `Mod.UpgradeA ? 0 : (Mod.UpgradeB ? 0 :
(Mod.UpgradeC ? 0 : 1))` — the exclusivity expressed two independent ways.

## Rule 2 — modifier prerequisite: SOLVED (F2)

A modifier's **F2 = the skill Power SNO it belongs to**. All seven Blade Shift
modifiers carry `skill=399111` (the `Rogue_BladeShift` power). The "needs ≥1 point in
the modified skill" rule is exactly this link — the modifier is bound to its skill.

## Rule 3 — category point-thresholds: PARTIAL (structure yes, numeric threshold not
in this table)

The tree's **categorical/tier structure is present**: active-skill categories surface
as node names + `SkillRankBonus` affix names (`Rogue_Basic_*`, `Rogue_Core_*`,
`Rogue_Special_*`); passive **talent tiers** are explicit in the Talent node names
(`Rogue_Talent_Cunning_T5_N1`, `_Evade_T1_N1` — category = Cunning/Discipline/Evade/
Mechanic, tier = T1..T5). But no numeric **point-threshold** appears as a field in
`SkillTreeRewards` (T1 vs T5 talents differ only by name, not by a decoded value).
The numeric gates are therefore **not located** in this table — remaining candidates:
the `GenericSkillTree` UI layout (g46 2136720, a tier-row scene), a separate
category/cluster table, or an engine rule. **Not** declared an engine boundary — this
is "found the node structure + tier names, numeric thresholds still to be located."

## Net

The skill tree is data-driven (vindicating the owner's correction of the earlier
NO-GO). A future API could expose `SkillTreeNode { Name, Kind, SkillSno, ModifierGbid,
GroupId }` and per-skill modifier groups directly — rules 1 & 2 as typed data. Worth
proposing to the Optimizer to shape before building (customer-proxy).
