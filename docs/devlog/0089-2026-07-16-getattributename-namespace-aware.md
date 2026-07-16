# 0089 — namespace-aware GetAttributeName: DataAttributes + no stale wrong-names

**Date:** 2026-07-16
**Work items:** casc-fr#47 (FR-C32), #46 (FR-C31), #48 (LIB-4)
**CL:** CL-93 · `Diablo4Storage.TryGetDataAttributeName` + `GetAttributeName` fixes

A tightly-coupled cluster the Optimizer queued while LIB-3 (CL-92) was in
flight. All three are on the attribute-name surface, and the LIB-3 bit-31
finding resolved the hardest one directly.

## FR-C32 (#47) — bit 31 is a namespace flag, not a sign

Live paragon/glyph data carries `AttributeId`s with the high bit set
(`0x800000xx`); `GetAttributeName` returned `null` for all of them and the
Optimizer's masking-into-`GetAttributeName` gave misleading answers (masked
`252`/`254` → "Damage"). CL-92 had already decoded this in the **item-affix**
data; verifying the Optimizer's **node/glyph** ids against the same table
closed it:

| flagged id (`& 0x7FFFFFFF`) | `DataAttributes[ordinal]` |
|---|---|
| 193 | `Paladin_Arbiter_WingStrike_Damage` |
| 249 | `Warlock_Shadowform_Damage_Bonus` |
| 251 | `Warlock_Demonform_Damage_Bonus` |
| 252 | `Multiplicative_Warlock_Demonform_Damage_Bonus` |
| 253 | `Damage_Percent_Bonus_While_Volatile` |
| 254 | `Multiplicative_…While_Volatile` |

**Bit 31 selects the `DataAttributes` designer table** (SNO 1907204), indexed
by ordinal `id & 0x7FFFFFFF` — a *disjoint* namespace from the engine
`eAttribute` registry (engine-254 = "Damage"; DataAttributes-254 =
Volatile-mult). The additive/multiplicative pairing the Optimizer spotted is
in the table (a `Multiplicative_` prefix). New
`Diablo4Storage.TryGetDataAttributeName(int, out string)` reads the szName by
ordinal; `GetAttributeName` returns `null` for flagged ids (documented: use
the new method, never `abs()`). `AttributeId == -1` confirmed as the "no
attribute" sentinel.

## FR-C31 (#46) — stop the stale by-id fallback returning wrong names

`GetAttributeName(1124)` returned `"Barrier Generation"` for a glyph affix
whose own text is *damage while Healthy*. Root cause: the curated
`LabelByAttributeId` fallback keys on the drift-prone id, and `1124` is the
**pre-Season-14** Barrier id — live glyph-affix data still references it while
nodes moved to `1127`. A stale by-id entry answers with the old meaning.

Fix: the by-id fallback is now **restricted to the season-stable low range**
(`< 481` — `AttributeNames.StableAttributeIdRangeExclusiveMax`, CL-88's own
"ids < 481 unmoved" invariant). The drift-prone tail (Armor 481→482, Elites
950→953, Barrier 1124→1127, …) is dropped from the by-id map; those attributes
resolve season-robustly through the runtime `id→token` node scan when current,
and return an **honest null** when the id is stale — never a wrong name. Every
one of the ≥481 entries was verified to be a stale predecessor (the current
successors resolve via the token scan), so no current coverage is lost.

## LIB-4 (#48) — doc regen

`GetAttributeName.md` / `AttributeNames.md` predated CL-88 (described the
curated map as primary; examples pinned to stale ids 481/950). Refreshed the
XML comments (token-scan pipeline primary; current examples 482/953) and
regenerated `docs/api`.

## Boundary honesty

For a stale glyph-affix id like `1124` the fix returns `null` rather than
resolving it to the correct name — that would need a glyph-affix-namespace
scan (a separate source; item affixes don't reference these ids). `null` over
a wrong name is exactly what #46 asked for; the fuller affix-namespace
resolution is noted as a follow-up. Tests (live `3.1.1.72836`): the shifted-id
null guard + the flagged-ref resolution, both `content-snapshot`. Spec §11.3;
Appendix A CL-93.
