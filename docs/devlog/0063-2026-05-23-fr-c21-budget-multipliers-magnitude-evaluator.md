# 0063 — FR-C21 build (1/N): budget multipliers + magnitude evaluator

2026-05-23 · CL-68 · branch `fr-c21-formula-eval-budget-multipliers`

## Trigger

The Optimizer signed off on FR-C21 (`casc-fr#33`, 2026-05-23): `Sno` as
the canonical aggregation key (CASC's correction accepted), full-
resolution scope (FlatValue carries the resolved magnitude, not just
the formula text) accepted, and "build to it." First slice of the
multi-CL build: the part with no upstream dependencies — the math.

## What ships

Two small public types on the FR-C21 path:

- **`ParagonPowerBudget`** — clean-room calibration table for the six
  budget-multiplier intrinsics the engine implements in code (absent
  from every shipped GameBalance data table, confirmed previously).
  Pinned empirically against owner in-game readings on build
  `3.0.2.71886`:

  ```
  MagicDefensive     = 10      MagicOffensive       = 2.5
  RareMajorDefensive = 4       RareMajorOffensive   = 5
  RareMinorDefensive = 4       RareMinorOffensive   = 5
  ```

  Plus `TryGetMultiplier(name, out value)` for the evaluator's lookup
  by canonical engine name
  (`ParagonPowerBudgetMultiplierNode<Rarity>{Major|Minor}<Off|Def>`).

- **`ParagonMagnitudeFormula.Evaluate(string)`** — the formula DSL
  subset: numeric literal, zero-arg intrinsic call, binary
  `+ - * /`, parens. Built on the existing internal
  `PowerScriptFormulaEvaluator` (the FR-C13 power-script evaluator)
  with a function resolver that delegates to
  `ParagonPowerBudget.TryGetMultiplier`. Returns `NaN` when the
  expression references an unknown intrinsic — future-build trip wire,
  never a fabricated number.

## Worked validations (in-game oracle)

| Formula text                                                            | Expected | Notes                                                       |
|---                                                                      |---       |---                                                          |
| `5`                                                                     | `5.0`    | Normal-rarity core stat — bare constant, no multiplier      |
| `0.75 * ParagonPowerBudgetMultiplierNodeMagicDefensive()`               | `7.5`    | `Generic_Magic_Armor` → +7.5% Total Armor                  |
| `3 * ParagonPowerBudgetMultiplierNodeMagicOffensive()`                  | `7.5`    | `Generic_Magic_DamageToElite` → +7.5% Damage to Elites      |
| `0.75 * ParagonPowerBudgetMultiplierNodeRareMajorDefensive()`           | `3.0`    | `Generic_Rare_AllResistance` → +3.0% All Resistance         |
| `1 * ParagonPowerBudgetMultiplierNodeRareMajorDefensive()`              | `4.0`    | `Generic_Rare_MaxLife` → +4.0% Max Life                     |
| `2 * ParagonPowerBudgetMultiplierNodeRareMajorOffensive()`              | `10.0`   | `Generic_Rare_Damage` → +10% Damage                         |
| `3.5 * ParagonPowerBudgetMultiplierNodeRareMajorOffensive()`            | `17.5`   | `Warlock_Rare_006` Demonology bonus → +17.5%                |
| `3 * ParagonPowerBudgetMultiplierNodeRareMinorOffensive()`              | `15.0`   | `Generic_Rare_CriticalDamage` → +15% Critical Damage        |
| `1.5/2 * ParagonPowerBudgetMultiplierNodeRareMajorDefensive()`          | `3.0`    | Division precedence — `1.5/2` binds tighter than the outer `*` |

Each row is a `Theory` case in
`B8_magnitude_formula_evaluates_to_expected_displayed_value`. The
live `Acceptance_matrix_against_live_install` extends with one
end-to-end check: read `Generic_Magic_Armor` (671247)'s
`NodeAttribute.FormulaGbid`, look up the formula text in the shipped
`AttributeFormulaTable` (sno 201912), evaluate to `7.5` — proves the
whole resolution path round-trips against game bytes.

## Reuse of the existing evaluator

The `PowerScriptFormulaEvaluator` (CL-29/CL-31, FR-C13) already
handles this exact grammar for power-script formulas — numeric
literals, identifier-call nodes, binary ops, parens. Paragon
magnitude formulas are a strict sub-grammar (no `SF_N` slots, no
binary-literal substitution). One thin wrapper
(`ParagonMagnitudeFormula`) supplies the budget-multiplier function
resolver and a no-op slot lookup; nothing else needed. Worth
keeping `PowerScriptFormulaEvaluator` internal — the paragon-facing
surface is what consumers see.

## Library-boundary reversal (FR-C21)

Appendix C amended: the boundary that read

> The library ships no formula evaluator at all, by decision.

now carries a carve-out for the FR-C21 node-info surface — the
magnitude evaluator + calibration table are in-scope. The narrower
"no formula evaluator" boundary still holds for power-script formula
output, glyph rank/radius scaling, item/affix value resolution, and
general `AttributeFormulaTable` evaluation. This was an explicit
owner direction on 2026-05-22 ("CASC delivers full resolution
(value+unit+name) for FR-C21").

## What's next

- **CL-69**: `ParagonNodeInfo` / `ParagonNodeStat` projection +
  `Catalog.GetNodeInfo(int sno)` + SNO-keyed decode cache. The
  per-node surface — magnitude resolved via this CL, plus StatName /
  Variant / Unit derived from the `Generic_<rarity>_<StatToken>`
  node-name convention. The Optimizer's chosen aggregation key
  (node SNO) lives here.
- **CL-70**: `Catalog.GetBoardNodes(int boardSno)` + `EnumerateNodes`
  — the consumer hot path with cell coords.
- **CL-71 (stretch)**: localized labels via `AttributeDescriptions`
  (sno 4080) if the Optimizer signals need.
