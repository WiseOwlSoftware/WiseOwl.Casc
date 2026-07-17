# 0097 — a unique item's power is a same-name affix (LIB-4)

**Date:** 2026-07-17
**Work item:** proactive LIB-4 (gear-depth arc; scoping spike → build)
**CL:** CL-103 · `Diablo4Storage.TryReadUniqueAffix`

Coming out of the gear-depth scoping spike: the highest-value, lowest-effort win
was wiring a unique item to its power. Both halves were already decoded
(`ReadItem`, `ReadAffix`) — the question was how they link.

## The link is by name, not by reference

Dumping `1HAxe_Unique_Druid_100` (item, group 73, SNO 1306219), its header
references resolve to:
- `+8` → `axe_uniq06` (group 1) — the **model actor**
- `+80` → `1HAxe_Legendary_Generic_001` (group 73) — the **base-item template**

Neither is the affix. The item's *power* lives in an `AffixDefinition` (group
104) that shares the item's SNO name **verbatim** — `1HAxe_Unique_Druid_100`
exists in both g73 (the item) and g104 (SNO 578782, the affix). That's the same
§6.7 sibling convention the localized StringList tables use (CL-20). Verified
5/5 across the roster (Druid/Rogue/Barb/Paladin/Generic uniques all name-match).

So the wiring is: `CoreToc.TryGetName(Item, id)` → `CoreToc.TryGetId(Affix, name)`
→ `ReadAffix`. `Diablo4Storage.TryReadUniqueAffix(itemSnoId)` returns the item's
`Effects` / `InlineFormula` (its rolled values, §8.1/§11.3) + localized `Name`;
`false` for a non-unique item or a seasonal `S<NN>_`-prefixed variant whose affix
name differs (verified `1HAxe_Legendary_Generic_001` → no g104 twin → false).

## Scope + discipline

Wiring only — no new byte layout, no speculative surface: one method joining two
shipped readers, the obvious canonical relationship. Per
[[feedback_optimizer-as-customer-proxy]] this ships as a minimal proactive
`LIB-N` and solicits the Optimizer's shape feedback rather than pre-building a
richer gear API it might want different. The two heavier gear-depth targets from
the spike — tempering recipes (`TemperRecipeFamily`, structural) and item-type
base stats (g98 float scalars) — are left for the Optimizer to prioritise.
