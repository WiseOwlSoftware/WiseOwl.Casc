# 0091 — formula function contracts (LIB-3 R3) + a coverage correction (FR-C31 R2)

**Date:** 2026-07-16
**Work items:** casc-fr#45 (LIB-3 R3), #46 (FR-C31 R2)
**CL:** CL-95 · spec §8.1 + `FormulaRange` XML + a CL-93 correction

Two Optimizer counter-rounds, both landed after it independently verified the
prior CLs (CL-94's `idx16→AttributeFormulas` decode reproduced 3,218/3,218,
zero dangling; CL-92's `arModifiers` structure re-derived from raw bytes).

## LIB-3 R3 — record the formula function contracts

CL-94 gave the consumer the value *formula*; R3 asked for the function
*semantics* so it can print a number, explicitly **not** an evaluator. The
functions are engine C++, so the honest question was: what's determinable from
the 1038-entry `AttributeFormulas` table without the binary?

**Answer: the printed min/max is fully determinable; two residuals are engine-
defined but don't affect it.** The proof is the `GearAffix_CritChance` ladder —
`FloatRandomRangeWithInterval(1,0.5,1)/100` → 0.5–1 %, `(1,1,1.5)/100` → 1–1.5 %,
… `(3,3.5,5)/100` → 3.5–5 %. Args 2 and 3 track the displayed band bounds
exactly; arg 1 (1/2/3) is the granularity and doesn't move them. So:

| function | contract | min / max |
|---|---|---|
| `FloatRandomRangeWithInterval(g, min, max)` | value in `[min,max]`, `g`-step granularity | args 2, 3 |
| `RandomInt(lo, hi)` | inclusive integer | `lo`, `hi` |
| `IPower()` | item power; range-selected by `nItemPowerRangeStart` | deterministic |
| `ROUND` | nearest int; tie-break **engine-defined** (≤1, half-integer only) | deterministic |
| `Max`/`Pin`/`Pow`, budget multipliers, `GetTotalAffixBonus`, `CurrentLegendaryRank` | as named | deterministic |

**Min/max rule:** evaluate the row twice — each roll fn at its low arg, then its
high arg — and clamp both to `[RangeValue1, RangeValue2]`.

**`RangeValue1/2` are output clamps, not the roll spread** (the Optimizer's
point 5, confirmed): across 2580 ranges they collapse to round,
formula-independent bounds — `(0,100)`×493 for percentages, `(1,9999)` for core
stat, `(0,99999)`×547 for large stats. Clarified on the `FormulaRange` XML so a
consumer can't mistake them for the min/max.

Recorded in spec §8.1; the two genuinely engine-defined residuals (`g`'s exact
step count, `ROUND` tie-breaking) are flagged as such and shown not to affect
the printed range.

## FR-C31 R2 — a correction I owed

The Optimizer falsified my CL-93 delivery claim *"no current coverage is lost."*
It's wrong: three ids — **954, 1120, 1124** — are live-referenced by group-112
glyph affixes, are not reachable by the `Generic_`-node scan, and CL-93's
stable-range restriction now sends them to `null`. Small, and `null` over a
wrong name is still the right trade, but the claim as written was false and is
corrected in the record (Appendix A CL-95).

And the root cause was misattributed: these aren't stale predecessors, they're
**reassigned ordinals**. The multiplicative-variant id is the additive id **+1**
in the same engine namespace — verified: 40 of 44 `Mult*` glyph-affix ids
resolve via `id-1` to their exact stat (`1124→1123` "Damage while Healthy",
`954→953` "Damage to Elites", `1120→1119` "Damage to Healthy Enemies",
`737→736` "Vulnerable Damage"). The same additive-at-N / `Multiplicative_`-at-
N+1 convention appears in the `DataAttributes` pairs (`251`/`252`, `253`/`254`).

**Not shipped as a blind `id-1` fallback** — it over-applies: `162→161` gives
the truncated "Maximum", and `253/255/260` are the FR-C28 compound-base family
whose bare label ("Damage") is useless without `ParamPlus12`. `GetAttributeName`
is namespace-blind (id only), so it has no safe signal to gate `id-1` on. The
correct resolution for these live glyph-affix ids is the affix-`Desc` name
source the Optimizer already green-lit under FR-C27 (`#39`) — the glyph affix's
own text names the stat directly. Recorded as the route; not smuggled in here.

Spec §8.1 + §11.3; Appendix A CL-95. Recon: `SnoScan formuladump` / `multcheck`.
