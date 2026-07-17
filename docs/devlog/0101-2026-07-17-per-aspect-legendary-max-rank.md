# 0101 — legendary max rank is per-aspect, at affix +0x94 (#55)

**Date:** 2026-07-17
**Work item:** casc-fr#55 (FR-C38)
**CL:** CL-107 · `AffixDefinition.MaxRank` (corrects CL-100)

The Optimizer filed #55 correcting CL-100's `MaxLegendaryRank = 10`. The correction
itself corrected twice: first "the cap is 21" (from two aspects), then "it's
per-aspect (21/6/16…) and not in the affix record" (from four). Both prior readings
over-extrapolated from a handful of oracles — the same trap this project keeps
re-learning. The owner's four Codex-of-Power oracles are the ground truth:

| aspect | affix | game curRank/max · range | inline | cap |
|---|---|---|---|---|
| Edgemaster's | 578875 | 16/21 · [40–60] | `40+(rank-1)*1` | 21 |
| Conceited | 578845 | 18/21 · [40–60] | `40+(rank-1)*1` | 21 |
| Coagulation | 2591186 | 3/6 · [15–20] | `15+(rank-1)*1` | 6 |
| Glynn's Anvil | 2445175 | 8/16 · [25–40] | (cap term) | 16 |

## The find

The Optimizer's lead ("it's on the Power definition") was wrong, but productively
so — it ruled the Power record out and pointed at the per-aspect nature. `21` does
not appear as an int32/float anywhere in Edgemaster's/Conceited's Power records
(nor `16` in Glynn's). It's in the **affix** record: `affixdump` on Edgemaster
showed `+0x94 = 21`, Coagulation `+0x94 = 6`. Verified 4/4 exact (21/21/6/16), then
validated across the whole population with a new `SnoScan maxrankscan`:
**661/661** `legendary_*` aspect affixes carry `+0x94`, all in a sane 1..200 range,
distribution 21×394 / 11×82 / 16×79 / 6×19 / tail — exactly rank-cap shaped. The
four oracle spans reconstruct exactly as `[f(1) … f(MaxRank)]`.

Shipped as `AffixDefinition.MaxRank`. The neighbor cluster `+0x8C/+0x90` correlates
loosely with the cap but is not named (unverified); `+0x98` is a constant 16.

## What the "10" actually was

CL-100 read the `("10", 10.0)` record at every legendary Power's script-formula
tail as the rank cap. It isn't. On `legendary_generic_063` the tail is
`[Affix_Value_1#…/100] [100.0] ["10"/10.0] ["0"/0.0]` — a fixed **value-descriptor
footer**, universal *because it is not the rank cap*. The consuming check that
"confirmed" 10 re-read the library's own decode (tautological); the owner's oracle
refuted the semantic. `PowerDefinition.MaxRank` / `.MaxLegendaryRank` are removed;
the footer is still stripped from `ScriptFormulas` (it is not an SF_N), its value
no longer surfaced.

## Caveat (Glynn's Anvil)

A multi-value aspect's shown range isn't always `[f(1), f(MaxRank)]` of one term:
Glynn's per-Resolve bracket `[2.5–5.0]` ≠ `f(16)=4.0`; its "up to 32%" cap fits
`25+(rank-1)` at rank 16 and is a separate derived quantity. The span rule holds
per value-term, not per tooltip line.

## Discipline

0.7.0 shipped the wrong cap (published to NuGet); CL-107 is a breaking change
(owner-sanctioned — zero consumers). The lesson, third time this session: don't
declare a constant/boundary from partial coverage. Here the fix was a *field
offset* that reproduces 3 distinct caps across 4 oracles and 661 records — far
stronger than any single-value extrapolation.
