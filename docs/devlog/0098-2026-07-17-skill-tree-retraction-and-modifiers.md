# 0098 — the skill tree is data-driven (a retraction) + skill modifiers (LIB-5)

**Date:** 2026-07-17
**Work item:** owner-directed (skill-tree deep RE, Rogue + Sorceress oracles)
**CL:** CL-104 · `PowerDefinition.Modifiers` + `PowerModifier`

I told the owner skill trees were "engine-assembled — NO-GO." The owner pushed
back: *"That makes zero sense. Plus there are huge sections of the data still
unevaluated."* Both true. This is the retraction + the real decode.

## The mistake

My scoping spike looked at the `SkillTree` **UI scene** (group 46), saw chrome +
`Testnode` placeholder widgets with the real nodes not hardcoded, and concluded
the graph was runtime-assembled. That's a lazy inference: the UI reading node
data at runtime means the data lives *somewhere*. I'd dismissed g99 from its
*names* without decoding a single record, and I'd modeled ~15–20 of **182** SNO
groups — declaring an engine boundary from that much unevaluated data is exactly
the over-claim the Optimizer keeps catching in me. The owner caught this one.

## The real structure

- **Skills = Powers** (g29): `Rogue_BladeShift` = 399111. Already typed.
- **g99 `Class_<Class>_<Section>_<Cluster>`** are the *passive* clusters, and they
  reference their skills as typed Power refs (`201, 1, 29=group, <PowerSNO>`).
  Barb `…_General_I_Shouts` → War Cry / Challenging Shout / Rallying Cry —
  three real shouts, correctly named. Proof it's data.
- **Skill modifiers** (the enhancement/upgrade nodes — Blade Shift's Grenade
  Shift, Range of Motion, …) aren't separate Powers and aren't referenced from
  the skill record. They're in the skill's **sibling StringList**
  `Power_Rogue_BladeShift` (1120009) as `Mod<N>_Name` / `Mod<N>_Description`
  labels — found via `stlfind "Impossible Escape"` → `[Mod3_Name]`. The §6.7
  sibling convention, extended.

Shipped `PowerDefinition.Modifiers` (`PowerModifier{Index, Name, Description}`),
read from those labels. Validated against the owner's Rogue oracle: Blade Shift's
7 modifiers at sparse indices 0,1,2,3,5,7,9, names + effect text matching
in-game; Puncture / Twisting Blades generalize it.

## What's still open (honestly — may be data or engine)

The owner spelled out the tree's structural rules: one modifier per group
(mutual exclusivity), a modifier needs ≥1 point in its skill, a category needs N
points in the preceding category. The modifier *content* is complete; the
**group / prerequisite / threshold** encoding is located but not decoded. Per the
lesson just learned, I will **not** pre-declare whether it's data or engine — I'll
RE it and report what the bytes actually say.
