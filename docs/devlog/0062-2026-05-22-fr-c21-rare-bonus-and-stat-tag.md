# 0062 — FR-C21 deferred RE: rare bonus mechanic (`@48`/`@64`) + `StatTag` group 124

2026-05-22 · CL-67 · branch `fr-c21-bonus-mechanic-stat-tag`

## Trigger

`casc-fr#33` (FR-C21) sat `awaiting:optimizer` for sign-off on the
node-SNO-as-canonical-key correction; every other open issue was
`needs:owner` or `awaiting:optimizer`. Per the owner-authorized
autonomous loop, with nothing for CASC's queue, close the deferred RE
debt CL-66 called out: the two undecoded node-level descriptors on
`ParagonNodeDefinition` (`@48`, `@64`) and the group-124 records the
latter references. Both are the data foundation for the conditional
half of a rare paragon node's in-game text:

> *+X% [StatA], +Y% [StatB] — Bonus: another +Z% [stat] when N [stat] met.*

## The bytes

A rare node carries two `DT_VARIABLEARRAY[DT_SNO]` descriptors that are
absent (size 0) on every other observed node kind (Common, Magic, Start,
Gate, Socket):

```
ParagonNodeDefinition payload  (rare-only fields shown)
  @+48 → DT_VARIABLEARRAY[DT_SNO]   (size 4: a single slot, observed 0)
  @+64 → DT_VARIABLEARRAY[DT_SNO]   (size 4 or 12: group-124 StatTag SNOs)
```

Sampled across all rare-shaped + non-rare nodes inspected with
`SnoScan rawhex/nodeinfo`:

| Node                       | `@48` size, value | `@64` size, values                     |
|---                         |---                |---                                     |
| Warlock_Rare_006 (2451111) | 4, `[0]`          | 4, `[WillpowerMain2]`                  |
| Generic_Rare_001 (679732)  | 4, `[0]`          | 12, `[Barb_Strength+Dexterity, DexteritySide2, StrengthSide2]` |
| Generic_Magic_Armor        | *empty*           | *empty*                                |
| Generic_Socket             | *empty*           | *empty*                                |
| Generic_Gate               | *empty*           | *empty*                                |

`@48`'s slot is `0` on every rare we've sampled — the canonical purpose
is unconfirmed (the field name has not been recovered, no rare we've
seen authors a non-zero value, and the empirical signal is consistent
with a reserved "bonus passive power" hook that lives in the schema
but is intentionally unused in current content). It is surfaced raw
(`int`) with `-1` reserved for "no descriptor" so a consumer can still
distinguish "non-rare" from "rare with empty slot." When and if a
populated rare appears, the surface needs no API change.

`@64` is the real foundation: each element is a group-124 `StatTag`
SNO id. The class-specific Warlock rare lists exactly one tag
(`WillpowerMain2` — the Willpower / "Main" tier 2 threshold); the
class-generic rare lists three class-keyed alternatives
(`Barb_Strength+Dexterity` for the Barbarian's two-stat composite,
`DexteritySide2` for Side tier 2 Dexterity, `StrengthSide2` for the
same Strength tier). At runtime the engine picks the alternative
matching the player's class.

## Group 124 — `StatTagDefinition`

The referenced records were not previously read by this library.
Decoded layout (verified on build `3.0.2.71886`):

```
StatTagDefinition payload
  @+0  DT_INT snoId
  @+64 DT_VARIABLEARRAY[DT_CHAR]   → ASCII formula text (NUL-terminated;
                                     the NUL is counted in dataSize)
  @+80 DT_VARIABLEARRAY            → pre-parsed token stream (engine
                                     bytecode equivalent — not modelled;
                                     the text is the authoritative source)
```

Worked example — `WillpowerMain2` (SNO `1068426`):

```
@64 descriptor → dataOffset=96, dataSize=37
@96 ASCII       "760 + (455 * ParagonBoardEquipIndex)" + NUL
```

Three forms of tag, all read through the same shape:

- **Simple** (`WillpowerMain2`, `StrengthSide1`, …) — a linear formula
  in `ParagonBoardEquipIndex`. Cross-validated by the live oracle:
  Fathomless (a Warlock rare node binding) shows `2125` Willpower
  required in-game ⇒ `760 + 455×3` ⇒ `ParagonBoardEquipIndex == 3`
  for Fathomless's board.
