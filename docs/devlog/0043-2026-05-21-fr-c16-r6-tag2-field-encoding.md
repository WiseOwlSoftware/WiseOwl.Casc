# 0043 — FR-C16 R6: the tag-2 field-value encoding (grammar crack, not yet shipped)

*2026-05-21*

Idle-cycle RE following CL-47. Cracked the root cause behind CL-47's
deferred defect #3 (sparse-bound widgets mis-keying rect fields) — and
found it is bigger than `Node_Icon`: the UI-scene field grammar has a
**second value-record encoding** the parser does not yet read.

## The grammar

A widget's schema is the run of `(fieldHash, DT_BINDABLEPROPERTY,
DT_type)` triplets. Each schema field has exactly one **value record**,
keyed to the schema positionally, at a strict **0x38-byte cadence** after
the schema. A value record is **one of two shapes**:

- **`0x22` literal record** (56 bytes, value at +0x08) — the only shape
  the current §10.3 parser reads.
- **tag-2 block** (`u32 tag==2`, `u32 0`, `u32 value` at +0x08) — **not
  read** as a field value today. (The parser does harvest tag-2 blocks
  into `ExtraLayerValues`, but that is the *0x58 layer-block* use; see
  the overload note below.)

Different widgets use different encodings for the *same* fields:

| widget | encoding | decoded values (schema order) |
|---|---|---|
| `Node_IconBase` | all `0x22` (dense) | `1,7,3,7,7,7,3, 0x1D166DC7` ✓ (works today) |
| `Node_Icon` | **mixed** | `1, 28, 3, 28, 28, 28, 3, 0x25DAA956` |
| `Template_Board_Background_Center` | **all tag-2** | `1200, 0, 1200, −1, 0, 0x2954DF0C` |
| `Template_Board_Background_Top` | all `0x22` | `0, −1, 0, 0x900C7D87` ✓ |

## Why the current decode is wrong for tag-2 widgets

- **`Node_Icon`** (mixed): the parser reads only the 4 `0x22` records and
  positionally keys them to the *first 4 schema fields*, so the handle
  `0x25DAA956` lands on `nBottom` (CL-47's 635087190 garbage). True
  decode: a clean **symmetric 28-inset** symbol slot, handle on
  `hImageFrame`.
- **`Template_Board_Background_Center`** (all tag-2): the parser reads
  **zero** records → every field unbound → rect all-zero. True decode: a
  **1200×1200 authored rect** (matching the known 1200² center frame).
  The current "chrome carries no authored sub-rect" claim (FR-C11 /
  ParagonBoardChrome doc + the `ReadParagonBoardChrome` test) is an
  artifact of the unread tag-2 fields, **not** a fact.

## The overload that makes this non-trivial

tag-2 (`2, 0, value`) is used for **two different things** with identical
local structure:

1. **field values** — the schema-aligned 0x38-cadence run (this finding).
2. **0x58 layer-block handles** — the rarity/start/chrome composite layer
   stacks (FR-C8/C9, surfaced as `ExtraLayerValues`).

They are distinguishable by **position/cadence** (field values are the
contiguous 0x38-strided run immediately after the schema; layer blocks
come after, off-cadence) — but a naïve "count all tag-2 as field values"
pulls layer-block values into fields (verified: it gave the chrome center
a bogus extra value and broke the FR-C7 ratio). The correct reader must
bound the field-value run to the schema-aligned cadence.

## Why it is not shipped on this idle cycle

Reading tag-2 as field values **corrects** the decode — but it changes
**delivered** surfaces: FR-C7 render ratios, FR-C11/FR-C24
`ParagonBoardChrome` rects (the 1200² center, the rim sides), and the
"no authored sub-rect" documentation. Each changed value must be
validated as *correct* (not merely *different*), the pinned tests +
docs updated, and — because it revises decode the Optimizer has already
consumed — it warrants owner/Optimizer awareness rather than a unilateral
rewrite. CL-47's ±4096 rect guard remains the safe shipped state; it
contains the `Node_Icon` symptom without touching the delivered chrome
decode.

**Proposed next step (owner decision):** a dedicated round —
"FR-C16 R7 / tag-2 field-value reader" — that (1) bounds the field-value
run by the schema cadence, (2) re-validates the chrome 1200²,
`Node_Icon` 28-inset, and the FR-C7 ratios against the owner oracle,
(3) updates the affected tests + the ParagonBoardChrome "no authored
sub-rect" claim, and (4) retires the CL-47 guard in favour of the exact
decode.
