# Resume prompt — WiseOwl.Casc

> Full-context handoff. After a compaction or a new session, read this
> end-to-end before continuing. Companion docs: the two canonical
> byte-format specs `docs/casc-format.md` (CASC/TACT/TVFS/BLTE transport)
> and `docs/casc-diablo4-format.md` (Diablo IV SNO/container/record
> layer) — each with its own correction log; `docs/devlog/` (the
> narrative), `docs/ARTICLE-SOURCE.md` (wiseowl.com article source).

## CURRENT STATE (2026-07-16) — read this first

The sections below this one ("Status (end of session 1)", the numbered
"Next steps") are **historical** — accurate for their era but superseded
by what follows. This block is the live state.

### How work arrives now: the CASC⇄Optimizer FR loop (GitHub Issues)

Feature requests + bugs no longer come from local backlog files — they
are **GitHub Issues at the private `WiseOwlSoftware/casc-fr` repo**, and
you act as the bot **`wiseowl-casc-bot`**. The full protocol + the
token-bootstrap (`export GH_TOKEN="$CASC_BOT_TOKEN"; unset GITHUB_TOKEN`,
`MSYS_NO_PATHCONV=1` for `gh api`) is in **`CLAUDE.md`** (git-ignored —
public repo; never commit it). Read CLAUDE.md before any FR action.

- **Roles:** you are the library/producer (CASC); the consumer is the
  ParagonOptimizer (`wiseowl-optimizer-bot`); Brent is the owner.
- **Turn labels:** `awaiting:casc` (your turn), `awaiting:optimizer`,
  `needs:owner`. Lifecycle: `fr:proposed`→`accepted`→`delivered`
  (`CL-NN`+`WiseOwl.Casc@SHA`+release-or-"unreleased")→`consumed`.
- **CASC never closes an issue** (only the Optimizer, on owner-validated
  `fr:consumed`). Comments are role-tagged `**[CASC]**`.
- **Operating mode:** self-paced `/loop` — poll `awaiting:casc`, do the
  work, schedule a fallback wake. **`needs:owner` is the only hard stop.**
  After >1 idle poll with nothing queued, do available work (cumulative
  hash-decode, deferred RE) — owner-approved standing directive.
- **Merge on CI green** (owner directive 2026-05-23): after a PR's CI
  checks pass, `gh pr merge <n> --squash --delete-branch`; no need to wait
  for owner. **GitHub auto-merge is NOT enabled on this repo** (`--auto`
  → "Auto merge is not allowed"), so the pattern is: `gh pr checks <n>
  --watch --interval 20` (do **not** add `--fail-fast` — it can exit 0
  before checks register, racing ahead), then merge manually. Branch
  protection requires the `Build & test` + `API docs in sync` checks
  (integration tests `[SkippableFact]`-skip in CI, so they never gate).
  Doc-only changes push straight to `main` (no PR).

### ✅ 0.4.0 — PUBLISHED to NuGet (2026-07-16)

**First stable release** (dropped `-alpha`) — **live on NuGet.org**: both
`WiseOwl.Casc` and `WiseOwl.Casc.Diablo4` `0.4.0` indexed + installable.
Cut at `d6d7188`; owner published the GitHub Release `v0.4.0` (tag
created), approved the `nuget`-environment reviewer gate, OIDC push
succeeded. CHANGELOG `[0.4.0]` is the **humanized/succinct** style the
owner wants ([[feedback_changelog-features-only]] — release notes, not an
API diff, no CL/FR/SNO jargon). `PackageReleaseNotes` de-absolutized
(was pinned to a stale `0.2.0-alpha`). Post-release: CA1861 test warning
fixed (`#80`, `6313423`). **Release mechanics** (`publish.yml`): GitHub
Release published → `nuget` env reviewer gate → OIDC push; the tag must
equal `Directory.Build.props <Version>`. `main` tip: **`6313423`**;
`<Version>` is still `0.4.0` (bump before the next release).

### Active threads (2026-07-15, FR-C27+C30 delivered — 1 awaiting:casc)

**1 awaiting:casc** (`#41` FR-C29 — the big open RE):

| # | FR | What's needed |
|---|---|---|
| 41 | FR-C29 — per-class character-stat derivation formulae | `fr:accepted`. Big multi-phase (per-class core→bonus coefficients, base/level scaling, composites, Torment multipliers). Owner relayed 2026-05-24: **search all non-identified data sources** — leads `LevelScaling` (206158), `SimpleScalarFormulas` (2536879), `DamageMitigation` (1846727). Owner-suggested **name-hash-grep** short-circuit (DJB2 the in-game stat names → grep unidentified SNOs). **NOTE: `DataAttributes` (1907204) is ruled out** — CL-88 proved it's the designer/season-attribute subset, not a scalar registry. If coefficients prove engine-coded, honest boundary → consumer hard-codes. Phase 1 first; Phase 2 (`LevelScaling`) / Phase 4 (`MonsterLevelCurves`) are incremental slices. |

FR-C30 (`#42`, CL-87) + FR-C27 (`#39`, CL-88) delivered this session,
both now `released:v0.4.0`. `#42` advanced to `needs:owner` (owner
validation); `#39` `awaiting:optimizer`. FR-C24 (`#36`)/FR-C28 (`#40`)
closed rounds, `awaiting:optimizer`.

**Pending release-label bulk:** every CL-42..CL-86 FR (C21..C28) also
shipped in 0.4.0 but is NOT yet tagged `released:v0.4.0` — per protocol,
bulk-labeling waits for the Optimizer's pinned tracking issue to be
`awaiting:casc` / an explicit owner relay (don't bulk ahead of it). Only
this session's two deliveries were labeled (I'd promised the label in
their delivery comments).

### Recent CL trajectory (2026-05-22 → 2026-07-15)

CL-42..CL-88 all on `main`; **shipping in the 0.4.0 release** (drafted).
Auto-merge after CI green is the pattern; PRs open ~minutes.

