# FR-D1 — response: first-party localized ParagonBoard display name

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Re:** `fr-d1` (the recipe-or-gap question).
> **Status: DELIVERED — answer (B): it was an API gap; the minimal
> typed surface is shipped, tested, and spec-recorded.** Authoritative
> byte/string spec: `casc-diablo4-format.md §6.4` (+ Appendix A
> `CL-15`).

## 1. Verdict — (B), with the reason it is not (A)

The mapping is **not reachable without decoding a D4 convention the
shipped surface did not expose**, so per your §1 this is a gap and we
shipped the smallest typed addition. Your read was correct on every
point:

- `ParagonBoardDefinition` (group 108) carries **no** name,
  name-string-id, or GBID — confirmed by the §7.1 layout (only `snoId`,
  `nWidth`, `arEntries`).
- The board name lives in a **separate sibling StringList table**
  (group 42) whose link to the board is a **name convention**
  (`"ParagonBoard_" + boardSnoName`, label `Name`) — undocumented D4
  data, i.e. **our side of the boundary**. Resolving it consumer-side
  would mean guessing StringList keys (the exact violation §3 guards
  against) and is locale-fragile.

Technically the values are *fetchable* through the existing public API
once you know the convention — but encoding that convention is D4 RE,
not consumer policy, so we internalised it rather than documenting a
recipe for you to hard-code.

## 2. What shipped (public API)

```csharp
// Raw decoded localized name; no fallback policy (you own "if unknown,
// show the SnoName identifier"). false / "" when unresolved.
bool Diablo4Storage.TryReadParagonBoardName(
        int boardSnoId, out string name, string locale = "enUS");

// Throwing convenience (family-consistent with the other Read*),
// SnoNotFoundException when unresolved.
string Diablo4Storage.ReadParagonBoardName(
        int boardSnoId, string locale = "enUS");

// Newly-named group (still NOT per-SNO path-addressable; meaningful
// for CoreTOC name<->id resolution).
enum SnoGroup { … StringList = 42 }
```

No new public type. `DefaultLocale` (`"enUS"`) is the existing
constant. Consistent with the library boundary: raw decoded string
only — no policy, no imaging, **no fallback baked in**. Your existing
"unknown → show SnoName identifier" stays yours; pass through
`TryReadParagonBoardName` and keep your placeholder for the `false`
case.

## 3. The decoded convention (for your audit)

```
boardName = CoreToc.GetName(108, boardSnoId)   // "Paragon_Warlock_00"
tableName = "ParagonBoard_" + boardName        // "ParagonBoard_Paragon_Warlock_00"
tableSno  = CoreToc.GetId(42, tableName)        // group-42 StringList SNO
text      = GetStrings(locale).TryGet(tableSno, "Name")
```

- Label is **`Name`**; the sibling table holds exactly one entry on the
  verified build.
- **Name-keyed only — no SNO arithmetic.** Warlock's table happens to
  be `boardSnoId − 1`; Sorcerer's is not (`Paragon_Sorc_00` 939773 →
  `ParagonBoard_Paragon_Sorc_00` 1111181). The library resolves
  strictly through CoreTOC by name; do not assume an offset if you ever
  audit this yourself.
- Holds for **all eight** class stems on build `3.0.2.71886`
  (`Paragon_Barb/_Druid/_Necro/_Paladin/_Rogue/_Sorc/_Spirit/_Warlock`).
- Locale-aware end to end (the StringList catalog is per-locale).

## 4. Acceptance (your §4 probes, verbatim)

| Probe | Result |
|---|---|
| `Paragon_Warlock_00` (SnoId 2458674, `IsStart`) | **`Start`** (enUS) |
| `Paragon_Warlock_03` (SnoId 2458680, non-start) | **`Dynamism`** (enUS) — distinct |
| Locale check, same board, `deDE` | **`Dynamismus`** (no English baked in) |
| Deterministic per (board SNO, locale), all classes | ✓ name-keyed via CoreTOC; verified across class stems |
| Unknown board SNO | `Try…` → `false` / `""`; `Read…` → `SnoNotFoundException` (no fallback policy) |

Recorded with a `CL-15` acceptance row + the verified-anchor table in
`casc-diablo4-format.md §6.4`. Asserted by the
`ReadParagonBoardName_resolves_localized_board_name` SkippableFact
(passes against live build `3.0.2.71886`; full suite green, 0 warnings).

## 5. How to integrate (zero consumer byte/string parsing)

Replace the interim identifier
(`IsStart ? "<Class> Start" : "<Class> <SnoName>"`) with:

```csharp
var name = d4.TryReadParagonBoardName(boardSnoId, out var n)
    ? n                       // the first-party localized in-game name
    : SnoNameIdentifier(...); // your existing placeholder — still yours
```

Pass your UI locale through the `locale` argument. `SourceBoard`
origin / `CombinedBoard` / the L2 ranking label now show the real
localized name verbatim with no further consumer code.

## 6. Boundary & what is owed

Library = the decoded sibling-table convention + the raw localized
string. No fallback policy, no formatting, no imaging (all yours — D4
values still carry markup tokens; strip/format consumer-side as you
already do for other StringList text). **Nothing further is owed**
unless: (a) the owner cuts the next gated NuGet release (then switch
your package reference); or (b) a seasonal D4 build changes
`.build.info` Build Key — then re-verify (Appendix D); the convention
is stable unless Blizzard re-authors the paragon string tables. Same
amend-until-next-publish contract as FR-C7 §7 — reopen the round log
here if you need a shape change before then.

## 7. Round log

- **Round-1 (open, 2026-05-17):** consumer raised the recipe-or-gap
  question (per-board attribution plumbing already shipped against the
  interim identifier).
- **Round-1 (delivered, 2026-05-17):** library session — answer **(B)**.
  Recon vs live `3.0.2.71886` decoded the sibling-StringList-table
  name convention (`ParagonBoard_<boardSnoName>` / label `Name`);
  shipped `Diablo4Storage.TryReadParagonBoardName` /
  `ReadParagonBoardName` + `SnoGroup.StringList`; spec `§6.4` + `CL-15`;
  integration test asserts the verbatim probes. Consumer integrates by
  reading the delivered API — no consumer byte/string parsing.
