# 0030 — FR-C13 R1: Power-record SF_N slot table structural finding

*2026-05-19*

Picking up FR-C13 R1 ([casc-fr#23](https://github.com/WiseOwlSoftware/casc-fr/issues/23))
with the owner's relayed anchor data: 9 IEEE-754 floats across 4
distinct Warlock Legendary powers (Pyrosis / Fathomless / Overmind /
Ritualism). The byte-search probe locates the stored slot table and
confirms the 16-byte slot-record structure across all four powers.

## Probe shape

Read each Power blob via `Diablo4Storage.ReadSno(SnoGroup.Power,
snoId)`, byte-search the 9 anchor floats as little-endian IEEE-754
patterns, then scan for the 16-byte signature `+4 == 0x06`, `+12 ==
0x00` to enumerate every slot-record-shaped span in the blob, then
group contiguous spans (16-byte stride) into "runs". Inputs in
`e:/tmp/scene-probe/Program.cs` (FR-C13 R1).

## The 16-byte slot record

```
+0  char  Literal[4]     // ASCII text form of the value (".9", "9", "15", "10", ".45")
+4  uint  TypeTag = 0x06 // literal-float marker
+8  float BinaryValue    // IEEE-754 — the slot's numeric value
+12 uint  Padding = 0
```

Both forms are stored together: the engine renders the text form
directly when a formula evaluates to a slot reference; uses the
binary form for arithmetic.

## Per-power tail-data slot tables

Each Power's stored value table is the LAST contiguous run of 16-byte
slot records in the blob (preceding tail data carries formula chains
referenced by `Attacks_Per_Second_Total` and the `SF_N` ASCII
identifiers — see "Open structural questions" below). All tables are
terminated by a `("0", 0.0)` record.

| Power | SNO | Table @ | Slots (positional) | Trailing |
|---|---|---|---|---|
| Pyrosis    | 2527268 | 0x13E0 | `4.5`                                                  | `10`, terminator |
| Fathomless | 2521393 | 0x1740 | `0.15`, `7`, `6`                                       | `10`, terminator |
| Overmind   | 2524552 | 0x13F0 | `0.45` (0x3EE66667), `0.65` (0x3F266667)               | `10`, terminator |
| Ritualism  | 2526168 | 0x1720 | `0.9`, `9`, `15`                                       | `10`, terminator |

The trailing `"10"`/10.0 record appears IDENTICALLY across all four
powers, immediately before the `("0", 0.0)` terminator. Likely a
hardcoded sentinel (max-level / max-rank) the engine uses
universally rather than a per-power SF_N slot — but flagged as an
open question since CASC didn't decode its semantic role.

## Owner anchor verification

| Power | Slot | Owner-reported | Stored | Match |
|---|---|---|---|---|
| Pyrosis    | SF_0 | 4.5  | 4.5 (slot 0) | ✓ |
| Overmind   | SF_0 | 0.45 | 0.45000002 (slot 0) | ✓ (1-bit IEEE-754 rounding; owner gave 0x3EE66666, blob has 0x3EE66667) |
| Overmind   | SF_1 | 0.65 | 0.65000004 (slot 1) | ✓ (same 1-bit rounding) |
| Ritualism  | SF_0 | 0.9  | 0.9 (slot 0) | ✓ |
| Ritualism  | SF_1 | 9    | 9 (slot 1) | ✓ |
| Ritualism  | SF_2 | 15   | 15 (slot 2) | ✓ |
| Fathomless | SF_0 | 0.15 | 0.15 (slot 0) | ✓ |
| Fathomless | SF_2 | 6    | 6 (slot 2) | ✓ |
| Fathomless | SF_1 | 1.05 | **stored slot 1 = 7** — **mismatch** | see below |

For 3/4 anchored powers, **SF_N maps positionally to stored slot
index N**. For Fathomless, stored slot 1 = 7, not 1.05. But
`0.15 × 7 = 1.05` exactly — strongly suggesting **SF_1 for Fathomless
is a computed expression** (likely `SF_1 := SF_0 × stored_slot_1`,
where stored_slot_1 = 7 = max-stacks and 1.05 = per-stack × max =
cap value). The tooltip then renders `[SF_1*100|x%|]` = `1.05 × 100`
= `105%` cap.

## Open structural questions (the AST chain)

Each Power blob also contains MULTIPLE earlier runs of the same
16-byte signature, several preceded by ASCII identifiers. For
Ritualism:

- @ 0x0D0C run (58 entries, all 0/1 sparse) — preceded by
  `Attacks_Per_Second_Total` identifier. The default per-cost
  formula chain (constant across the 4 anchored powers).
- @ 0x1550 run (11 entries with `0`/`1`/`100`) — unidentified.
- @ 0x1620 run (10 entries) — preceded by `SF_2` ASCII identifier.
  Likely the SF_2 formula DEFINITION (an expression tree that
  resolves SF_2 to a value).
- @ 0x1720 run (5 entries) — the per-power stored slot table.

The `SF_N` ASCII identifiers preceding runs strongly suggest each
SF_N has its own formula-definition chain stored in the blob —
trivial for powers where SF_N := slot_N (identity), non-trivial for
Fathomless's SF_1 := SF_0 × slot_1. Decoding these AST chains is
the next-level RE step and is what unblocks the full SF_N → value
resolution.

## Why `nFormulaCount @ 0xB30` is 0 for all 4 powers

Verified: every anchored Power has `count@0xB30 = 0x00000000` and
`ptr@0xB38 = 0x00000000`. The community schema's
`PowerDefinition.arBuffs` / `arPayloads` / `arMods` arrays are also
empty for these Legendary passives (consistent with the Optimizer's
"Effects=[] on all 9/9 Warlock Legendary nodes" finding). The SF_N
slot table is therefore NOT pointed to by any of those schema
fields — it's in a SEPARATE tail-data region the schemas didn't
document, found by structural scan.

## Disposition

The structural finding is firm; the API design needs owner
clarification on **two specific questions** before code lands:

1. **Confirm the per-power stored slot tables** above against the
   owner's atlas-frame / tooltip oracle on all 4 powers (not just
   the anchored values — confirm ALL stored slot values including
   the trailing "10" + the Fathomless slot 1 = 7).
2. **SF_N → stored-slot mapping methodology** — for Fathomless,
   confirm that SF_1 = 1.05 is the *computed* value through a
   formula expression (`SF_0 × slot_1`), not a direct stored slot.
   This determines whether the library API surfaces:
   - (a) **Stored slot table only** — consumer evaluates SF_N
     expressions itself via the formula chains (parallel to FR-D's
     `GameBalanceFormulas`).
   - (b) **Decoded SF_N → value map** — library decodes the AST
     chains (the `SF_N`-named runs in tail data) and produces
     `{SF_0 = 0.15, SF_1 = 1.05, SF_2 = 6}` resolved-value pairs.

Option (b) is more useful for the consumer but requires decoding
the 16-byte AST opcode encoding the owner noted on Dynamism
(`0.0` / `1.0` / `6.0` + type-marker + arity). That decode is
tractable now with 4 powers' formula chains as cross-anchors, but
will take a follow-up round.

Disposition: bounce to owner on casc-fr#23 for the two clarification
asks; no code change this round per doc-only commit policy.
