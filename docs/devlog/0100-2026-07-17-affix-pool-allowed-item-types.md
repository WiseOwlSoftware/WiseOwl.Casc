# 0100 — affix pool: which item types an affix rolls on (#51)

**Date:** 2026-07-17
**Work item:** casc-fr#51 (affix pool)
**CL:** CL-106 · `AffixDefinition.AllowedItemTypes` + `Diablo4Storage.RollableAffixes`

The proposal + spike were on #51; the Optimizer answered the shape questions
(build the per-affix primitive + an inverted convenience; item-type granularity;
raw ids first, names later). This ships it.

## The primitive

For magic/rare/legendary, an affix rolls from a per-item-type pool. The pool is
**per affix**, not a central table (`AffixFamilyList` is a name registry). Each
gear affix carries a `DT_VARIABLEARRAY[int]` at payload `+0x78` (dataOff@+0x78 /
size@+0x7C) of the `eItemType` ordinals it may roll on. Semantically verified:
`CoreStat_Strength` → armor/jewelry `[16,17,28,30,29,23]`,
`CoreStat_Strength_Weapon` → the 11 weapon types, `Charm_Armor_Percent` → `[71]`,
`CritHitChance` → `[70]`. Shipped as `AffixDefinition.AllowedItemTypes`.

## The inverted convenience

`Diablo4Storage.RollableAffixes(itemTypeId)` — a lazy full pass over group 104
yielding every affix whose `AllowedItemTypes` contains the id ("what rolls on this
type"). Byte-only decoded; `ReadAffix(sno)` for localized text.

## The honest boundary (unchanged from the spike)

The values are engine `eItemType` ordinals, **not** group-98 `ItemType` SNOs. A
correlation attempt against the g98 records failed (the Charm g98 record carries
no `71`), so names need an oracle or the EXE enum. Raw ids ship as the primitive;
the slot rollup the Optimizer wants is deferred until the enum is resolved — one
owner oracle ("which types are 70/71 + a couple weapon values") anchors the rest.
Tempering pools use the same per-affix mechanism (a temper-family subset).
