# 0094 — max legendary rank + the affix formula grammar (LIB-3 R7)

**Date:** 2026-07-17
**Work item:** casc-fr#45 (LIB-3 R7)
**CL:** CL-100 · `PowerDefinition.MaxRank` + `MaxLegendaryRank` + §8.1 grammar

The Optimizer's R7 wired in the CL-96 inline-formula contract — **492 uniques now
print** — and came back with the remaining ~741 that still can't print a number,
sorted into three blockers plus a residual I owed. This is the R7 answer.

## 1. `CurrentLegendaryRank()` — 630 aspects, and the max *is* in the data

The single highest-value item. `19+CurrentLegendaryRank()*0.5` isn't a roll —
it's rank-scaled and deterministic, so the honest presentation is a rank *span*,
which needs the max rank. The Optimizer's question: **is it in the data?**

Yes — and it was already decoded, we just threw it away. Every legendary Power's
script-formula tail ends with a `("10", 10.0)` sentinel that FR-C13 strips as an
"engine max-rank marker" (owner-confirmed R2 as the max-tier/rank cap). I
verified it's **universal, not per-record**: `SnoScan ranksentinel` over all
**699** `legendary_*` powers → 699/699 carry `("10",10.0)`, **zero** exceptions
or misses on `3.1.1.72836`. And it's a global, not an aspect field: `affixstr`
on `legendary_barb_011` shows the modifier holds exactly one string — the inline
formula — with no rank/clamp bytes (the `affixdump` "idx14 len 36" is float
bytes, not a string).

The powers also cross-reference the paired affix (`Affix.legendary_barb_001."Static
Value 0"`), confirming the g29 power and g104 affix are the same aspect — so the
power's rank-10 cap governs the affix's `CurrentLegendaryRank()`.

Rank is **1-based**: the dominant inline shape is `base + (CurrentLegendaryRank()
- 1)*k` (357 rows) — the `-1` is meaningless unless rank starts at 1 (base = the
rank-1 value). So the span is `[formula(1) … formula(10)]`.

Surfaced the sentinel value the decoder already saw as **`PowerDefinition.MaxRank`**
(data-driven — "it's in the data, here it is") plus the baked
**`PowerDefinition.MaxLegendaryRank` = 10** for the affix path (which doesn't
carry the sentinel), per the engine-constants pattern with a seasonal re-verify
trigger. A live test locks 699/699.

## 2. `PowerTag.X."Script Formula N"` — identifiable, not resolvable

All **86** references (on this build) point at one power, `S10ChaosTuningPerClass`
(2434194). It *is* readable — but `powersf` shows its script formulas decode to a
degenerate `"SF_0 + 0.0"` (NaN): the real per-class tuning values live in the
binary-AST opcodes (the `0x0B` markers) that the FR-C13 decoder explicitly
defers. So the cross-reference is **identifiable but not numerically resolved**
in this release — documented as the boundary, not claimed as done.

## 3. The grammar has ternaries and comparisons

Two unique affixes (`Chest_Unique_Paladin_001` et al.) use
`(S14_Mythic_UniquePotency > 0) ? a : b`. Three things §8.1's *function table*
couldn't express, now written as a **grammar**: a relational `>` and a ternary
`?:`; a **bare identifier that's a `DataAttributes` token by name**
(`S14_Mythic_UniquePotency` = `[280]`, byte-verified — ties the evaluator to the
FR-C27 namespace); and a **runtime-conditional** value (Mythic? → two ranges). I
documented the grammar's shape and precedence but did **not** guess the operator
semantics beyond "conventional C-style" — the engine table isn't oracle-confirmed
("looks conventional" is what R4 warned against).

## 4. The 32 residual, characterised

`1168 rollable-Desc − 1136 with-inline = 32` (matches the Optimizer's number
exactly). `rollableresidual` splits them honestly: **21 are `FormulaGbid`-backed**
(`Boost_Legendary_*` + Season set-seal Talismans — already computable via the
GBID path, not truly residual), and **11 have neither** — Skill-Rank *integer
grants*, set-powers referencing `Owner.*` runtime state, a mount-armor unique,
and one test affix. None is a decode gap; each is a value that isn't a simple
roll range.

## Discipline

The counts are scoped to the g104-inline predicate (630 / 86 / 2) and differ from
the Optimizer's rollable-predicate population (597 / 83) — flagged as a
population difference, not reconciled into a single figure
([[feedback_calibrate-claims-to-evidence]]). Max rank = 10 is data-evidenced
(699/699); the rank *floor* (whether the engine ever evaluates rank 0) is the one
detail an in-game oracle would pin, and I said so rather than assert it.
