# Devlog 0050 — FR-C16 R13: ParagonBoardUI disassembly — blocked by encrypted .text

*2026-05-21*

Owner directive: disassemble the `ParagonBoardUI` controller to verify the
literal per-widget `property ← data-source` wiring (upgrade the CL-51
activation table from name-inferred to wiring-confirmed).

## Tooling

Installed `capstone` 5.0.7 + `pefile` 2024.8.26 (full x86-64 disassembly).
Parsed `Diablo IV.exe` (PE32+, image base `0x140000000`): `.text`
`0x140001000` (40 MB), `.rdata` `0x1426D3000` (12 MB).

## Found: the data-binding source registry (in .rdata, readable)

`.rdata` carries a registry of `{evaluatorFnPtr→.text, argc:u64, inline
name}` records — ~115 named expression / data-binding functions (`RangeInt`,
`Floor`, `Table`, `ComputeCritChance`, …). The paragon-relevant entries:

| name | argc | note |
|---|---|---|
| **`ParagonNodeIsPurchased`** | 1 | the only per-node domain-state binding source |
| `ParagonGlyphAffixIsActive` | 1 | |
| `ParagonGetGlyphLevel` | 1 | |
| `ItemIsEquipped` / `AffixIsEquipped` | 1 / 0 | |
| `ParagonPowerBudgetMultiplierNode*` | 0–2 | formula sources (FR-C13) |

So **`ParagonNodeIsPurchased` is a real, registered, per-node data-binding
source** — IsPurchased is data-binding-confirmed (upgrades CL-51's
`NodeFact.Purchased` provenance). Notably, **no** `IsSelected`/`IsPurchasable`/
`IsLocated`/`IsRevealed` per-node source is registered — those node states are
widget-framework interaction states (per-state field family, CL-51) or
controller C++ logic, not named binding sources.

## Blocked: .text is encrypted at rest

The wiring (and the evaluator bodies) live in `.text`, which is
**encrypted/packed at rest** — confirmed:
- `.text` entropy = **7.999 / 8.0** (maximum; `.rdata` 6.09, `.data` 0.94).
- The entry point (`0x1421E47EC`) and every registry `fnPtr` disassemble to
  high-entropy noise.

This is D4's anti-tamper protection: code is decrypted in-memory at runtime
by the protector. **Static disassembly of the controller is impossible from
the on-disk binary** (not a tooling issue). Reading the literal wiring or the
`ParagonNodeIsPurchased` body would require a **runtime memory dump of the
decrypted process** — a heavier, anti-cheat-sensitive undertaking, an
owner-level decision, and outside the clean-room data-library scope.

## Disposition

Static EXE RE is exhausted. The binding mechanism is characterized as far as
the readable sections allow: named binding-source registry (`.rdata`) +
widget/asset naming convention + per-state field family. CL-51's typed
`NodeActivation` stands; `NodeFact.Purchased` is now registry-confirmed. The
remaining per-widget wiring is in encrypted code. No library change this
round — findings only. (Recon scripts: `e:/tmp` PE-parse / xref / registry
enumerators.)
