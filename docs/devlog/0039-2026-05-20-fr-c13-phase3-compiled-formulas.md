# 0039 — FR-C13 Phase 3: compiled-form AST decoder + cross-validation (CL-41)

*2026-05-20*

Optimizer R5 consume-verified Phase 2 (CL-40) and authorized Phase 3
start (casc-fr#23 R5, 2026-05-20). Phase 3 closes the FR by decoding
the engine-compiled binary AST for expression slots and surfacing the
per-power binary-derived `{SF_N → value}` map for the R5 regression
gate.

## What landed

### 1. Expression-record decoder (type=0x05)

The Phase 1/2 backward-walk halted on Demonic Spicules because its
slot table interleaves a 48-byte type=0x05 *expression record*
between the literal slots (SF_0 = `"0.02"`, SF_1 = `"60"`) and the
trailing `("10", 10.0)` sentinel. Phase 1/2 found 0 SF_N slots for
Demonic Spicules; Phase 3 finds all 3.

The 48-byte expression record, anchored on Demonic Spicules's
SF_2 = `"SF_1 / 3"`:

```
+0..3   pad = 0
+4..15  ASCII text (NULL-terminated within 12 bytes — "SF_1 / 3\0\0\0\0")
+16..19 type tag = 0x05  (expression marker)
+20..23 opcode marker    (observed = 7 on the anchor; opaque)
+24..35 pad = 0
+36..39 type tag = 0x06  (embedded-literal marker)
+40..43 IEEE-754 single  (binary operand value — 3.0f on the anchor)
+44..47 trailing opcode  (observed = 0x0E; opaque)
```

The 4-byte pad following the record explains the 52-byte backward
stride. The decoder now tries `-16`, `-20`, and `-52` strides in
order. The `-52` candidate must be a genuine type=0x05 record start
(not any literal) — accepting a literal at `-52` would jump past the
real slot region into early-tail literal blocks (Pyrosis, etc., have
runs of `(0,1,100)` records preceding the slot table that aren't
SF_N entries).

### 2. `PowerDefinition.CompiledFormulas` API

```csharp
public sealed class PowerDefinition
{
    // Phase 1 (CL-37)
    public IReadOnlyList<PowerScriptFormula> ScriptFormulas { get; }
    // Phase 2 (CL-40)
    public IReadOnlyDictionary<string, double> ResolvedFormulas { get; }
    public IReadOnlyList<PowerFunctionRef> FunctionRefs { get; }
    // Phase 3 (CL-41)
    public IReadOnlyDictionary<string, double> CompiledFormulas { get; }
}
```

`CompiledFormulas` is the engine-truth `{SF_N → value}` map derived
from the binary compiled records:

- **Literal slots (type=0x06):** value = the IEEE-754 single read
  directly from the slot record's float position (+8 in Layout A,
  +12 in Layout B/C). Identical to
  `PowerScriptFormula.LiteralValue` promoted to `double`.
- **Expression slots (type=0x05):** operator tree from the text;
  numeric operands substituted from the binary AST opcode region's
  embedded IEEE-754 singles in left-to-right encounter order. So
  Demonic Spicules's `SF_2` evaluates as
  `SF_1 / binary_literal[0]` = `60 / 3.0f` = `20`; the `3.0f`
  comes from the compiled record's bytes at +40, **not** from
  re-parsing the text `"3"`.

The text/binary split surfaces engine-compiled inconsistencies if
they ever appear: if the engine ever stores text `"0.02"` but binary
`0.025f`, `ResolvedFormulas` says `0.02` and `CompiledFormulas` says
`0.025`. The R5 regression gate catches it.

### 3. `PowerScriptFormulaEvaluator.EvaluateWithBinaryLiterals`

New overload that takes an optional `IReadOnlyList<float>?
binaryLiterals` parameter. Evaluation walks the AST normally, but
each `NumberNode.Evaluate` consumes the next binary literal in
left-to-right encounter order when the list is non-empty (falls
back to the text-parsed `Value` when the queue is exhausted, so
partial-binary contexts still produce a number).

