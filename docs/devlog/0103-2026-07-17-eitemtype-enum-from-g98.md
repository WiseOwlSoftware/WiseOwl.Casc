# 0103 — the eItemType enum was in g98 all along (#51)

**Date:** 2026-07-17
**Work item:** casc-fr#51 (affix pool — item-type names + slot rollup)
**CL:** CL-108 · `ItemType.EItemType` + `Diablo4Storage.ReadItemTypeNames` /
`GetItemTypeName` (corrects CL-106)

The owner declined the in-game item-type oracle and (via the Optimizer) asked me
to pursue the **EXE `eItemType` enum** for the pool names + slot rollup. The EXE
route was a dead end via grep (a stripped 57 MB binary; the one `eItemType:` string
is in store UI). So I went back to g98 — which CL-106 had written off ("not in g98;
a correlation attempt failed") — and applied the discipline: don't give up from a
shallow look.

## The find

CL-106's correlation scanned the g98 **header** fields and found no `71` in the
Charm record. But the ordinal isn't in the header — it's the **first int32 of the
`DT_VARIABLEARRAY` at payload `+0x28`**. Reading that:

```
Charm      +0x28 VLA[0] = 71   ✓ (Charm_Armor_Percent → [71])
Helm       = 16   ChestArmor = 17   Gloves = 28   Boots = 29   Legs = 30
Amulet     = 26   Ring = 19   Axe = 1   Bow = 10   ...
```

Extracted across the whole g98 set (`SnoScan itemtypeenum`): **61 distinct
ordinals**, validated against the real pools — `CoreStat_Strength` `[16,17,28,30,
29,23]` and `CoreStat_Strength_Weapon` `[1,10,11,6,3,7,46,13,2,12,9]` line up.
Shipped as `ItemType.EItemType` + `Diablo4Storage.ReadItemTypeNames()` /
`GetItemTypeName(int)`.

1H/2H and class variants **share one ordinal** (`Axe`/`Axe2H` = `1`,
`Staff`/`StaffDruid`/`StaffSorcerer` = `13`), so the map is
one-ordinal-to-many-names; the resolver returns the shortest equippable base name.

## The gap — and its refinement (2026-07-18)

Two pool ordinals — `9`, `23` — aren't *leaf* item-types (no record carries them
as its `+0x28` `eItemType`), so `GetItemTypeName` returns `null`. Initially
recorded as "no g98 record / engine-aggregate." A later idle re-check (applying the
session's discipline lesson to my own gap) refined it: they're **aggregate category
ordinals**, present as *secondary* markers on the relevant records. `23` appears in
exactly the seven off-hand/shield records (`Shield`, `Amazon_Shield`, `ShieldHTH`,
`Focus`, `FocusBookOffHand`, `OffHandTotem`, `HoradricSeal`) → the off-hand/shield
category; `9` is a weapon-family marker on the weapon records. So the leaf map is
complete; the two aggregates are category-level, not voids. Recon: `SnoScan
itemtypeenum <target>`; shared on casc-fr#51.

## Discipline note

Third "don't declare from a shallow look" win of the session. CL-106 concluded the
data lacked the mapping after checking one field of one record; the mapping was one
VLA over. The owner asked for the EXE; the answer was in the SNO data. Delivered on
`casc-fr#51` unreleased (owner is batching).
