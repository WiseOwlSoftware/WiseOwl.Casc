# 0065 — FR-C21 build (3/N): `Catalog.GetBoardNodes` hot path + `EnumerateNodes`

2026-05-23 · CL-70 · branch `fr-c21-board-nodes-hot-path`

## Trigger

CL-68 shipped the math, CL-69 shipped the per-node projection. This CL
ships the **batch surface the Optimizer's multi-board B&B search hits
in its inner loop**:

```csharp
IReadOnlyList<(ParagonGridCell Cell, ParagonNodeInfo Info)>
    Catalog.GetBoardNodes(int boardSno);

IEnumerable<ParagonNodeInfo>
    Catalog.EnumerateNodes(AssetQuery? query = null);
```

The Optimizer flagged `GetBoardNodes` as the **hot path** in their #33
reply (2026-05-22): *"The optimizer's multi-board B&B search and the
UI both work per board, so the (cell, info) batch is what I call
repeatedly. Keep `EnumerateNodes` for a global catalog/index, but
optimize `GetBoardNodes`."* This CL closes the consumer-signed-off
backlog (CL-68 / CL-69 / CL-70).

## What ships

```csharp
public readonly record struct ParagonGridCell(int Row, int Col);

// On Catalog:
public IReadOnlyList<(ParagonGridCell, ParagonNodeInfo)>
    GetBoardNodes(int boardSno);     // cached + memoized per-board-SNO

public IEnumerable<ParagonNodeInfo>
    EnumerateNodes(AssetQuery? q = null);  // lazy, shares SNO cache
```

## Caching architecture (three layers converge here)

Each call to `GetBoardNodes` touches three caches that converge on
the same Catalog instance:

1. **Board defs** — `ConcurrentDictionary<int, ParagonBoardDefinition?>`.
   The 21×21 grid blob decoded once per SNO; subsequent calls hit
   the cached array.
2. **Node infos** — the §7.7 `ConcurrentDictionary<int,
   ParagonNodeInfo?>`. ~17–21 distinct defs per board across ~441
   cells; the Optimizer's expectation.
3. **Projected lists** —
   `ConcurrentDictionary<int, IReadOnlyList<(ParagonGridCell,
   ParagonNodeInfo)>?>`. The (cell, info) projection cached **per
   board SNO** so the optimizer's "re-query the same board" pattern
   in its B&B search tree returns the same list reference at O(1)
   cost.

The third cache is the new bit for CL-70 — the per-node cache
already existed (CL-69), but rebuilding the projection list every
call would still walk 441 cells. With it, a repeat query is one
dictionary lookup.

Missing / undecodable board SNOs memoize as an empty list. The
search-tree pruning often probes malformed ids; the cache makes
re-probes free.

## Ordering

Row-major from top-left: `row 0` = top, `col 0` = left, empty cells
(unauthored slots, sentinel `0xFFFFFFFF`) skipped. The list length
equals the board's `NodeCount` minus any cells whose nodes failed to
resolve to a `ParagonNodeInfo` (rare — would indicate a malformed
node placed on a real board, which the live matrix doesn't see).

## `EnumerateNodes`

Streams every paragon node in the install through the §7.7
projection, sharing the SNO-keyed decode cache. The query is
forced into `Kind = ParagonNode` (with `with` on the record copy),
so `EnumerateNodes(new AssetQuery { NameContains = "Armor" })`
yields only paragon-node hits whose name matches. Malformed nodes
are silently skipped (CoreTOC name missing, or
`ParagonNodeDefinition.Parse` throws).

The Optimizer asked for both surfaces with `GetBoardNodes` as the
hot path; `EnumerateNodes` is for catalog scans (global indexing,
debugging, bulk-export).

## Tests

The CL-70 assertions extend the existing live
`Acceptance_matrix_against_live_install`:

- `GetBoardNodes(2458674)` (Paragon_Warlock_00) returns `>60 and
  <441` placements.
- Every pair carries in-range coordinates (`Row`/`Col` in
  `[0, Width)`) and a non-null `info.Sno`.
- Cache identity — repeat `GetBoardNodes(2458674)` returns the
  same list reference.
- Distinct definition count lands in the Optimizer's expected band
  (`10..30`; their estimate was ~17–21).
- Missing board ⇒ empty list (memoized).
- `EnumerateNodes().Take(5)` streams 5 valid infos.
- `EnumerateNodes(new AssetQuery { NameContains = "Generic_Magic_Armor" })`
  yields non-empty, each name matches the substring.

92/92 tests green on `3.0.2.71886` (the new assertions extend the
acceptance matrix; the test count is unchanged).

## FR-C21 backlog status

The consensus backlog the Optimizer signed off on 2026-05-23 is now
**complete**:

- CL-68 — `ParagonPowerBudget` + `ParagonMagnitudeFormula` (the math) ✓
- CL-69 — `ParagonNodeInfo` projection + `Catalog.GetNodeInfo` + cache ✓
- CL-70 — `Catalog.GetBoardNodes` hot path + `EnumerateNodes` ✓

`casc-fr#33` is now ready for consumer verification — the
Optimizer can consume the full surface end-to-end:
`GetBoardNodes(boardSno)` for the search-tree inner loop;
`GetNodeInfo(sno)` for tooltip-style lookups;
`EnumerateNodes(q)` for catalog scans.

## Open follow-ups (only if requested)

- Localized labels via `AttributeDescriptions` (sno `4080`) for
  `StatName` — currently humanizes the engine token. Defer until
  the Optimizer asks (the token is readable).
- Glyph rank/radius magnitude scaling — explicitly deferred by the
  Optimizer to a follow-on FR; not in scope here.
- `ParagonNodeStat.VariantName` resolution — reserved for the
  edge case where a future stat surfaces a non-zero `NParam` with
  meaningful semantics. None observed.
