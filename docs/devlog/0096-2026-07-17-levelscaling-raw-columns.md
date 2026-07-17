# 0096 — LevelScaling's other columns: exposed raw, honestly unnamed (CL-102)

**Date:** 2026-07-17
**Work item:** casc-fr#50 companion (the Optimizer's "type all six LevelScaling columns" re-raise)
**CL:** CL-102 · `LevelScalingRow` + `.Columns` + `LevelScalingTable.Row`/`.Rows`

The #50 delivery promised a fast-follow: type `LevelScaling`'s remaining columns
(`monsterDr` / `powerBase` / `powerDelta` / `powerItem` / `xpScalar`) — the
Optimizer wanted `powerItem` as a possible `IPower()` source for the §8.1 affix
evaluator. I did the RE. The honest outcome is a boundary, not five names.

## Why I can't name them

Dumping the 212-byte row across L1/L40/L70/L200 (col at each level):

| col | L1 | L70 | L200 | behavior |
|---|---|---|---|---|
| `+4` | 1.0 | 30.53 | 1234.9 | **hpScalar** (verified — drives base Life) |
| `+8` | 0.85 | 7.84 | 10.17 | grows → plateaus |
| `+32` | 1.2 | 0.147 | 0.5 | decreases (inverse) |
| `+36` | 1.0 | 0.033 | 0.5 | decreases |
| `+20/+24/+28` | — | — | — | constants (0.002 / 0.015 / 0.5) |

`hpScalar` is anchored (base Life 50/860/1526, §8.2). The others are **not**:
unlike `DifficultyTiers`, whose XP column reproduces a known in-game curve and so
*locks* every column's identity (§8.3), `LevelScaling` has no anchor here and no
in-game readout to oracle against. Mapping the Maxroll names to `+8`/`+32`/`+36`/…
would be a pure guess — the exact FR-C31 wrong-name defect the Optimizer told me
to avoid ("on your terms, not Maxroll's"). `powerItem` in particular: item power
at L70 is ~800-ish, and no column reads anything near that, so it isn't obviously
even present in this table.

## What shipped

Not a guess — the **raw exposure**: `LevelScalingRow.Columns` surfaces all 53
columns (comprehensive-data-exposure), `hpScalar` labeled, the rest raw with
their per-level behavior characterised in §8.2. So the consumer has every column
and can pick whichever is `powerItem` *once its identity is known* — but the
library asserts no name it can't stand behind.

## The unblock

Naming these needs one of: the d4data GameBalance column-order schema (community
intel to verify against the blob, [[feedback_third-party-re-as-intel]]) or one
owner in-game oracle per column. Posted the boundary + the ask on #50.
