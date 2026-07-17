# 0092 — inline value formulas: where unique-power rolls live (LIB-3 R5)

**Date:** 2026-07-16
**Work item:** casc-fr#45 (LIB-3 R5)
**CL:** CL-96 · `AffixEffect.InlineFormula` + `NoFormula` sentinel fix

The Optimizer went to cash the CL-94 line "*your `[roll]` placeholder now has a
real source for all 305 uniques*" and it didn't hold: the `idx16 →
AttributeFormulas` chain resolves **gear** stat affixes; on a unique/legendary
power, `idx16` is the NoGbid sentinel and the chain returns nothing (0/509 of
their `[Affix_Value_N]` affixes resolved). That claim was broader than the
evidence — a pattern worth naming, and named ([[feedback_calibrate-claims-to-evidence]]).
R5: **where do unique-power `Affix_Value_N` rolls actually live?**

## Answer: inline in the record

Dumping `2HMace_Unique_Barb_100` (Death Blow shockwave, `[Affix_Value_1|%|]`),
the operand region past the effect VLA is ASCII:

```
FloatRandomRangeWithIntervalUniqueAffixPityBonus(20, 60, 80)
```

The roll formula is stored **inline** as a `DT_STRING_FORMULA` in the affix
record, not referenced by GBID. It is located by the same modifier's `idx10`
(payload offset) / `idx11` (length) — verified across uniques (`578742`
idx10=480/len=68, `578750` idx10=472/len=59, `578782` idx10=488/len=60, each
matching its string). By §8.1, args 2/3 = the range → **60–80%**. Shipped as
`AffixEffect.InlineFormula`; a consumer resolves a modifier's value as
`FormulaGbid` curve (gear) → else `InlineFormula` (unique) → else `StaticValues`.

## Coverage — measured, not asserted

Of the **1,168** affixes whose `Desc` carries a rollable `[Affix_Value_N]`,
**1,136 (97.3%)** carry a decodable inline formula via the `idx10`/`idx11`
descriptor. **32 do not** — a residual, recorded rather than rounded away. So
the honest statement is "1,136 of 1,168", not "all uniques".

## A sentinel bug this surfaced

`AffixEffect.NoFormula` was `0` in CL-94. But `idx16` is **never** `0` — across
5,871 modifiers it is either a real GBID or `0xFFFFFFFF`. The true NoGbid
sentinel is `0xFFFFFFFF`, so a consumer testing `FormulaGbid != NoFormula` was
treating every unique's `0xFFFFFFFF` as a resolvable GBID (the dogfood showed
`formula=0xFFFFFFFF(unresolved)`). Fixed to `0xFFFFFFFF`.

## Credit where the earlier claim did land

`StaticValues` genuinely resolves the *`[Affix."Static Value N"]`* placeholder
(a different token from `[Affix_Value_N]`) — the fixed set/mythic/unique
numbers, ~1,030 affixes. That part of CL-94 was right; it just isn't the roll
token.

Tests (live `3.1.1.72836`, content-snapshot): `2HMace_Unique_Barb_100` →
`InlineFormula`; gear crit → GBID + empty inline; `NoFormula == 0xFFFFFFFF`.
151/151 green. Spec §11.3 + §8.1; Appendix A CL-96. Recon: `SnoScan inlineformula`.
