# Feature backlog — consumer-driven

Tracks feature requests from the first consumer (the Diablo IV
ParagonOptimizer project) and their disposition. Round 1 (FR-1…FR-10) is
**done & proven** (see `devlog/0002`, `CHANGELOG`). This file tracks round 2
(FR-11…FR-16), all flagged *future / non-blocking* by the consumer:
"promote when the d4-character-model RE workstream actually starts; refine
each with concrete SNO ids/acceptance bytes at that point."

Adopting them honestly means: implement what is **fully specified and
verifiable now**; formally accept-and-defer what needs its own
reverse-engineering workstream (no spec yet) rather than ship unproven RE.

| FR | Disposition | Notes |
|---|---|---|
| **FR-11** Read any group by id + int escape hatch | **DONE** | `SnoGroup` now names `Power=29, Item=73, PlayerClass=74, ItemType=98, Affix=104` (+ existing `GameBalance=20`). `Diablo4Storage.ReadSno(int groupId,int id,SnoFolder)` / `TryReadSno(int…)` overloads (the TVFS address is id-only; group is informational). Raw bytes + `SnoRecord` only — no typed readers (FR-5/FR-16). |
| **FR-12** GameBalance enumeration + GBID hash | **DONE** | `CoreToc.EntriesInGroup(SnoGroup.GameBalance)` already enumerates *all* GameBalance SNOs (generic by group, not just 201912). Added `static uint Diablo4.GbidHash(string)` — case-insensitive DJB2; verified `GbidHash("ParagonNodeCoreStat_Normal") == 0x42C16A1B` (upstream §7.1). Table parsing stays consumer-side. |
| **FR-13** StringList / localized names | **ACCEPTED — DEFERRED (own RE workstream)** | Genuinely library-level (the `StringList-Text-<locale>-*.dat` container is a generic D4 concern, reusable across games) — but its byte format is **not** in any spec we hold and not yet reverse-engineered. Implementing it now would be speculative RE with no verifiable acceptance ("a known key" is undefined until the workstream). Promote when the character-model RE starts; then write the container spec into `docs/casc-format.md` first, implement `d4.TryGetString(...)` (enUS default + locale option), and prove against the live install. **Not shipped unproven.** |
| **FR-14** `SnoFolder.Child` (and PayMed/PayLow) by id | **MECHANISM DONE; acceptance gated** | The FR-1 resolver is folder-generic — `SnoPath(id, SnoFolder.Child[, subId])` → `Base\Child\<id>[-<subId>]` (the `<id>-<subId>` form, per the ~16k `base:child\<id>-<n>` census); `PayLow`/`PayMed` likewise. It is the **identical proven code path** as Meta/Payload. No new code needed. A *concrete* Child-bearing SNO id was not pinned in sampled ranges — that requires the deferred RE (which SNOs carry children); the FR-14 acceptance test self-skips honestly until then rather than fake a pass. |
| **FR-15** Bulk group streaming | **DONE** | `Diablo4Storage.ReadGroup(SnoGroup, SnoFolder)` → `IEnumerable<(int Id, byte[] Bytes)>`, skips legitimately-absent ids, reuses the resident local index / encoding table / cached archive handles (FR-9) so a full-group sweep does not re-open storage. |
| **FR-16** Boundary doc (no code) | **DONE** | Recorded here and in `docs/casc-format.md` (the FR-5 boundary section): Item / Affix / Power / Class / GameBalance → stat-effect *modeling* is a **ParagonOptimizer domain spec** built on FR-11…FR-15. The library provides transport + CoreTOC + combined-meta + `SnoRecord` primitives + BCn + GBID hash + StringList (when FR-13 lands) and **will not grow typed game-record APIs**. The future split is unambiguous. |

## Verifiable-now vs. deferred — rationale

Round-2 acceptance criteria are mostly written as "ids from future RE" /
"≥N" — i.e. not yet pinned. FR-11/12/14/15/16 are nonetheless fully
specified at the *mechanism* level and verifiable today (id-keyed read is
group-agnostic; the GBID hash has an exact known test vector; group
enumeration/streaming use existing primitives). FR-13 is the one that needs
its own reverse-engineering before any honest implementation — accepted,
documented, and explicitly gated on that workstream. This matches the
consumer's own "promote when the workstream starts" guidance and the
project's no-faking integrity rule.

## When the d4-character-model workstream starts

1. Pin concrete acceptance: real Power/Affix/Item SNO ids + expected
   `SnoRecord` header bytes (FR-11/14), the `N` for GameBalance
   enumeration (FR-12), a known StringList key→enUS string (FR-13).
2. FR-13: reverse the `StringList-Text-<locale>-*.dat` container, write it
   into `docs/casc-format.md` (self-contained, correction log), then
   implement + prove `d4.TryGetString`.
3. Re-confirm FR-11/12/14/15 against the then-current build; refine tests
   with the pinned ids.