The shared `EvalContext` replaces the prior signature
`Evaluate(Func, Func?, List)` — same behavior for the existing
`Evaluate` entry point, just plumbed through a context object so
the binary-literal cursor can be threaded.

## Cross-validation results — R5 regression gate

9 Warlock legendary anchors:

| Anchor | SF_N count | Phase 2 == Phase 3 |
|---|--:|---|
| Pyrosis | 1 | ✓ |
| Fathomless | 3 | ✓ |
| Overmind | 2 | ✓ |
| Ritualism | 3 | ✓ |
| Chaos | 3 | ✓ |
| Dominion | 3 | ✓ |
| Dynamism | 4 | ✓ |
| Greater Hex | 2 | ✓ |
| **Demonic Spicules** | **3** | **✓ (SF_2 = 20 via both paths)** |
| **Total** | **24 keys** | **24 agree, 0 disagree** |

Demonic Spicules is the load-bearing anchor — its `SF_2 = "SF_1 / 3"`
goes through the binary literal path. Phase 2 parses the text `"3"`
to `3.0`; Phase 3 reads the IEEE-754 single from the compiled
record's bytes at +40 to `3.0f`. Both produce `60 / 3 = 20`. The
agreement confirms the engine compiled the text and binary forms
consistently.

72-power no-crash sweep: **73 powers (the 72 legendary set + 1
ambient match), 59 with slot tables, 59 with non-empty
`CompiledFormulas`, 0 threw.**

## Out of scope — AST opcode interior

The `0x07` (after type=0x05 at +20) and `0x0E` (after the embedded
literal at +44) markers are not yet decoded — treated as opaque on
the anchor. The 48-byte record shape only covers the
single-binary-operator, single-literal-operand case (Demonic
Spicules's `"SF_1 / 3"`). More complex AST shapes
(multi-operator, function-call operands, parens, no-literal
references) would need additional record-shape RE and additional
test anchors.

Practical impact: zero powers in the 72-power sweep hit a more
complex expression-record shape — all expression-text slots
encountered in the live build are either pure-literal (Phase 1/2)
or fit the Demonic Spicules `"SF_N op literal"` template. If
future build patches introduce more complex compiled-form ASTs,
the decoder will return a literal-only slot table for those
specific powers (the expression record won't validate the
type=0x05 marker pattern) — which is correct fallback behavior
(no fabrication), but loses the SF_N values for those slots.

## Files touched

- `src/WiseOwl.Casc.Diablo4/PowerDefinition.cs` — new
  `CompiledFormulas` property, extended `DecodeScriptFormulas`
  with expression-record detection, new
  `TryReadExpressionRecord` + `TryReadAnyRecord` helpers, new
  `ResolveCompiledFormulas` + `EvalSafeWithBinaryLiterals`.
- `src/WiseOwl.Casc.Diablo4/PowerScriptFormulaEvaluator.cs` —
  new `EvaluateWithBinaryLiterals` entry point, new `EvalContext`
  passed to all `Node.Evaluate` overrides.
- `tests/WiseOwl.Casc.Diablo4.Tests/Diablo4StorageIntegrationTests.cs`
  — two new tests:
  `PowerDefinition_phase3_compiled_formulas_match_resolved_for_9_warlock_anchors`
  (the R5 regression gate) and
  `PowerDefinition_phase3_decodes_demonic_spicules_expression_slot`
  (the canonical expression-record anchor).
- `docs/casc-diablo4-format.md` §11.2 — Phase 3 section + the
  48-byte expression-record layout + the cross-validation gate
  table.

## Test result

`dotnet test`: **45/45 green** on `D:\Diablo IV` build
`3.0.2.71886` (Phase 1: 1 anchor + sweep, Phase 2: 9 anchors +
function refs, Phase 3: 9-anchor cross-validation + Demonic
Spicules + sweep).
