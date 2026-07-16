# 0086 — LIB-1: item base-type taxonomy (gear/item API)

**Date:** 2026-07-16
**Work item:** casc-fr#43 (LIB-1 — first proactive comprehensive-data-exposure item)
**CL:** CL-90 · spec §13 · `ItemType` / `ItemClass`

First proactive CASC-initiated deliverable under the owner's directive to
*expose all dataset data via typed APIs, gated on verified RE*. Owner example:
enumerate all weapons / armor / jewelry / charms.

## RE

`g98` (`GearItem`) is the item-type dictionary — the engine's `eItemType` enum,
~153 entries: gear (`Sword`, `Amulet`, `Helm`, `Charm`, …) plus non-gear
(`Gold`, `HealthPotion`, `DungeonKey`, `Quest`, `Gem`, …). An item (`g73`) names
its base type at payload **`+0x0C`** (`Chest_Normal → ChestArmor`,
`1HSword_Legendary → Sword` — confirmed).

Classification is fully structural (no name parsing — `feedback_no-atlas-name-jumps`),
from five fields I pinned by dumping representatives across categories and
diffing:

| Field | Meaning |
|---|---|
| `+0x08` kind | 32/48 ⇒ equippable gear; <32/72/104 ⇒ non-gear |
| `+0x0C` sub-kind | 5 ⇒ Charm (4 ⇒ dungeon key) |
| `+0x30` weapon-family | ≥0 ⇒ weapon-slot (Axe/Sword/Mace share 1; Bow 6; Wand 8; Shield 14…); -1 ⇒ not a weapon |
| `+0x3C` armor-scalar | 0.0 ⇒ jewelry (no armor value); >0 ⇒ body armor |
| `+0x44` slot | >0 for armor/jewelry (excludes Essence = 0) |

`ItemClass` = Charm (`+0x0C==5`) · Weapon (`+0x30≥0`) · Armor (`+0x30==-1,
slot>0, scalar>0`) · Jewelry (same, scalar==0) · Other. Verified across the
full set: **Weapon 28** (weapons + off-hands shield/focus/totem), **Armor 5**
(chest/helm/gloves/legs/boots), **Jewelry 2** (amulet/ring), **Charm 1**,
**Other 117**. The jewelry/armor split at `+0x3C` was the subtle one — 0.0 for
Amulet/Ring, 0.1–0.35 for the armor pieces.

## Shipped

- `ItemType` (`SnoId`, `Name`, `Class`, `IsEquippable`, `WeaponFamily`) + `ItemClass`.
- `Diablo4Storage.ReadItemType(int)`, `EnumerateItemTypes()`,
  `EnumerateItems(ItemClass)` (every weapon/charm/… in the game).
- `ItemDefinition.ItemTypeSnoId` (the item→type link).
- Catalog: `AssetKind.ItemType` + a **decoded** `category` facet (a structural
  improvement over the existing name-convention `type` facet).
- Tests: `LIB1_item_type_classification_is_structural` (invariants + the link)
  + `LIB1_item_type_category_counts_pinned_to_build_3_1_1` (content-snapshot).
  Recon: `SnoScan itemtypes`. 139/139 green on live 3.1.1.72836.

## Boundary / next

Identity + type + enumeration. Item **stat effects / affix rolls** (the larger
frontier, g104) are the next comprehensive-data-exposure slice. Off-hands
currently fold into `Weapon` (weapon-slot); a `Weapon` vs `Offhand` split is a
possible refinement if a consumer needs it.