| CL | SHA | PR | FR / slice |
|---|---|---|---|
| 66 | `0945892` | #54 | FR-C21 foundation — `ParagonNode eNodeType@16` + per-attr GBID @88 |
| 67 | `e6e226e` | #56 | FR-C21 — rare bonus mechanic `@48`/`@64` + `StatTagDefinition` (group 124) |
| 68 | `a7a22aa` | #57 | FR-C21 — `ParagonPowerBudget` + `ParagonMagnitudeFormula` |
| 69 | `342205c` | #58 | FR-C21 — `ParagonNodeInfo` projection + `Catalog.GetNodeInfo` + decode cache |
| 70 | `8c463d8` | #59 | FR-C21 — `Catalog.GetBoardNodes` hot path + `EnumerateNodes` |
| 74 | `0aa6f39` | #63 | FR-C21 oracle fix — Gate nodes DO carry stats (CL-69 over-drop reversed) |
| 75 | `d068086` | #64 | FR-C22 — `ParagonNodeInfo.LocalizedTitle` via sibling `ParagonNode_<SnoName>` |
| 76 | `0c9b46e` | #65 | FR-C21 — `StatName` prefers AttributeId map over node-token (multi-stat-node fix) |
| 77 | `b50f170` | #66 | FR-C23 Option A — `Catalog.GetParagonTooltipChrome().PanelByRarity` |
| 78 | `164499e` | #67 | FR-C25 — `Diablo4Storage.GetAttributeName` + `AttributeNames` curated map |
| 79 | `76fd286` | #68 | FR-C24 slice 1 — sibling-StringList projection (glyph title + affix description) |
| 80 | `847abd4` | #69 | FR-C26 — multi-layer tooltip chrome composite (BaseLayer / OrnateFrame / variants / banners) |
| 81 | `1d6e94b` | #70 | FR-C26 — `SkillIconAtlas` (`2DUI_Tooltip_Icons`, 61 frames) on chrome record |
| 82 | `8e44df2` | #71 | FR-C26 — typed `Divider` field (Optimizer-picked `Center_Divider_White` 1559055) |
| 83 | `84a5e2f` | #72 | FR-C24 slice 2a — glyph engine constants (`BaseRadius=3` / `RadiusUpgradeLevels=[25,50]` / `MaxLevel=150`) |
| 84 | `d376b12` | #73 | FR-C24 slice 2b — `ParagonGlyphAffixDefinition` structural decode (`OperationKind`/`DisplayFactor`/`AffectedAttributes`/`Tags`/`LinkedPowerSnoId`/`AffectedRarityKind`); op-coupled byte layout. Closes FR-C24. |
| 85 | `b226adb` | #74 | FR-C28 — tag-conditional `(AttributeId, ParamPlus12)` resolution; `AttributeNames.LabelByCompoundKey` (100+ entries / 17 attrs) + `GetAttributeName(int, uint, locale)`; 19 `Skill_<Tag>` GBIDs cracked. Closes FR-C28. |
| 86 | `95e1150` | #75 | FR-C24 Headhunter counter-round — `ParagonGlyph_<SnoName>` sibling-StringList (non-`Item_`-prefixed) for `LocalizedTitle`; `Rare_<Stat>_Generic` shape now resolves (`Rare_Will_Generic` → "Headhunter"). |
| 87 | `35649fe` | #76 | FR-C30 — `AffixDefinition.Name` + `Diablo4Storage.TryReadAffixName` (sibling `Affix_<sno>` StringList, label `Name`); mirror of `Desc`/`TryReadParagonBoardName`. Sole first-party source for aspect display names. 1,464/6,145 affixes named; honest empty otherwise. Pure sibling-label surface, no byte-layout change. |
| — | `9f8784f` | #77 | Season-14 re-baseline + **isolate content-snapshots** — exact game-authored values (registry counts, Power SF_N, affix AttributeIds) drift per build; acceptance tests now assert decode *structure/invariants*, exact values quarantined under `[Trait("kind","content-snapshot")]` (`--filter kind=content-snapshot`). All byte-verified as content drift, not regressions. |
| 88 | `acc7d95` | #78 | FR-C27 — **season-robust `GetAttributeName`**. `DataAttributes` is NOT the registry (designer/season subset); `AttributeId` is a per-build ordinal (Armor 481→482, high-health 1120→1123, Barrier 1124→1127). Now scans live `Generic_` nodes for `id→token` (cached) → `AttributeNames.LabelByToken` (season-stable) → sno-4080 localize; auto-tracks id shifts. Compound map re-keyed on `(baseLabel, ParamPlus12)`. Legacy id-map = fallback. Fixed `BlockChance` label bug. |

**Test count:** **135/135 green** on the live `3.1.1.72836` (Season 14) install (was 127 pre-refactor; +the new FR-C27 coverage test + content-snapshot rows). Season-14 drift was resolved in CL-#77 (re-baseline) — the earlier "4 build-drift failures" note is now closed. Exact-value anchors live under the `content-snapshot` trait; a future season bump surfaces there as one filterable cluster.

### FR-C21 — full node-info API (DELIVERED 2026-05-23)

Public surface ([[project_fr-c21-node-info]]):
- `Catalog.GetNodeInfo(int sno)` → `ParagonNodeInfo` (cached + memoized).
- `Catalog.GetBoardNodes(int boardSno)` → `IReadOnlyList<(ParagonGridCell, ParagonNodeInfo)>`
  — the consumer hot path; reference-equal repeat lookups (3-layer cache).
- `Catalog.EnumerateNodes(AssetQuery?)` → lazy global enumerator.
- `ParagonNodeInfo` (`Sno`, `Name`, `LocalizedTitle`, `Kind`, `Rarity`,
  `Icon`, `IconMask`, `PassivePower`, `PassivePowerName`, `Stats[]`,
  `HasSocket`, `IsGate`).
- `ParagonNodeStat` (`AttributeId`, `StatName`, `Variant`, `VariantName`,
  `FlatValue`, `Unit`, `Formula`, `InlineFormula`).
- `ParagonPowerBudget` + `ParagonMagnitudeFormula` (the math; engine-constant
  budget multipliers per [[project_engine-constants-pattern]]).
- `ParagonNodeDefinition.BonusPassivePowerSno` / `.BonusStatTagSnoIds` +
  `Diablo4Storage.ReadStatTag` → `StatTagDefinition` (rare bonus mechanic).
- `Diablo4Storage.GetAttributeName(int, locale)` via the
  `AttributeNames.LabelByAttributeId` curated map (~40 ids) + sno-4080
  `AttributeDescriptions` template strip.

Status: `awaiting:optimizer` for `fr:consumed` on `casc-fr#33`. The
two CL-74 + CL-76 mid-flight oracle corrections + CL-78 localization
all landed cleanly. Open future-RE-threads (filed as fresh FRs):
- Bonus stat magnitude (`+Z% [stat]` on rares) — strongest candidate
  is the `@88` GBID array's `+1` entry; linkage not yet verified.
- AttributeId 259 tag-conditional names → **FR-C28** (`casc-fr#40`).
- `DataAttributes` (sno 1907204) full registry RE → **FR-C27** (`casc-fr#39`).

### Recon tooling on `build/SnoScan` (all committed on `main`)

