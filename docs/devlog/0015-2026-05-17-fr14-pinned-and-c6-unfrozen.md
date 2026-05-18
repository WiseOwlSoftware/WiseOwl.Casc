# 0015 — FR-14 acceptance pinned + C6 scope-freeze lifted

*2026-05-17*

Owner: "Complete all backlog items" → on the scope question, chose
**unfreeze + do C6 too**. Two deliverables.

## FR-14 — concrete Child SNO pinned (CL-19)

The id-keyed resolver was always folder-generic; only a concrete
Child-bearing SNO was unpinned (the test self-skipped). Added a recon
command `SnoScan childpaths` over `CascStorage.DiagnosticPaths` — there
are **≈547,244** `base/child/<id>-<n>` paths. Pinned anchor: SNO
`1015186` (group 71, `AmbS_EMT_Dungeon_AncientsSand`) →
`Base\Child\1015186-0` non-empty; a bad sub-id is a clean miss.
`Resolves_child_folder_by_id` now asserts this and **no longer
self-skips** (28 D4 tests, 0 skipped).

## C6 — typed non-paragon readers (CL-20..22)

The B1–B6 scope-freeze was an owner decision; the owner lifted it.
Done honoring the boundary — **identity + verifiable raw/localized
fields only**; the multi-KB Power/Item engine records are deliberately
*not* modeled (no fabricated values; that stays the consumer's
stat-effect domain).

Recon revealed the high-value, fully-verifiable field is the **sibling
StringList convention generalized** (FR-D1 §6.4 → §6.7): a record's
localized text is the group-42 SNO `"<TypePrefix>_" + snoName`,
name-keyed via CoreTOC — `Item_`(Name/Flavor/TransmogName),
`Affix_`(Desc), `Power_`(name/desc), `ParagonBoard_`(Name). One
internal resolver now backs `TryReadParagonBoardName` + the C6 readers
(DRY, CL-20). The Power inline `szName@+8` was found unreliable (absent
for many `CAMP_*` powers) so the sibling table is the name source —
not that offset (no over-claim).

Shipped: `PlayerClassDefinition` (SnoId + binary `eClass`@+16 — the
field FR-D3's glyph rank uses, now typed, CL-21) + `ReadPlayerClass`;
`PowerDefinition`/`AffixDefinition`/`ItemDefinition` (identity +
sibling-localized text, CL-22) + `ReadPower`/`ReadAffix`/`ReadItem`
(locale-aware). Byte-only `Parse(blob)` = identity only (honest empty).
Spec §6.7 + §11; Appendix C boundary updated (freeze lifted; modeling
still consumer's); Appendix D anchors. Acceptance
`C6_typed_readers_decode_identity_and_localized_text` vs live
`3.0.2.71886`: PlayerClass Warlock→eClass 10; Power 2521393→
`Fathomless`; Affix 2586362→"Your attacks Critically Strike …"; Item
223287→"The Butcher's Cleaver"/"Cadaver Chopper". Full suite green, 0
warnings; API docs regenerated. `SnoScan childpaths` recon-only.

Backlog: FR-11..16 + B1–B6 + C6 all DONE; nothing deferred. The
library still ships **no formula evaluator** and models no deep
gameplay record — the boundary held even with the freeze lifted.
