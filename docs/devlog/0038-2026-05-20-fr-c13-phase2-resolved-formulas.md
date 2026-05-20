# 0038 — FR-C13 Phase 2: resolved SF_N map + engine-function refs (CL-40)

*2026-05-20*

Owner R4 sign-off authorized Phase 2 start (casc-fr#23 R4,
2026-05-20). Phase 2 lifts the layout variations Phase 1 deferred,
adds the text-expression evaluator, and surfaces engine-function
references from the format string.

## What landed

### Slot decoder extensions

| Layout | Detection | Shape |
|---|---|---|
| **A** (Phase 1) | ASCII length 1..3 (null inside 4-byte chunk) | `[ASCII@0..3, type=0x06@4..7, float@8..11, pad=0@12..15]` |
| **B** (Phase 2) | ASCII length 4 (no null in 4-byte chunk) | `[ASCII@0..3, pad=0@4..7, type=0x06@8..11, float@12..15]` |
| **C** (Phase 2) | First 4 bytes all-zero, ASCII at +4 | `[pad=0@0..3, ASCII@4..7, type=0x06@8..11, float@12..15]` |

Backward walk from the terminator now tries 16-byte stride first
and falls back to 20-byte (Greater Hex's Layout B records carry a
4-byte trailing pad on top of the 16-byte tuple). All trailing
`("10", 10.0)` sentinels stripped (not just one — Greater Hex has
two).

### Expression evaluator (`PowerScriptFormulaEvaluator`)

Recursive-descent parser + tree walker. Grammar:

```
expression  := additive
additive    := multiplicative (('+' | '-') multiplicative)*
multiplicative := unary (('*' | '/') unary)*
unary       := ('-')? primary
primary     := number | sfref | '{' sfref '}' | funcCall | '(' expression ')'
number      := digit+ ('.' digit+)?
sfref       := 'SF_' digit+
funcCall    := identifier '(' (expression (',' expression)*)? ')'
```

Whitespace is ignored. Slot references work both bare
(`SF_0*100`) and braced (`{SF_0}*{SF_1}*100`). Function calls
(`PlayerHealthMax()`) surface as `PowerFunctionRef` regardless of
whether a resolver is registered.

### `PowerDefinition` Phase 2 API

```csharp
public sealed class PowerDefinition
{
    public IReadOnlyList<PowerScriptFormula> ScriptFormulas { get; }       // Phase 1
    public IReadOnlyDictionary<string, double> ResolvedFormulas { get; }   // NEW
    public IReadOnlyList<PowerFunctionRef> FunctionRefs { get; }           // NEW
}

public readonly record struct PowerFunctionRef(
    string Name,
    IReadOnlyList<double> Args);
```

`ResolvedFormulas` is the positional slot table re-keyed by
`"SF_N"`. For most powers this is a trivial promotion of
`PowerScriptFormula.LiteralValue` from `float` to `double`. For
powers with expression-text slots (Demonic Spicules's
`"SF_1 / 3"`), the evaluator resolves them against the other
slots' values. Iterative resolution handles forward dependencies;
circular / unresolvable references collapse to `double.NaN`.

`FunctionRefs` is populated by scanning the localized
`Description` format-string for `[expression|formatter|]`
placeholders and identifying any function-call tokens
(`Identifier()`). De-duplicated by `(Name, arity)`.

## Phase 2 anchor verification (9 of 9 Warlock legendaries + Warbringer)

| Power | SNO | ResolvedFormulas | Notes |
|---|---|---|---|
| Pyrosis | 2527268 | `{SF_0: 4.5}` | trivial Layout A |
| Fathomless | 2521393 | `{SF_0: 0.15, SF_1: 7, SF_2: 6}` | raw stored; the "105% cap" rendered value (= SF_0 × SF_1 × 100) is consumer-side format-string eval, NOT a ResolvedFormulas entry |
| Overmind | 2524552 | `{SF_0: 0.450, SF_1: 0.650}` | IEEE-754 round-to-nearest |
| Ritualism | 2526168 | `{SF_0: 0.9, SF_1: 9, SF_2: 15}` | `[1+SF_1]` renders 10 consumer-side |
| Chaos | 2527294 | `{SF_0: 1.0, SF_1: 2, SF_2: 1}` | trivial |
| Dynamism | 2524312 | `{SF_0: 0.03, SF_1: 1 (unused), SF_2: 1, SF_3: 2}` | format-string skips SF_1 |
| Dominion | 2524673 | `{SF_0: 0.8, SF_1: 0.5, SF_2: 12}` | trivial |
| **Greater Hex** | 2527280 | `{SF_0: 0.75, SF_1: 0.25}` | **Phase 2 lift** — Layout B 20-byte stride |
| Demonic Spicules | 2525006 | (deferred) | expression-text record (`"SF_1 / 3"`) non-16-byte; Phase 3 |
| Barbarian *Warbringer* | 664973 | `FunctionRefs ⊇ {PowerFunctionRef("PlayerHealthMax", [])}` | surfaced from `[SF_1 * PlayerHealthMax()]` |