`nodeinfo` (full per-node dump + formula text), `nodesbyformula`,
`formula`, `formulafind`, `boardname` (dogfoods
`TryReadParagonBoardName`), `cellof`, `rawhex`, `listgroup`
(+max-results param, `c4e9d56`), `snoid`, `attrmap`
(`AttributeId → stat-name`), `findhandle` (scans all 140k textures),
`checkfields` (hash-dictionary sanity), `texdump` / `frametail` /
`framescan` / `framediv` / `listframes` (FR-C26 tooltip-chrome RE).
`build/AtlasExport` has `frame <handle> <out.png>` for visual
extraction.

### Engine-bound residuals (read once, don't re-discover)

- **`AttributeId` is a power-budget category, NOT the stat key.**
  `Generic_Magic_Armor` / `_ArmorPercent` / `_DamageReductionFromElite`
  all share `AttributeId 481`. Stat identity = node SNO/name. Canonical
  agg key for FR-C21 = node SNO. CL-69 / CL-76 / CL-78 wire this in.
- **`ParamPlus12` is the skill-tag GBID for tag-conditional attrs**
  (attr 259 = DamageBonusTag): `+12` holds the same GBID as the
  `@88` array entry (Demonology = `0x32ABA6FB`, label uncracked).
  FR-C28 (`#40`) is the typed-resolution slot.
- **Bonus stat magnitude** (`+Z% [stat]` on rares) — strongest
  candidate: rare's `@88` GBID array has `ptAttributes.Count + 1`
  entries (Warlock_Rare_006 → `0xAC62A180`, Generic_Rare_001 →
  `0x6D91307D`). Linkage not verified; owner oracle pairing needed.
- **`ParagonBoardEquipIndex` is runtime**, not in the board record.
  `ParagonBoard.payload+32` (128 Warlock / 64 Paladin / 0 older) is
  almost certainly a class-version flag — values don't match
  indices 0..7. Consumer supplies the index at evaluation time.
- **Composite tag-record sub-records** on group-124
  (`Barb_Strength+Dexterity` etc.) — primary formula text decodes;
  per-alternative records do not. Open follow-up if Optimizer
  needs them.
- **Engine controller code is encrypted** ([[project_engine-controller-code-encrypted]])
  — Phase-C-style EXE RE for runtime-bound bindings (hover tooltip
  layout, bullet glyph, icon bezel, AttributeId registry semantic
  mapping) is permanently impossible. Procedural fallback is the
  accepted answer per FR-C7 §6 + FR-C26.
- **Engine constants pattern** ([[project_engine-constants-pattern]])
  — when a field is universal across every record + not in the
  byte layout, bake as an instance property with Appendix D
  re-verify trigger. CL-68 budget multipliers + CL-83 glyph
  radius/maxlevel are the precedents.

### Active branch / PR / release state

- **No open PRs.** Auto-merge after CI green is the new pattern (owner
  directive 2026-05-23) — PRs open ~minutes between push and merge.
- **`main` is the integration branch.** New code work starts a fresh
  branch off `main`; docs-only commit straight to `main` per preferences
  §7 ([[feedback_doc-changes-no-pr]]).
- **Published on nuget.org (immutable): `0.1.0-alpha`, `0.2.0-alpha`,
  `0.3.0-alpha`.** Everything from CL-50 onward is **unreleased** — on
  `main`, in no package. Release is owner-driven + batched
  ([[feedback_release-cadence]]); never cut for one fix without explicit
  "release now".
- **126/126 Diablo4 + 8/8 transport tests green on live build `3.0.2.71886`.**

### Autonomous CASC⇄Optimizer loop (owner-authorized 2026-05-22 + auto-merge 2026-05-23)

Owner authorized the two agents to **negotiate the API to consensus, then CASC
builds / Optimizer consumes / iterate on inefficiencies, working independently
unless a critical decision is required**. Poll actively to minimize latency.
2026-05-23 extension: **auto-merge after CI green** — once CI checks pass,
run `gh pr merge --squash --delete-branch` from CASC's side; no need to wait
for owner manual-merge. The full FR-C21..C26 cluster shipped this way over
2026-05-22..05-24.

### FR-C22..C26 tooltip arc — DELIVERED (chrome side) / open (affix structural-8)

Full state in [[project_post-c21-tooltip-arc]]. Headline:

- **FR-C22 (CL-75)** `ParagonNodeInfo.LocalizedTitle` via sibling
  `ParagonNode_<SnoName>` (label `Name`) — `awaiting:optimizer`,
  `needs:owner`. Gate → "Board Attachment Gate", Start → "Paragon
  Starting Node", class-rares → authored names ("Binding" etc.).
- **FR-C23/C26 chrome (CL-77/80/81/82)** `ParagonTooltipChrome` —
  the full 3-layer composite (BaseLayer + PanelByRarity + OrnateFrame)
  + variants + banners + `SkillIconAtlas` (`2DUI_Tooltip_Icons`) +
  typed `Divider` (`Center_Divider_White` 1559055). Bullet glyph =
  Unicode `◆` procedural fallback; icon bezel = deferred residual.
  **Engine controller is encrypted** ([[project_engine-controller-code-encrypted]])
  — Phase-C EXE RE is permanently impossible.
- **FR-C25 (CL-78)** `Diablo4Storage.GetAttributeName(int, locale)`
  via curated `AttributeNames.LabelByAttributeId` (~40 ids) + sno-4080
  `AttributeDescriptions` template strip. Retires CL-76 basic-four
  hardcode on the live path. `awaiting:optimizer`.
- **FR-C24 (CL-79 + CL-83; slice 2b OPEN)** — sibling-StringList slice
  done; glyph engine constants done (`BaseRadius=3` /
  `RadiusUpgradeLevels=[25,50]` / `MaxLevel=150` per
  [[project_engine-constants-pattern]]); affix-side 4 fields
  (DisplayFactor / AffectedAttributes / SkillTagSelector /
  Requirements) + rarity refinement stay open on `casc-fr#36` as
  slice 2b. **First new-session target.**
