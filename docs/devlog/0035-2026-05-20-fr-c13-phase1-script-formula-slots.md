# 0035 — FR-C13 Phase 1: Power Script Formula slot table (CL-37)

*2026-05-20*

Owner-authorized Phase 1 of the 3-phase FR-C13 plan (per
casc-fr#23 R3 sign-off 2026-05-20). Phase 1 delivers the foundation
layer: `PowerDefinition.ScriptFormulas` — the positional slot table
the engine resolves through `[SF_<i>n</i>...]` placeholders in
localized `Description` format strings.

## API surface

```csharp
public sealed class PowerDefinition
{
    // ... existing identity + localized text ...

    /// FR-C13 Phase 1 — the positional Script Formula slot table.
    /// Each entry's Index matches the engine's [SF_N...] format-string
    /// placeholders. Empty when the power has no slot table (most
    /// active skills); non-empty for the 72 legendary node powers.
    public IReadOnlyList<PowerScriptFormula> ScriptFormulas { get; }
}

public readonly record struct PowerScriptFormula(
    int Index,           // positional 0..N-1, matches [SF_N] in Description
    string Text,         // ASCII text form: "0.02" (numeric) or "SF_1 / 3" (expression)
    float LiteralValue)  // IEEE-754 float for numeric slots; NaN (Phase 2) for expressions
{
    /// <summary>True when Text is an arithmetic expression rather than
    /// a number literal — Phase 2's evaluator will resolve these.</summary>
    public bool IsExpression { get; }
}
```

## Decoder shape

Per the FR-C13 R1/R2 RE + d4parse `DT_STRING_FORMULA` schema, each
slot is a 16-byte compiled-form record. The decoder lifts the
Layout-A variant (the clean, common form across most of the 72
legendary powers):

```
+0..3   ASCII text     // e.g. ".15", "4.5", "60", "10", "0" — 1..3 chars + null
+4..7   type tag       // 0x00000006 = numeric-literal marker
+8..11  IEEE-754 float // the slot's binary value
+12..15 pad            // 0x00000000
```

Walk strategy: scan the blob from the end backward for the
universal `("0", 0.0, 0.0, 0.0)` terminator record. Anchor on it
and walk backward in 16-byte strides decoding each record. Stop
when a 16-byte block doesn't match Layout A. Reverse the collected
records to positional order. Strip the universal trailing
`("10", 10.0)` sentinel (the engine's max-rank marker, present on
every anchored legendary).

## Phase 1 anchor verification

6 of 9 Warlock legendaries fully decode under Layout A:

| Power | SNO | Decoded slots | Match |
|---|---|---|---|
| Pyrosis | 2527268 | `[("4.5", 4.5)]` | ✓ |
| Fathomless | 2521393 | `[(".15", 0.15), ("7", 7), ("6", 6)]` | ✓ |
| Overmind | 2524552 | `[(".45", 0.45000002), (".65", 0.65000004)]` | ✓ (IEEE-754 round-to-nearest = 1-bit higher than owner-relayed 0.45/0.65) |
| Ritualism | 2526168 | `[(".9", 0.9), ("9", 9), ("15", 15)]` | ✓ (slot 1=9 is the raw `[1+SF_1]`=10 input, owner-confirmed) |
| Chaos | 2527294 | `[("1", 1), ("2", 2), ("1", 1)]` | ✓ |
| Dynamism | 2524312 | `[(".03", 0.03), ("1", 1), ("1", 1), ("2", 2)]` | ✓ (4 slots; format string skips SF_1) |
| Dominion | 2524673 | `[("0.8", 0.8), ("0.5", 0.5), ("12", 12)]` | ✓ (engine SF_0=damage, SF_1=cost, SF_2=duration per format string) |
| **Greater Hex** | 2527280 | empty (Layout B) | Phase 2 |
| **Demonic Spicules** | 2525006 | empty (Layout B + expression record) | Phase 2 |

## Why Layout B/C are deferred to Phase 2

Greater Hex's slot table uses 4-character ASCII chunks like
`"0.75"`/`"0.25"` (no inline null in the 4-byte chunk), with the
type tag at offset +8 and float at offset +12 — distinct from
Layout A's type at +4 / float at +8. Demonic Spicules additionally
has an expression-text record (`"SF_1 / 3"` at 0x1A54) that's a
24-byte+ compound structure, not a 16-byte slot record. Both need
the Phase 2 expression evaluator to disambiguate cleanly — Phase 1
deliberately surfaces empty rather than fabricating partial decodes
under uncertain heuristics.

## No-fabrication discipline

The decoder returns an empty `ScriptFormulas` when the Layout-A
walk fails, rather than emitting partial / heuristic decodes. This
is the same discipline as the FR-C9 paragon-render no-fabrication
gates (CL-26/CL-27/CL-30/CL-31/CL-32/CL-34/CL-35) — surface only
what the decode pattern firmly establishes; future phases lift the
remaining cases.

## Phase 2 scope (next round)

- Lift Layout B (4-char ASCII chunks) + Layout C (pad-prefixed
  ASCII) for the remaining slot-table variations.
- Parse the format-string expression syntax (`+`, `-`, `*`, `/`,
  `()`, function calls like `PlayerHealthMax()`) per the discovery
  inventory (32 distinct AST shapes across 72 legendaries).
- Surface `IReadOnlyDictionary<string, double>` resolved
  `{SF_N → value}` map. Engine function calls go through a
  consumer-registered resolver delegate per casc-fr#23 R3 ask 2
  (option A: typed `FunctionRef` with resolver callback).
- Anchor against all 9 Warlock legendaries' expected SF_N values
  + the 18 additional (5 × ~3) anchored values from R2's expanded
  table.

## Deliverable

- `src/WiseOwl.Casc.Diablo4/PowerDefinition.cs`: ScriptFormulas
  property + PowerScriptFormula record struct + Layout-A decoder.
- `tests/WiseOwl.Casc.Diablo4.Tests/Diablo4StorageIntegrationTests.cs`:
  6-power anchor verification + 72-power no-crash sweep.
- `docs/casc-diablo4-format.md` §11.2 narrative + CL-37 entry.
- 42/42 tests green on `D:\Diablo IV` build `3.0.2.71886`.
- PR forthcoming after commit.
