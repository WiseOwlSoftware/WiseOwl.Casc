# 0104 — item SnoName + the season-prefixed unique-duplicate convention (#56)

**Date:** 2026-07-17
**Work item:** casc-fr#56 (FR-C39, Optimizer decode-share + soft ask)
**CL:** CL-109 · `ItemDefinition.SnoName`

The Optimizer flagged (from a consumer KB bug, live-verified) that the Item group
carries **leftover season-prefixed duplicate** unique SNOs: ~473 of ~1030 live
`_Unique_` item SNOs are `^S\d+_`-prefixed (`S05_`/`S10_`/`S12_`/`S05_BSK_`…). They
share a canonical unique's **localized display name** but differ in SNO, often slot,
and explicit count — so any surface that de-dupes uniques by display name picks the
wrong record. Canonical example: `Amulet_Unique_Rogue_100` (1306259, Amulet) vs
`S10_Amulet_Unique_Rogue_100_Boots` (2416563, Boots, more explicits).

## Response

The reliable signal is the CoreTOC name prefix `^S\d+_` (the Optimizer's clean
91-item fix). But `ItemDefinition` didn't expose the CoreTOC name — only `SnoId` and
the localized `Name`. So consumers had to reach for `CoreToc.GetName` separately, and
enumeration (`EnumerateItems`) yielded byte-only records with no name at all.

Fix: `ItemDefinition.SnoName`, populated by both `ReadItem` and `EnumerateItems`
(the latter is where the dedup happens). Test locks the Word of Hakan pair — same
display `Name`, `^S\d+_` cleanly separates the leftover.

## Why the duplicates exist (owner hypothesis)

Likely **seasonal→eternal migration**: when a seasonal-realm character moves to
Eternal, its items need SNOs that don't collide with the canonical eternal versions,
so the season incarnation is retained under an `S<n>_`-prefixed name. That fits the
data — same display name, season prefix, from a specific season's variant.

## The flag isn't clean (confirmed)

Checked the pair's flags word (payload `+0x14`): canonical `0x3C84`, leftover
`0x10003C8E` — the leftover has `0x10000000`. But per the Optimizer's wider check
(91% season-prefixed vs 7% canonical, 65 exceptions), `0x10000000` is a
seasonal-content bit also set on some current-season canonical uniques, not a clean
canonical marker. No single bit separates them, so the library exposes the raw
`SnoName` signal + documents the convention rather than baking a heuristic flag.