- **FR-C27 (#39, awaiting:casc)** `DataAttributes` (sno 1907204) full
  registry decode — deferred from CL-78. Would retire the curated map.
  Deep RE; entry layout known (stride 360, name@+0, gbid@+256,
  +104 aux) but AttributeId field offset not pinned.
- **FR-C28 (#40, awaiting:casc)** tag-conditional `(AttributeId,
  ParamPlus12)` → label map for attr 259 (Demonology / Conjuration /
  Hellfire / etc.). Medium RE.

### Catalog discovery API — SHIPPED + iterating (FR-C20)

`d4.Catalog` (`Find`/`OfKind`/`TryResolve`/`TryGet<T>`,
`AssetRef(Kind,Group,Sno,Name,Tags)`, `Facets` /
`Related` / `FindByFacet`) shipped through CL-55..60 + the FR-C26
chrome additions (CL-77/80/81/82). New family = one
`IAssetProvider` + one `AssetKind` in
`src/WiseOwl.Casc.Diablo4/Catalog/AssetProviders.cs`, zero facade
edits. Per [[feedback_optimizer-as-customer-proxy]] — don't pre-build
speculative kinds; Optimizer drives what's needed via FR.

### Open casc-fr issues (snapshot 2026-05-24 late — re-poll before acting)

CASC turn (1, "awaiting:casc"):
- **#39** FR-C27 — `DataAttributes` full registry RE.

Optimizer turn (7, all "awaiting:optimizer" or "needs:owner"):
- **#32** FR-C20 — `fr:delivered`, `needs:owner` to bless `fr:consumed`.
- **#33** FR-C21 — `fr:delivered`, `needs:owner` to bless `fr:consumed`.
- **#34** FR-C22 — `fr:delivered`, `needs:owner`.
- **#35** FR-C23 — `fr:delivered`, `awaiting:optimizer` (consumer
  visual-close iteration).
- **#36** FR-C24 — `fr:delivered` (CL-79+CL-83+CL-84 the full arc), `awaiting:optimizer`.
- **#37** FR-C25 — `fr:delivered`, `awaiting:optimizer`.
- **#38** FR-C26 — `fr:delivered`, `awaiting:optimizer` (consumer
  visual-close iteration).
- **#40** FR-C28 — `fr:delivered` (CL-85), `awaiting:optimizer`.
- **#42** FR-C30 — `fr:delivered` (CL-87, `35649fe`), `awaiting:optimizer`.
- **#41** FR-C29 — `fr:accepted`, **`awaiting:casc`** (per-class stat
  formulae; open-ended RE — see Active-threads table).
- **#39** FR-C27 — `fr:proposed`, **`awaiting:casc`** (`DataAttributes`
  registry RE; coupled to FR-C29).

(Older `fr:consumed`-closed issues #22/24/25/27/29 etc. covered in
the historical section below.)

### Suggested new-session ordering

1. **Poll** — re-check `awaiting:casc` (this snapshot drifts as the
   Optimizer's session may have added counter-rounds).
2. **FR-C29 (#41) + FR-C27 (#39) are the two open RE threads** and are
   coupled — both may hinge on the same `DataAttributes` (1907204) /
   `LevelScaling` (206158) decode. Owner's directive on FR-C29: search
   all non-identified sources, short-circuit via the name-hash-grep
   technique (DJB2 the in-game stat/attribute names → grep unidentified
   SNOs → cluster hits → cross-validate the adjacent float against the
   expected coefficient). Start with `DataAttributes` (retires FR-C27's
   curated map AND is a FR-C29 candidate if per-attr scalars co-locate),
   then `LevelScaling` (Phase-2 baselines: Warlock@70 Life=1526 etc.),
   then `SimpleScalarFormulas`/`DamageMitigation`. Ship as multi-slice
   CLs (FR-C24 precedent); honest engine-coded boundary if the mine dries.
3. **Season-14 test re-baseline** (housekeeping, non-blocking) — update
   the 4 drifted anchors on the live `3.1.1.72836` suite; the two Power
   SF_N tests need owner oracle re-validation first.

### Older FR issues (historical — all delivered or closed)

Older issues `#22..#31` (FR-C12 through FR-C19, plus FR-T1) all
sit `awaiting:optimizer` / `needs:owner` / `fr:consumed` as of the
session of 2026-05-22. See historical sections below + `Appendix A`
in `docs/casc-diablo4-format.md` for the per-CL audit trail
(CL-42..CL-65 cover this older arc).

### Key recent findings (don't re-discover these)

- **UI-scene §10.3 field grammar is now COMPLETE (CL-48).** A field's
  value is stored as **either** a 56-byte `0x22` record **or** a 12-byte
  **tag-2 block** (`tag==2,+4==0,value@+8`); widgets use either, mixed.
  The old 0x22-only parser under-decoded tag-2 widgets (e.g. chrome
  centre's 1200² rect read as zero). Parent widgets nest **anonymous,
  name-less child sub-records** (a class id + `0xFFFFFFFF` at +0x08 — the
  rarity disc state layers); a parent confines its field scan to the run
  before its first child.
- **Node recipe per-state (CL-47):** `ParagonNodeRecipeLayer.SelectionDiscs`
  (`Unselected`/`Selected`) splits the rarity disc pair — don't flatten
  them (selected ring would draw on unselected nodes). Handles match the
  owner #22 oracle (Magic `0x621CB6FF`/`0x72C29402`, etc.).
- **`DecodeMip0` BC row-pitch is texture-specific (CL-49):** derive
  blocks-per-row from the exact mip0 byte count (`SerTex[0].SizeAndFlags
  ÷ (blockRows×blockSize)`), NOT `Align(width,64)`. Atlas 447106 is
  128-aligned (pitch 1280, not 1216) — the guess garbled it.
- **Surfaces shipped this arc:** `ReadParagonNodeRecipe` /
  `ParagonNodeRecipe`, `ReadParagonBoardGrid` / `ParagonBoardGrid`,
  `TiledStyleDefinition` (+ `ReadTiledStyle`), `Diablo4.KnownFieldNames`/
  `KnownTypeNames`, `ParagonBoardChrome.TiledStyleBindings`.
- **Cumulative hash-decode** (owner directive): persistent dictionary
  `docs/d4-hash-dictionary.md` + `Diablo4.KnownFieldNames`/`KnownTypeNames`;
  re-scan opaque blobs after each crack. Still-uncracked of note:
  field `0x0CDB00E9` (DT_INT, small signed ints — a blind name-brute
  missed it; needs the d4data `FieldChecksums` registry, not on disk here).
- **Discipline lessons (memory):** never name an API role from
  atlas-name/dimensions or widget-name alone (structural evidence only);
  validate a decode-correctness claim on a **structured** frame, not a
  flat one (a flat frame masked the CL-49 row-pitch bug → I wrongly
  blamed the consumer on #26); subagents must never touch `e:\Casc`.

### Devlogs

**FR-C21..C26 arc (2026-05-22..05-24):**
- 0062 FR-C21 bonus mechanic + StatTag (CL-67)
- 0063 budget multipliers + magnitude evaluator (CL-68)
- 0064 ParagonNodeInfo projection + GetNodeInfo (CL-69)
- 0065 GetBoardNodes hot path + EnumerateNodes (CL-70)
- 0069 Gate-stats fix (CL-74)
- 0070 LocalizedTitle FR-C22 (CL-75)
- 0071 StatName per-attribute fix (CL-76)
- 0072 GetParagonTooltipChrome FR-C23 Option A (CL-77)
- 0073 GetAttributeName FR-C25 (CL-78)
- 0074 Glyph sibling-StringList projection FR-C24 slice 1 (CL-79)
- 0075 Tooltip chrome multi-layer composite FR-C26 (CL-80)
- 0076 SkillIconAtlas FR-C26 slice 3 (CL-81)
- 0077 Divider field FR-C26 (CL-82)
- 0078 Glyph engine constants FR-C24 slice 2a (CL-83)

**Earlier arc (FR-C8..C20):** devlogs 0016–0061 cover FR-C8..C20 +
the FR-C13/C14/C16/C19 deep RE. `docs/casc-diablo4-format.md`
Appendix A is the authoritative CL log; `docs/d4-hash-dictionary.md`
the hash registry.

## What this project is

`WiseOwl.Casc` — a clean-room, modern, fully-documented .NET library for
Blizzard's **CASC** content-storage stack, with Diablo IV as the first game
module. **Not** a fork of CascLib; informed by it (MIT) + the upstream
reverse-engineering record. Published under the **Wise Owl Software** org
(GitHub `WiseOwlSoftware` + the reserved `WiseOwl.*` NuGet prefix); Brent
Rector is sole owner/admin of both that org and the `BrentRector` account.
Package `Authors` = "Brent Rector"; `Company`/copyright = "Wise Owl
Software". The `WiseOwl.*` prefix is a reserved anti-impersonation naming
decision.

- **Local root:** `e:\Casc` (git repo; commit+push freely at milestones).
- **GitHub:** `WiseOwlSoftware/WiseOwl.Casc` (**public**; origin set;
  `gh` authed as `BrentRector`; commits use the
  `<id>+BrentRector@users.noreply.github.com` form — plain
  `brent@wiseowl.com` is rejected by GitHub email privacy).
- **Build:** .NET 10 SDK. `dotnet build/test WiseOwl.Casc.slnx`. Both
  libraries multi-target `net8.0;net10.0` (netstandard2.0 was dropped
  2026-05-21 — single modern consumer; removing it cleared the analyzer
  warnings that the ns2.0 API gaps forced + the System.Memory/polyfill
  baggage).
- **Live data:** Diablo IV at `D:\Diablo IV` (product `fenris`, build
  `3.0.2.71886`). Integration tests self-skip without it; set
  `WISEOWL_CASC_INSTALL` / `WISEOWL_CASC_CORETOC` to point elsewhere. The
  upstream extracted CoreTOC.dat is also auto-found at the Paragon path.
- **Never commit game bytes.** `.gitignore` covers extracted data.

## Standing preferences (binding)

1. Full XML docs on every public type/member; inline comments on
   non-obvious blocks. Overrides any "minimal comments" default.
2. Modern C# 14 / best-possible API; no back-compat required.
3. Self-contained specs in `docs/`; maintain the correction log; when the
   spec is found wrong/incomplete, **correct it and add the true content**
   (cross-reference, don't duplicate, `e:\Paragon\docs\d4-binary-formats.md`
   §3–§8.15 — the upstream D4 SNO/.tex record of truth).
4. Commit+push at milestones. Commit trailer:
   `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`.
5. Maintain `docs/ARTICLE-SOURCE.md` (expand, never shrink — obey its own
   header), `docs/devlog/NNNN-*`, this file, and project memory every
   meaningful session. Do NOT write into the wiseowl.com site repo.
6. Do not modify `e:\Paragon` except to read.
7. **Docs-only changes commit straight to `main`** (devlog, `casc-*-format.md`,
   `docs/fr-*`, RELEASING, this file, CHANGELOG prose, ARTICLE-SOURCE) —
   no PR. The branch → PR → CI → merge flow is **only** for code, tests,
   workflows, and public-API/`docs/api` surface (what CI validates).
   Rationale + the `paths-ignore`+required-checks deadlock it avoids:
   memory `feedback_doc-changes-no-pr`.

## Architecture

- `src/WiseOwl.Casc` (game-agnostic transport): `Md5Key`/`ContentKey`/
  `EncodingKey`, `CascException`, `CascOpenOptions`, `Configuration/`
  (`BuildInfo`, `BuildConfiguration`), `Indices/LocalIndex`,
  `Encoding/` (`Blte`, `EncodingTable`), `Tvfs/TvfsManifest`,
  `Internal/` (`Bytes`, `CascPathHash`), `CascStorage` (orchestrator).
- `src/WiseOwl.Casc.Diablo4`: `SnoGroup`, `SnoRecord`, `CoreToc`,
  `TextureDefinition`, `CombinedTextureMeta`, `Diablo4Storage` (facade:
  `Open`, `CoreToc`, `ReadSno`/`OpenSno`, `TextureMeta`, `SnoPath`).
- `tests/` xUnit: CI-safe synthetic + self-skipping live-data
  (`Xunit.SkippableFact`). `samples/Casc.Sample.Console`.

## Status (end of session 1) — see also devlog 0001

**PROVEN end-to-end vs live D4 `3.0.2.71886`:**
- Transport: `.build.info`→config→16-bucket `.idx`→archive envelope→BLTE
  (real ~100 MB multi-chunk `encoding`)→encoding table→closed-loop
  CKey→EKey→index.
- TVFS: resolves+reads `Base\CoreTOC.dat`, `Base\Texture-Base-Global.dat`.
- CoreTOC `0xBCDE6611`: 849,257 SNOs / 181 groups (stock CascLib NuGet
  overflows here; ours doesn't).
- Combined-meta `0x44CF00F5`: 140,197 defs; `2DUI_ParagonNodes` → BC3
  4224×192, 31 ptFrames (matches upstream §8.13/§8.15).
- **Per-SNO read by id (FR-1/FR-2) CLOSED** — D4 SNO address is
  `Base\<Folder>\<id>` (NOT name-path, NOT `base:meta\<id>`; correction
  CL-4). TVFS walk was always complete (1,759,690 entries). `ReadSno`/
  `TryReadSno` (Meta+Payload), `SnoNotFoundException`, `0xABBA0003`
  shared-payload transparent fallback, image-agnostic BC1/BC3
  `DecodeMip0`→raw RGBA, `CoreToc.TryGetId`, archive handle cache — all
  proven on the live install.
- 14 tests pass, 0 skipped; solution builds 0 warnings. The
  ParagonOptimizer migration blocker is **closed**.

## Next steps (priority order)

1. Hand the resolution back to the ParagonOptimizer session — it can now
   migrate D4Extract off vendored CascLib onto this library (full meta
   pipeline + textures).
2. Optional, non-blocking: BC7 decode (paragon is BC3; only if a needed
   atlas is BC7); BLTE `'E'` key store; streaming/async `OpenRead`
   (currently eager `byte[]`/`MemoryStream` — fine for the dataset
   workload). `CoreTOCReplacedSnosMapping` only if a seasonal patch 404s
   a known SNO (FR-6, deferred).
3. Round-2 consumer FRs (`docs/feature-backlog.md`) — ALL done, none
   deferred. FR-11/12/15 (named groups + int escape hatch;
   `Diablo4.GbidHash` == `0x42C16A1B`; `ReadGroup` streaming);
   **FR-14 DONE** (folder-generic resolver; concrete Child anchor pinned
   2026-05-17 — SNO 1015186/group 71 → `Base\Child\1015186-0`, CL-19;
   ≈547k census via `CascStorage.DiagnosticPaths` / SnoScan
   `childpaths`; test no longer self-skips). **FR-13 StringList:
   REVERSE-ENGINEERED, implemented & proven**
   — per-locale `0x44CF00F5` bundle `base/StringList-Text-<locale>.dat`;
   `StringListCatalog` / `Diablo4Storage.GetStrings(locale)` /
   `TryGetString`. Definitive spec: `casc-diablo4-format.md §6.3` + CL-7;
   narrative `devlog/0003`. (Container = texture combined-meta family but body at
   `B=alignUp8(prevEnd)`, no `+8`, SNO positional from index.)
4. Round-3 typed readers (B1–B6) — **DONE & PROVEN** (converged design,
   owner-approved): `ParagonBoardDefinition`/`ParagonNodeDefinition`
   (+`NodeAttribute` with `NParam`+`ParamPlus12`, `ParagonRarity`,
   `SnoPassivePower`)/`ParagonGlyphDefinition`/`ParagonGlyphAffixDefinition`
   /`AttributeFormulaTable` + `Diablo4Storage.Read*` + `TryGetIconFrame`.
   Raw fields only; **library ships NO formula evaluator** (decided). §7
   acceptance matrix passes verbatim (201912=1038 entries, CoreStat_Normal
   →"5"). ~~Library scope FROZEN at "B1–B6"~~ — **scope-freeze LIFTED
   by owner 2026-05-17; C6 DELIVERED** (devlog 0015): typed
   `PlayerClassDefinition`(+`ReadPlayerClass`; SnoId+`eClass`@payload+16,
   CL-21) / `PowerDefinition` / `AffixDefinition` / `ItemDefinition`
   (+`ReadPower`/`ReadAffix`/`ReadItem(id,locale)`; identity +
   localized text via the **generalized sibling-StringList convention**
   §6.7/CL-20 — `<TypePrefix>_<snoName>` group-42, `Item_`Name/Flavor/
   TransmogName, `Affix_`Desc, `Power_`name/desc; CL-22). Boundary
   intact: identity + verifiable raw/localized fields only — deep
   Power/Item gameplay records NOT modeled, **still no formula
   evaluator**. Test `C6_typed_readers_decode_identity_and_localized_
   text` (live 3.0.2.71886). Backlog now fully DONE, nothing deferred.
5. **SPEC AUTHORITY: TWO canonical byte-format docs (mirror the two
   packages), each with its own CL-* log:** `docs/casc-format.md`
   (CASC/TACT/TVFS/BLTE transport) and `docs/casc-diablo4-format.md`
   (Diablo IV SNO/container/record; has the provenance & migration map +
   library boundary appendices). Upstream
   `e:\Paragon\docs\d4-binary-formats.md` §3–§8.15 is SUPERSEDED for
   layouts (frozen, history/article source only). Policy carve-out (6
   intrinsic values, scoring, relight, JSON schema) referenced, never
   absorbed. Keep these two docs definitive (transport facts →
   `casc-format.md`; D4 facts → `casc-diablo4-format.md`); do not
   re-merge them or re-introduce the frozen upstream as a layout source.
6. **API docs** = generated from XML comments into `docs/api/` (per
   package, per type/member) via pinned `xmldocmd`
   (`.config/dotnet-tools.json`); regen `scripts/gen-api-docs.{sh,ps1}`;
   CI `api-docs` job fails on drift. XML doc comments stay the source of
   truth — never hand-edit `docs/api/`; change the comments + regenerate.
   `docs/api/README.md` is the hand-written reading guide.
7. **NuGet packaging** done & verified: both libs pack `.nupkg`+`.snupkg`
   (per-TFM + XML docs, per-package README, MIT, SourceLink, dep groups;
   Demeanor house style). Icons (SVG→PNG ladder, `scripts/gen-icons.*`):
   `wiseowl-org` = the **org** mark = the owner's finished raster design
   `assets/Brown Owl.png` composited on the brand tile via
   `build/TileIcon` (colours/alpha preserved, NOT traced/recoloured;
   NOT a package icon — for the nuget.org org profile);
   `WiseOwl.Casc` package icon = **CASC lettermark** (`build/Lettermark`);
   `WiseOwl.Casc.Diablo4` = the **D·IV** sibling. `scripts/gen-icons.*`
   drives TileIcon (org) + Lettermark (CASC) + IconGen (SVG marks).
   `build/OwlTrace` (potrace) is retained for line-art sources but is
   out of the current pipeline. IP rule stands: never trace third-party
   imagery into a shipped mark; match the pipeline to the source kind
   (trace line art, composite a finished design).
8. **CI/release pipeline** (devlog 0008; runbook `docs/RELEASING.md`):
   `ci.yml` = validation only, PR-into-`main` + push-`main`, doc/asset
   `paths-ignore`, `concurrency: cancel-in-progress`; work branches carry
   NO trigger (feature-branch/PR model). `publish.yml` publishes BOTH
   packages and fires ONLY on `release: published` — four gates: release
   trigger, `nuget` Environment required-reviewer approval, tag ==
   committed `<Version>` guard, `--skip-duplicate` idempotency. Auth =
   NuGet.org Trusted Publishing (OIDC), **no stored key**. **PUBLISHED:
   `0.1.0-alpha` (2026-05-17) and `0.2.0-alpha` (2026-05-18) — both
   packages live on nuget.org, immutable.** `0.2.0-alpha` = FR-C7 +
   FR-D1/D2/D3 + FR-14 + C6 (tag → `ce9f778`/PR #15). One-time infra
   (env + reviewer, `NUGET_USER`, Trusted-Publishing policy) is done &
   proven. **Release cadence is owner-driven & batched — never
   prep/bump/cut a release for a single fix without an explicit
   "release now" (memory `feedback_release-cadence`).** `main`'s
   `<Version>` is still `0.2.0-alpha` (the immutable published number);
   it gets bumped only when the owner decides to cut the next batched
   release.
9. **FR-C8 (DELIVERED 2026-05-18, PR #16, CL-23 — an FR-C7
   correction; devlog 0016, `docs/fr-c8-response.md`).** Start/gate
   composites ARE in ParagonBoard 657304 (verdict #2 located). FR-C7's
   "no gate/start texture" was wrong: §10.3 modelled only the 56-byte
   0x22 record; Starter/Quest bind via a fixed **0x58-byte block**
   (tag@+0=2, value@+8, ownerClassId@+0x20, 0xFFFFFFFF@+0x28).
   Oracle-exact (Start 0xA0F996FE/0xF8312CA8; Gate 0xA0F996FE/
   0xC2DF4786 sel/0x0E6B6249 unsel; symbol = per-node HIconMask; no
   disc). Shipped `UiWidget.ExtraLayerValues` (raw) + corrected typed
   `start.*`/`gate.*` States.Layers (catalog-validated). Residual
   (rect/scale, shader brightness, exact sel↔unsel split) default /
   consumer-owned. Spec §10.12. **UNRELEASED — `0.2.0-alpha` was
   published from PR #15 (`ce9f778`) BEFORE FR-C8 (PR #16); FR-C8 is on
   `main`, in no package, and per owner is NOT released on its own
   (batched into a future owner-cut release).** **R5/R6 (PR #17,
   ddeb52d, CL-24, §10.13):** `Arrow_*` + `Connector_*` bind real art +
   authored rect (FR-C7 §6 correction — they were dropped by a
   last-0x22-record-straddle bug in `UiScene.Parse`, now surgically
   fixed; `overlay.pointerTriangle`/`connectorBar` populated,
   `selectionRing` genuinely empty). Arrows: Top `0xD51CAB25`/Right
   `0x6D3CB8DE`/Bottom `0x8EEAC178`/Left `0xB6D8C741`; connectors
   `0x77ECA3A8`/`0x288DE11F`. **R5 definitive:** start/gate 0x58 blocks
   are handle-only — no per-layer rect authored (inherit `NodeTemplate`
   box); `Rect`/`Alpha` stays default. **Animation definitive #3:**
   per-node glow pulse is engine-driven (no authored timing;
   `Storyboard_*` are UI transitions) — `AnimSpec=null` reaffirmed,
   bake a static frame. Reopen only with an in-game oracle showing
   authored timing. **R7/R8** select/deselect brightness/colour =
   definitively not authored (engine shader; `Tint`/`LitTint=null`
   the decoded answer; CL-24(d)). **R9 (PR #18, 31f33ce, CL-25 — an
   FR-C7 correction):** FR-C7's r3/r4 "ornate" (`Elem("NodeAvailable
   Glow")`=`0x4A901508`) was the SAME projection gap as start/gate —
   it never read `Template_Node_Rare`/`_Legendary`'s own `0x58` layer.
   Corrected: r3/r4 now `disc` + the template's own decode-true ornate
   (Rare `0xB71BD068`, catalog-validated `LayersOf`); `0x4A901508` is
   `NodeAvailableGlow` = the **selectable/available glow** (state, any
   rarity) → new **`overlay.availableGlow`** State (handle+Rect). §7.2
   matrix now **19 rows** (pre-publish amendment). Spec §10.11/§10.13.
   **R10 consumer action pending:** this revises FR-C7 r3/r4 the
   consumer validated in R3/R4 (switch rare/leg ornate to corrected
   `States.Layers`, add the glow overlay). Legendary/socket Common-path
   pulse composition otherwise unchanged.
9a. **FR-C9 (DELIVERED 2026-05-18, PR #19 fafcb35, CL-26 — devlog
   0019, `docs/fr-c9-response.md`).** Made completeness *structural*
   so there's no FR-C8-style R10/R11. Root cause: the CL-23 `0x58`
   block model over-fit (required ownerClassId@+0x20 + sentinel@+0x28;
   not universal; last block straddles next nameStart) → a class of
   real bindings still dropped (grey ring `0x87A89F86` et al.).
   **CL-26:** `UiScene.Parse` Pass-2c relaxed to the only stable
   marker `tag==2,+4==0,value@+8`, value-bounded ⇒ raw `ReadUiScene`
   **lossless** for texture bindings. Shipped
   `ReadParagonRenderModel()` (`ParagonRenderModel{Layout,Scenes}` —
   657304+964599, every binding widget `{handle,rect,alpha}`,
   shape-agnostic; one-shot manifest), `IsParagonTextureHandle` (the
   shared structural test: ≥`0x10000` ∧ catalog-resolvable), and the
   **coverage gate** `ParagonRenderModel_covers_every_bound_atlas_
   handle` (scans every 4-aligned u32 in both raw scenes → asserts all
   surfaced; a future gap fails casc CI). Spec §10.14 (published
   binding-record schema + losslessness guarantee). Boundary unchanged
   (consumer owns role/state classification).
9b. **FR-C7 (DELIVERED — RE complete, all gates met; devlogs 0010/0011,
   spec §10 + CL-9..CL-14, consumer contract `docs/fr-c7-api-proposal.md`
   §7).** D4 UI-scene format (group 46 = type `UI`, hash `0xE4825AB8`;
   `ParagonBoard` SNO 657304) fully reverse-engineered **standalone &
   clean-room**. Key decoded facts (all in spec §10):
   - **D4 identifier hash = DJB2 core `h=h*33+ch` SEED 0** (not 5381):
     `Diablo4.TypeHash` (no-lower, full u32), `Diablo4.FieldHash`
     (`& 0x0FFFFFFF`), `Diablo4.GbidHash` (lowercased). Public, shipped.
     Names absent from SNO data (hash-keyed) but in `Diablo IV.exe`
     reflection registry → recovered by string-extract+hash+match.
   - **Record header (CL-13):** `classOff = nameStart +
     alignUp8(strlen+1) + 0x10`; class id @classOff; `0xFFFFFFFF`
     sentinel @classOff+0x08. **Schema** = packed 12-byte
     `(fieldHash, typeHash("DT_BINDABLEPROPERTY")=0x1332C78D, DT_type)`.
     **Instance values** = fixed 56-byte `0x22` records, value@`+0x08`,
     positionally keyed to the schema.
   - **CanvasRef 1920×1200** (`ParagonBoard_main`); node element
     `Template_Node_Common`=100×100 ref; disc `Node_IconBase` inset 7 →
     86; **`PitchRef=100/1200`, `DiscRef=86/1200`**, Ornate/Symbol/
     SocketRing÷Disc=`100/86`. Over-determined 67.7 anchor (consumer
     oracle {zoom0,7680×2160,Warlock-Start}) **REPRODUCES** →
     `RenderRatios.Provisional=false`.
   - Grey ring / connectors / pointers / per-rarity tint / pulse-anim
     are **NOT in the data** (app-drawn / fixed shader §2.3 / engine) —
     `0`/`null` is the *decoded answer*, evidence-backed, not gaps.
   - **CL-14:** the `build/SnoScan widgets` heuristic over-attributes
     by nearest-name → **recon only**; the shipped header-pinned
     `Diablo4Storage.ReadUiScene` is the authoritative parser.
   Shipped public API (`Diablo4Storage`): `ReadUiScene(snoId)` (raw
   widget graph), `ReadParagonRenderLayout()` (typed §7.1 projection,
   18-row §7.2 `States`), `Diablo4.TypeHash/FieldHash`. PRs #5–#10
   merged; **PUBLISHED in `0.2.0-alpha` on nuget.org (immutable) — the
   FR-C7 §7 contract is now FROZEN for that surface.** (The old
   `artifacts/fr-c7-pack/*0.1.1-alpha.nupkg` local pack is obsolete;
   `0.1.1-alpha` was never released — superseded by `0.2.0-alpha`.)
   **Working with the
   optimizer:** FR loop; consumer is on HOLD (durable record
   `e:\Paragon\docs\fr-c7-paragon-render-layout.md` Round-11/12);
   `docs/fr-c7-api-proposal.md` §7 is the converged frozen-until-publish
   contract; relay status to the consumer via the owner. RE tool
   `build/SnoScan` (recon only, per CL-14). NEVER fabricate a number;
   the oracle is the check, not the source — discipline held all round
   (CL-13/14 caught over-claims before ship).
10. **FR-D1 + FR-D2 (DELIVERED — devlog 0012/0013, spec §6.4/§6.5/§6.6
    + CL-15/16/17, reports `docs/fr-d1-response.md` /
    `fr-d2-response.md`).** All answered **(B)** (API gaps → minimal
    typed surfaces). **Durable opaque-id principle** (owner, 2026-05-17;
    mirrored verbatim to spec Appendix C, outlives the FR): a consumer
    treats every SNO **name** as an opaque stable id and never
    decomposes its substructure; a D4 naming convention is a data
    mapping in the same category as a byte layout — decoded once
    library-side, CL-* + re-verify, exposed typed; "readable string not
    bytes" does not move the boundary.
    - **FR-D1 board name (Round-1, PR #11 merged):** sibling StringList
      SNO `"ParagonBoard_"+boardSnoName` (group 42), label `Name`,
      strictly name-keyed (no SNO offset). `Diablo4Storage
      .TryReadParagonBoardName`/`ReadParagonBoardName`; `SnoGroup
      .StringList=42`. CL-15/§6.4.
    - **FR-D1 rescoped — typed class/index:** the `ParagonBoard` record
      has NO class/index field; only source is the name
      `Paragon_<Token>_<NN>`. Decoded library-side: token = between
      `Paragon_` and final `_`; index = trailing int (variable width —
      `Paragon_Spirit_0`=0); class = unique case-sensitive prefix of
      exactly one §6.5 PlayerClass roster SnoName (data-driven, throws
      on ambiguity/none). `ParagonBoardDefinition.ClassSnoId/
      .ClassSnoName/.BoardIndex` populated by `ReadParagonBoard(int)`;
      byte-only `Parse(blob)` keeps `0`/`""`/`-1`. CL-16/§6.6.
    - **FR-D2 class roster:** group 74 `PlayerClass`; localized name =
      `General` StringList table SNO 4118, label
      `"PlayerClass"+SnoName+"Male"` (markup-free; base label has
      `|5sing:plur`). Real-class filter = that label exists (excludes
      `Axe Bad Data`, no hardcoded list). `CharacterClass(SnoId,
      SnoName, DisplayName)` + `Diablo4Storage.ReadCharacterClasses(
      locale)` (ordered by SnoId, cached). `ClassSnoId`==FR-D1's
      `CharacterClass.SnoId` (shared stable key). CL-17/§6.5.
    - **FR-D3 glyph→class (devlog 0014):** `ParagonGlyphDefinition`
      (group 111) has a per-class bool fixed array `fUsableByClass` at
      payload `+0x24`; slot index = the class's **eClass rank** —
      position when the §6.5 roster is sorted asc by `eClass`
      (PlayerClass record payload `+16`; sparse 0/1/3/5/6/7/9/10 →
      ranks 0..7). Over-determined: `_Necro` glyphs→rank4=Necromancer
      AND consumer-verified Warlock=idx7=rank7. Well-formed guard:
      affix `dataOffset`@payload`+0x50`==104 (junk `Axe Bad Data`
      732443→empty). `ParagonGlyphDefinition.UsableByClassSnoIds`
      (shared PlayerClass SNO key) via `ReadParagonGlyph(int)`;
      byte-only `Parse`→empty. CL-18/§7.3. Retires the consumer's
      Maxroll `classFilter`/`ClassByFilterIndex`/`ParagonClass` enum.
    Tests `ReadParagonBoard_resolves_typed_class_and_index` +
    `ReadCharacterClasses_returns_first_party_roster` +
    `ReadParagonBoardName_resolves_localized_board_name` +
    `ReadParagonGlyph_resolves_usable_by_class` (live `3.0.2.71886`).
    `SnoScan stl`/`stlfind`/`glyphclass` recon-only. Same
    amend-until-publish contract as FR-C7.
11. CHANGELOG/devlog/ARTICLE-SOURCE upkeep each session.
12. Later: future `.Wow`/`.Overwatch`/`.D2R` modules (core designed for them).

## Gotchas

- GitHub rejects commits authored as `brent@wiseowl.com` (email privacy);
  use the `<id>+BrentRector@users.noreply.github.com` form (already set in
  the local repo `user.email`).
- `.idx`/`data.NNN`: open `FileShare.ReadWrite` (game holds them).
- Encoding header `ESpecBlockSize` is at byte **18** (correction CL-1).
- Git on Windows warns LF→CRLF; harmless.
- Re-verify trigger: `.build.info` Build Key change (seasonal). Re-run
  integration tests; update the relevant correction log on drift
  (`casc-format.md` for transport, `casc-diablo4-format.md` for D4).
- A published NuGet version is immutable & permanent (unlist-only, never
  delete/re-upload). Never publish ad hoc — only via the gated
  `publish.yml` (GitHub Release → env approval). To release: bump
  `Directory.Build.props` `<Version>`, land on `main`, then
  `gh release create v<Version>`. Tag must equal `<Version>` or the job
  fails pre-pack. Full procedure: `docs/RELEASING.md`.
