# FR-D1 — response: first-party ParagonBoard metadata (name + class + index)

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Re:** `fr-d1-paragon-board-name.md` (rescoped: localized name +
> typed class + board index).
> **Status: DELIVERED — answer (B): API gap; the minimal typed surface
> is shipped, tested, and spec-recorded.** Authoritative byte/string
> spec: `casc-diablo4-format.md` §6.4 (name), §6.6 (class/index), §6.5
> (the class roster it resolves against); Appendix A `CL-15`/`CL-16`;
> durable opaque-id principle mirrored to Appendix C.

## 1. Verdict — (B) for all three

Your read was correct on every point. `ParagonBoardDefinition` (group
108) carries **no** name, name-id, GBID, **or class/index field** — the
1820-byte record is fully accounted for by §7.1 (header + `snoId` +
`nWidth` + `arEntries` + 441 cells, nothing else). The three pieces
resolve as follows, all library-side:

| Metadata | First-party source | Boundary |
|---|---|---|
| Localized name | sibling StringList table `ParagonBoard_<boardSnoName>`, label `Name` | StringList convention — RE (CL-15) |
| Class | the **SNO-name convention** `Paragon_<ClassToken>_<NN>` → unique-prefix match to the §6.5 PlayerClass roster | naming convention = a data mapping, decoded once library-side (CL-16) |
| Board index | the trailing integer of the SNO name | same (CL-16) |

Per your §1(B) clause: the only first-party source of class/index is
the SNO-name convention, so the parse is **ours** — decoded once,
documented with `CL-16` + an Appendix D re-verify trigger, exposed
typed. **Not** a consumer regex. The durable opaque-id principle (your
§3) is mirrored verbatim into `casc-diablo4-format.md` Appendix C so it
outlives this FR.

## 2. What shipped (public API)

```csharp
// Localized name (Round-1, unchanged):
bool   Diablo4Storage.TryReadParagonBoardName(int boardSnoId, out string name, string locale="enUS");
string Diablo4Storage.ReadParagonBoardName(int boardSnoId, string locale="enUS");

// Class + index — now typed on the definition, resolved by ReadParagonBoard:
ParagonBoardDefinition {
    int    ClassSnoId;    // the PlayerClass (group 74) SNO id — stable per-class key
    string ClassSnoName;  // e.g. "Warlock" — stable key, == CharacterClass.SnoName
    int    BoardIndex;    // per-class ordinal (Paragon_Warlock_03 → 3; Paragon_Spirit_0 → 0)
    // … existing SnoId / Width / Cells …
}
ParagonBoardDefinition Diablo4Storage.ReadParagonBoard(int id);  // populates the above
```

`ClassSnoId` is the **same stable key** as FR-D2's
`CharacterClass.SnoId` — the two FRs share one class identity. The
byte-only `ParagonBoardDefinition.Parse(blob)` leaves
`0`/`""`/`-1` (identity derives from the *name*, not the bytes —
honest, documented sentinels; the consumer always uses
`ReadParagonBoard(id)`). No fallback policy is baked into the name
resolver (consumer still owns "if name unknown, show a stable id"); an
unknown/ambiguous class token **throws** `CascFormatException` — the
re-verify signal, never a silent wrong answer.

## 3. The decoded conventions (for your audit)

**Name** (CL-15, §6.4): `tableName = "ParagonBoard_" + boardSnoName`,
group 42, label `Name`; strictly CoreTOC-name-keyed (no SNO offset).

**Class + index** (CL-16, §6.6): from `Paragon_<ClassToken>_<Index>`:

- `ClassToken` = substring between `Paragon_` and the **final** `_`.
- `BoardIndex` = trailing integer (variable width — parse as int, not
  fixed `NN`; `Paragon_Spirit_0` → 0).
- `Class` = the **unique case-sensitive prefix** of exactly one §6.5
  PlayerClass roster `SnoName` — data-driven against D4's own roster,
  **not** a hardcoded abbreviation map: `Barb`→`Barbarian`,
  `Necro`→`Necromancer`, `Sorc`→`Sorcerer`, `Spirit`→`Spiritborn`,
  `Druid`/`Rogue`/`Paladin`/`Warlock` exact. No match or ambiguity
  throws.

## 4. Acceptance (your §4 probes, vs live `3.0.2.71886`)

| Probe | Result |
|---|---|
| `Paragon_Warlock_00` (2458674, IsStart) | name **`Start`** (enUS); class → PlayerClass `Warlock` (SNO 2207749); **index 0** |
| `Paragon_Warlock_03` (2458680) | name **`Dynamism`** (enUS) / **`Dynamismus`** (deDE); class → `Warlock`; **index 3** |
| `Paragon_Sorc_04`, `Paragon_Spirit_0` | class → `Sorcerer` / `Spiritborn` (abbrev. token, unique-prefix); index 4 / 0 — **no consumer SnoName parsing** |
| deterministic per board SNO, all classes | ✓ data-driven vs the §6.5 roster (all 8 class stems) |
| unknown board name | name `false`/throws; class/index left `0`/`""`/`-1` |

Recorded with `CL-15`/`CL-16` rows + verified-anchor tables in §6.4/§6.6.
Asserted by `ReadParagonBoardName_resolves_localized_board_name` and
`ReadParagonBoard_resolves_typed_class_and_index` (pass vs live build;
full suite green, 0 warnings).

## 5. How to integrate (delete the consumer regex)

Retire `ResolvedDatasetBuilder.BoardNameRegex` / `NormaliseClass` /
`Split('_')` entirely:

```csharp
var b = d4.ReadParagonBoard(boardSnoId);
// class identity:  b.ClassSnoId  (stable key, == CharacterClass.SnoId)
//                   b.ClassSnoName ("Warlock")
// ordinal:          b.BoardIndex
// display name:     d4.TryReadParagonBoardName(boardSnoId, out var n) ? n : <your stable-id fallback>
```

No SNO-name substructure parsing remains consumer-side. `ClassSnoId`
joins directly to FR-D2's `ReadCharacterClasses()` roster for the
localized class name (the decoupled class-roster concern).

## 6. Boundary & what is owed

Library = the decoded conventions + raw values. No fallback policy, no
formatting, no imaging (D4 name strings may carry markup — strip/format
consumer-side as you do for other StringList text). Class roster/names
themselves are **FR-D2** (deliberately decoupled; `ClassSnoId` is just
the shared stable reference). Nothing further owed unless the gated
NuGet release ships, or a seasonal build changes `.build.info` Build
Key — then re-verify (Appendix D); the throw-on-ambiguity guard turns
any future naming drift into a loud failure, not a silent one. Same
amend-until-next-publish contract as FR-C7 §7 — reopen the round log
here for a shape change before then.

## 7. Round log

- **Round-1 (open → delivered, 2026-05-17):** localized board name —
  answer (B), sibling StringList table convention (CL-15).
- **Round-1 amended (owner, 2026-05-17):** scope extended to typed
  class + index; durable opaque-id principle recorded.
- **Round-1 amended → delivered (library, 2026-05-17):** confirmed the
  board record has no class/index field; decoded the
  `Paragon_<Class>_<NN>` convention library-side (unique-prefix vs the
  FR-D2 PlayerClass roster); shipped `ParagonBoardDefinition.ClassSnoId
  /.ClassSnoName/.BoardIndex` via `ReadParagonBoard`; spec §6.6 +
  CL-16; principle mirrored to Appendix C; integration test asserts the
  verbatim probes. Consumer deletes `BoardNameRegex`/`NormaliseClass`/
  `Split('_')` and consumes typed fields.
