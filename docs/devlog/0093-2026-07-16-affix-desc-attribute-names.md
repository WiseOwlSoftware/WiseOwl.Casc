# 0093 — read-not-curated attribute names from item-affix Desc (FR-C27 R2)

**Date:** 2026-07-16
**Work item:** casc-fr#39 (FR-C27 R2)
**CL:** CL-97 · affix-`Desc` source in `GetAttributeName`

The Optimizer's R2 was precise and fair: CL-88 made `GetAttributeName`
season-*durable* (node tokens survive the engine's per-build id renumbering) but
not more *covering* — it still only resolves the ~40 curated `LabelByToken`
tokens, so **52% of live-referenced AttributeIds returned `null`**. "Re-keyed
the curation, didn't retire it." Correct.

## The read-not-curated source

LIB-3's finding gives a second source that needs no curation: an item-affix
`Desc` placeholder names the modified attribute —
`"[Crit_Percent_Bonus * 100|%|]"` — and that token **is itself an sno-4080
(`AttributeDescriptions`) key**:

```
[Crit_Percent_Bonus]     = +[{VALUE}*100|1%|] Critical Strike Chance
[DOT_DPS_Bonus_Percent]  = +[{VALUE}*100|%|] Damage Over Time
```

So `AttributeId → affix-Desc-token → sno-4080 → localized name` is a fully
data-driven pipeline, keyed by the current-build id. Built once (a full g104
scan → 77-entry id→token map) and cached, consulted after the node path in
`TryResolveBaseLabel`.

**Recursion gotcha:** the map builder must use the byte-only `AffixDefinition.Parse`
+ a direct sibling-`Desc` read, *not* `ReadAffix` — `ReadAffix` resolves effect
names via `GetAttributeName` → `TryResolveBaseLabel` → the builder, and the
cache isn't set mid-build → stack overflow. Caught it in the first probe.

## Measured — and scoped

Over the **85** live positive attribute ids that nodes (g106) + glyph affixes
(g112) reference:

| | before | after |
|---|---|---|
| `GetAttributeName` resolves | 41 (48.2%) | **58 (68.2%)** |

17 ids rescued (`707 → "Damage Over Time"`, `1207 → "Lucky Hit Chance"`,
`162 → "Maximum Resource"`, …), no regressions. This **does not fully close**
`#39`: ~32% (27 ids — node/glyph-only stats no affix references, e.g. `256`,
`322`) still return `null`. That's the honest residual, recorded, not "closes
the gap" — the discipline this session's meta-note demanded
([[feedback_calibrate-claims-to-evidence]]). Fully closing needs a node-side read
source (not yet located) or curation for the tail.

Tests (live `3.1.1.72836`, content-snapshot): `707/1207/162` resolve, `256`
stays null. 151/151 green. Spec §11.3 + Appendix A CL-97. Recon: `SnoScan coverfix`.
