# 0034 — FR-C13 R2: all-classes legendary discovery + DT_STRING_FORMULA structure

*2026-05-20*

Owner directive: *"the API should handle all classes"*. Before any
implementation, this devlog catalogs every Legendary ParagonNode
across the 8 classes + the format-string AST shape inventory, and
documents what d4parse community RE reveals about the
`DT_STRING_FORMULA` binary layout.

## 72 legendaries, 8 classes

| Class | Legendary nodes |
|---|---:|
| Barbarian | 10 |
| Druid | 9 |
| Necromancer | 9 |
| Paladin | 9 |
| Rogue | 9 |
| Sorcerer | 9 |
| Spiritborn | 8 |
| Warlock | 9 |
| **Total** | **72** |

Per-power format strings dumped to
`fr-c13-all-legendaries.txt` (alongside this devlog) for reference.

## 32 distinct AST shapes across the 72 powers

Pattern abstracted by replacing literal numbers with `N` and any
`SF_<digit>` with `SF`. Top patterns by frequency:

| Count | Shape | Example |
|--:|---|---|
| 31 | `[SF*N|%x|]` | `[SF_0*100|%x|]` — multiplicative bonus |
| 24 | `[SF]` | `[SF_2]` — bare slot reference (durations etc.) |
| 22 | `[SF*N|x%|]` | same as above with different formatter order |
| 19 | `[SF * N|%x|]` | space variant |
| 13 | `[SF*N|%|]` | additive percent |
|  4 | `[SF * N|x%|]` | |
|  3 | `[SF|%|]` | |
|  3 | `[SF * N|%|]` | |
|  3 | `[SF*N|%+|]` | |

Beyond arithmetic, **engine function calls** appear:

- `[SF * PlayerHealthMax()]` — Barbarian *Warbringer*: `"For every [SF_0] Fury you spend, gain [SF_1*100|%|] of your Maximum Life ([SF_1 * PlayerHealthMax()]) as Fortify."`

So the format-string language includes:
- Arithmetic: `+`, `-`, `*` (and probably `/` in non-legendary powers; Demonic Spicules' inline `"SF_1 / 3"` formula uses `/`)
- Multi-term: `SF * SF * N`
- Function calls: `PlayerHealthMax()` and likely others
- Multiple formatter outputs per `[...]`: `[SF * N|%x||%|]` — two formatter directives, one bracket
- Formatter variants (~12 distinct): `|%x|`, `|x%|`, `|%|`, `|%+|`, `|+%|`, `|+|`, `|N%+|`, `|Nx%|`, `|N|`, `|%N|`, `|N|`, `|+|`

## DT_STRING_FORMULA layout (per d4parse Go source)

```go
type DT_STRING_FORMULA struct {
    // First 8 bytes skipped (unk_0x0 + unk_0x4)
    FormulaOffset  int32   // pointer to formula TEXT in blob
    FormulaSize    int32   // length of text
    CompiledOffset int32   // pointer to COMPILED form (AST bytes / float)
    CompiledSize   int32
    // Remaining 8 bytes (unk_0x18 + unk_0x1c) — purpose unclear

    Value          string  // populated from FormulaOffset/Size
    Compiled       string  // populated from CompiledOffset/Size (binary bytes, may include AST opcodes or just a raw float)
}
```

32 bytes total per header. **Value** carries the human-readable text
form of the formula (`"0.02"`, `"60"`, `"SF_1 / 3"`); **Compiled**
carries the binary form the engine actually executes — for trivial
numeric literals this is just a 4-byte IEEE-754 float; for
expressions it's an AST opcode encoding (which d4parse does not
decode either).

This confirms the empirical FR-C13 R1 finding: the slot records I
parsed (`[ASCII@0..3][type=0x06@4..7][float@8..11][pad@12..15]` and
variants) are the COMPILED form of `DT_STRING_FORMULA`s — the
binary representation pointed to by the headers.

The HEADER arrays are scattered earlier in the tail data; the
COMPILED data is contiguous at the end. The `Value` text in some
slots is itself a formula expression (Demonic Spicules' `"SF_1 / 3"`
case) — proving the engine evaluates these expression strings at
runtime, not just compiled opcodes.

## Implementation phasing (proposal)

Given the 72-power scope + AST shape variety + function calls + the
header/compiled split, full option (b-parsed) is at least 2–3
focused sessions. Phased plan:

**Phase 1 (one session): foundation.** Decode `DT_STRING_FORMULA`
headers per d4parse layout. Surface
`PowerDefinition.ScriptFormulas` as
`IReadOnlyList<ScriptFormula>` with `(Index, Text, RawCompiled,
LiteralValue)`. Trivial-numeric slots get `LiteralValue` populated
directly; expression slots get `Text` (e.g. `"SF_1 / 3"`) +
`RawCompiled` (the AST bytes) and `LiteralValue=NaN`. Tests anchor
against the 4 R1-anchored Warlock powers' slot values.

**Phase 2 (one session): expression evaluator.** Parse the `Text`
expression syntax (`+`, `-`, `*`, `/`, `()`, function calls) and
the format-string `[...]` placeholders + formatter directives.
Library-side resolver produces `IReadOnlyDictionary<string, double>`
SF_N → resolved value. Tests anchor against all 72 powers'
owner-confirmable values.

**Phase 3 (one session): AST opcode decoder (Compiled form).**
Decode the binary Compiled-form AST so the library doesn't depend
on the Value text expression parsing (engine-truth verification).
Cross-validate that the AST evaluation matches the Value-text
evaluation. This is the "belt and braces" — the Value text might
be a debug/comment form; the Compiled form is what the engine
runs.

## Disposition

Phase 1 starts next; cataloging here as a checkpoint so the scope
is visible. Each phase ends with a `[CASC]` delivery on
casc-fr#23 against the cumulative anchor set (4 → 9 → 72 powers).
