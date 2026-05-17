# 0013 — FR-D1 rescoped (class/index) + FR-D2 (class roster)

*2026-05-17*

Same day as FR-D1 Round-1 (board name, PR #11). The owner rescoped
FR-D1 from "board name" to full ParagonBoard metadata — localized name
+ typed `Class` + `BoardIndex` — to delete the consumer's build-time
`BoardNameRegex`/`NormaliseClass`/`Split('_')` (`ResolvedDatasetBuilder`),
and raised FR-D2 (the deliberately-decoupled character-class roster +
localized names). Both answered **(B)** in one cohesive round.

## Recon

`dump 108 2458674` proved the `ParagonBoard` record (1820 B) is
entirely header + `snoId` + `nWidth` + `arEntries` + 441 cells — **no
class/index field**. So class/index can only come from the SNO name
`Paragon_<ClassToken>_<NN>`. Per the durable opaque-id principle that
parse is library-side, documented, re-verify-triggered — not a consumer
regex.

For the class identity I needed a first-party roster. Group 74
(`PlayerClass`) holds the eight classes + one junk entry (`Axe Bad
Data`). A new recon command `stlfind <substr>` (recon-only) located the
localized class names: table `General` (SNO 4118), label
`PlayerClass<SnoName>Male` (markup-free; the base `PlayerClass<SnoName>`
carries `|5sing:plur` markup). A group-74 entry is a real class iff
that label exists → data-driven junk filter, no hardcoded list (FR-D2,
CL-17, §6.5).

For FR-D1's class: every board token is the **unique case-sensitive
prefix** of exactly one PlayerClass roster SnoName (`Sorc`→`Sorcerer`,
`Spirit`→`Spiritborn`, `Barb`→`Barbarian`, …). So class resolves
data-driven against the FR-D2 roster — not a hardcoded abbreviation
map. Index = the trailing integer (variable width: `Paragon_Spirit_0`
is single-digit). Ambiguity/no-match throws `CascFormatException` (the
re-verify signal, never silent drift). CL-16, §6.6.

## Delivery

- `CharacterClass(SnoId, SnoName, DisplayName)` +
  `Diablo4Storage.ReadCharacterClasses(locale)` (ordered by SnoId,
  cached per locale) — FR-D2.
- `ParagonBoardDefinition.ClassSnoId/.ClassSnoName/.BoardIndex`,
  populated by `ReadParagonBoard(int)` (byte-only `Parse(blob)` keeps
  honest `0`/`""`/`-1` sentinels) — FR-D1. `ClassSnoId` == the FR-D2
  `CharacterClass.SnoId` (one shared stable class key).
- Spec §6.5/§6.6 + Appendix A CL-16/CL-17; the durable opaque-id
  principle mirrored verbatim into Appendix C (outlives the FR).
- Integration tests `ReadParagonBoard_resolves_typed_class_and_index`
  and `ReadCharacterClasses_returns_first_party_roster` assert the
  verbatim probes vs live `3.0.2.71886`; full suite green, 0 warnings;
  API docs regenerated. Reports: `docs/fr-d1-response.md` (rewritten
  whole), `docs/fr-d2-response.md`.

Consumer can now delete its SNO-name regex and its hardcoded
`ParagonClass` enum, consuming typed fields + the data-driven roster.
Amend-until-next-publish contract, like FR-C7.