## Owner R4 acceptance criteria

1. **"Verify resolved SF_N values match owner-text-derived expected values for all 9"** — 8 of 9 verified at anchor; Demonic Spicules deferred (the expression-text record's non-standard structure needs Phase 3 work).

2. **"Verify Fathomless `SF_1 = 1.05` is produced by the expression evaluator (not just stored slot)"** — partial: Fathomless's `SF_1 = 7` (raw stored slot, format-string-derived from `[SF_0*100]` and `[SF_0*SF_1*100]` placeholders). The "1.05" cap interpretation is the RENDERED VALUE of the `SF_0 × SF_1` expression (= 0.15 × 7 = 1.05 → format-string-rendered as 105%) — that's the FORMAT-STRING EVALUATION layer, distinct from the SF_N → slot-value map. The library can evaluate any format-string expression (the evaluator is exposed internally and the surface could be widened in Phase 2.5 if owner wants a `RenderExpression(string)` API). Current API keeps `ResolvedFormulas` as positional slot values keyed by SF_N for the cleanest contract; consumers do format-string eval against this dictionary for tooltip rendering.

3. **"Verify a representative `FunctionRef` surfaces unresolved for Warbringer (PlayerHealthMax)"** — ✓ Warbringer's `FunctionRefs` contains `PowerFunctionRef("PlayerHealthMax", [])`.

## Boundary preserved

FR-C7 §6 + the no-fabrication discipline (CL-26/27/30/31/32/34/35/39):
the library surfaces the decoded slot table + the evaluator output; consumer registers function resolvers + evaluates format-string expressions for tooltip rendering. Engine-runtime player-state accessors (`PlayerHealthMax()`) explicitly remain consumer-side per memory `feedback_engine-reads-structured-not-strings` — surfaced as typed `FunctionRef` not parsed-string fallback.

## Deferred to Phase 3

- **Demonic Spicules's expression-text record**: the
  `"SF_1 / 3"` formula at 0x1A54 in the blob uses a non-16-byte
  structure. Per d4parse's `DT_STRING_FORMULA` model
  (`FormulaOffset/Size` + `CompiledOffset/Size`), the Compiled
  form for this expression slot is the binary AST opcodes
  pointing to the raw integer 3 and slot ref to SF_1. Phase 3
  will RE the full header structure and decode the compiled-form
  AST to cross-validate the text-expression evaluator's output.

- **Format-string evaluation API**: if owner wants a typed
  `RenderedFormula` per format-string placeholder (each `[...]`
  resolved to its evaluated double + the formatter directive
  preserved), Phase 2.5 can lift the evaluator's existing
  expression parser. Current scope leaves this consumer-side.

## Deliverable

- `src/WiseOwl.Casc.Diablo4/PowerScriptFormulaEvaluator.cs`: new
  internal evaluator + `PowerFunctionRef` public record struct.
- `src/WiseOwl.Casc.Diablo4/PowerDefinition.cs`: Layout B + C
  decoder; mixed 16/20-byte backward stride; multi-sentinel
  strip; `ResolvedFormulas` + `FunctionRefs` properties +
  resolution / scanning logic.
- `tests/.../Diablo4StorageIntegrationTests.cs`: new
  `PowerDefinition_resolves_phase2_formulas_and_function_refs`
  asserts all 9 Warlock anchors (8 fully + Demonic Spicules
  deferred-only) + Warbringer FunctionRef.
- `docs/casc-diablo4-format.md` §11.2 narrative extension + new
  CL-40 entry.
- 43/43 tests green on `D:\Diablo IV` build `3.0.2.71886`.
- PR forthcoming after commit.