- **Composite** (`Barb_Strength+Dexterity`, …) — the descriptor at
  `@64` still points at the primary formula text, but at a later
  offset in the payload; additional sub-records carry the
  per-alternative stats. The primary text is what this CL surfaces;
  the composite sub-structure is open follow-up.
- **Glyph-keyed** (`Glyph_Willpower_Main`, …) — the text is a bare
  numeric constant (`"40"`). Same descriptor shape; the consumer
  interprets it as a literal.

## What I did *not* model

- **The bonus stat itself.** "Bonus: another +Z% [StatA]" — the `+Z%`
  magnitude and which `eAttribute` it modifies are not surfaced.
  Strongest candidate is the rare-only "extra" entry in the
  per-attribute GBID array at `@88`: on every rare sampled
  `gbidArray.Count == ptAttributes.Count + 1` (2 attrs ⇒ 3 entries).
  The first N entries are byte-for-byte the existing attribute GBIDs;
  the +1 entry is node-specific (Warlock_Rare_006 → `0xAC62A180`,
  Generic_Rare_001 → `0x6D91307D`). Verifying the linkage and
  identifying the magnitude requires owner oracle (which displayed
  bonus value pairs with which rare) — open.
- **`ParagonBoardEquipIndex`.** Evaluating a simple tag's threshold
  needs this binding; the resume-prompt's standing hypothesis is
  `ParagonBoard.payload+32` (128 Warlock / 64 Paladin / 0 older). The
  value pattern doesn't immediately match a small index 0–7; this
  belongs to the next CL.
- **The composite-tag sub-records** (`Barb_Strength+Dexterity` and
  friends). The primary formula text decodes; the per-alternative
  records do not.
- **Formula evaluation.** Library boundary holds (Appendix C). The
  FR-C21 node-info surface, when shipped, will do the substitution
  and reduction — but that is the public projection layer, not the
  raw decode.

## Surfaces

- `ParagonNodeDefinition.BonusPassivePowerSno : int`
  (`-1` = no descriptor, `0` = rare with empty slot, otherwise a SNO id)
- `ParagonNodeDefinition.BonusStatTagSnoIds : IReadOnlyList<int>`
- `StatTagDefinition` (`SnoId`, `ThresholdFormulaText`)
- `Diablo4Storage.ReadStatTag(int)` / `TryReadStatTag(int, out)`
- `SnoGroup.StatTag = 124`

## Tests

Four new synthetic tests (CI-safe) + three live assertions added to
the existing acceptance matrix:

- `B2_node_decodes_bonus_passive_and_stat_tag_arrays` — a synthetic
  rare-shape blob with populated `@48` (size-1 slot, value `0`) and
  `@64` (3 tag SNOs) round-trips through `Parse`.
- `B2_node_without_bonus_descriptors_returns_empty_tags_and_minus_one_power`
  — a non-rare synthetic blob yields `BonusPassivePowerSno == -1` and
  an empty `BonusStatTagSnoIds`.
- `B7_stat_tag_decodes_formula_text` — synthetic group-124 blob,
  text-with-NUL round-trips to `"760 + (455 * ParagonBoardEquipIndex)"`.
- `B7_stat_tag_missing_descriptor_yields_empty_formula` — defensive
  empty case.
- Live matrix: `Warlock_Rare_006` ⇒ `[1068426]` ⇒ the WillpowerMain2
  formula text exactly; `Generic_Rare_001` ⇒ the three class-keyed
  tag ids; `Generic_Magic_Armor` ⇒ `BonusPassivePowerSno == -1` +
  empty `BonusStatTagSnoIds`.

`58/58` tests green on `3.0.2.71886`.

## Loose ends → next CL candidates

- The bonus-stat GBID linkage (`@88` extra entry).
- `ParagonBoard.payload+32` → `ParagonBoardEquipIndex` verification.
- Composite-tag per-alternative sub-records.
- The canonical engine field names for `@48` / `@64` (DJB2 candidates
  `snoBonusPower` / `arBonusStatTags` and family — neither obvious
  candidate is the hit; needs the `d4data FieldChecksums` registry).
