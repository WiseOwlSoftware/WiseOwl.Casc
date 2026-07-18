# 0106 — MonsterLevelCurves were there all along (#53, corrects CL-105)

**Date:** 2026-07-18
**Work item:** casc-fr#53 (FR-C36)
**CL:** CL-110 · `MonsterLevelCurvesTable` + `Diablo4Storage.ReadMonsterLevelCurves`

CL-105 recorded `MonsterLevelCurves` (1610053) as "an empty name-fragment registry,
no per-tier curve — an evidence-backed 'not in the data'." Wrong. The owner pointed
me back at #53 right after the skill-tree episode (where I'd called a fully
data-driven system "engine-side" three times). Same reflex, same error — the fourth
"not in the data" miss of the session.

## What the first read missed

The table is a VLA @ `+0x50` → **6 × 320-byte tier records** (`Raid_Tier_0..5`).
The first read saw each record's inline name + a couple of placeholder `1.0` floats,
saw they looked "identical across Tier_0 and Tier_5," and stopped. But per the "all
buffer is used" principle, 320 bytes/record isn't a name + two floats — and **each
record carries a `DT_VARIABLEARRAY` descriptor at record offset `+312`** pointing to
its curve rows in the record tail (`+2040`…). Following that descriptor:

| tier | rows | span (level → scaled) |
|---|---|---|
| Raid_Tier_0 | 11 | 55 → 100 |
| Raid_Tier_1 | 9 | 65 → 100 |
| Raid_Tier_2 | 7 | 75 → 100 |
| Raid_Tier_3 | 4 | 85 → 100 |
| Raid_Tier_4 | 3 | 95 → 100 |
| Raid_Tier_5 | 2 | 105 → 100 |

Each row is 12 bytes: two `int32` (equal in the live data — the level) + one
`float32` scaled value. Higher tiers start at a higher base level and have fewer
rows — the raid difficulty re-leveling.

## Shipped

`MonsterLevelCurvesTable` (`Tiers` → `MonsterLevelCurve` → `MonsterLevelCurvePoint`)
+ `ReadMonsterLevelCurves()`. The exact remap semantic (effective level vs.
multiplier) is a structural inference — named descriptively, raw values exposed
(comprehensive-data-exposure + calibrate-claims).

## The pattern

Four times this session I declared D4 gameplay data "not in the data / engine-side"
and was wrong every time: skill-tree NO-GO, monster base-HP, skill-tree gates,
now these curves. The fix each time was the same — follow the descriptor / follow
the size, don't stop at the first boring bytes. The rule is recorded
([[feedback_never-declare-engine-driven]]); this devlog is the fourth data point.
