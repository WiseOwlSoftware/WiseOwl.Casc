# Resume prompt — WiseOwl.Casc

> Full-context handoff. After a compaction or a new session, read this
> end-to-end before continuing. Companion docs: the two canonical
> byte-format specs `docs/casc-format.md` (CASC/TACT/TVFS/BLTE transport)
> and `docs/casc-diablo4-format.md` (Diablo IV SNO/container/record
> layer) — each with its own correction log; `docs/devlog/` (the
> narrative), `docs/ARTICLE-SOURCE.md` (wiseowl.com article source).

## CURRENT STATE (2026-07-17) — read this first

This block is the live state. Superseded session-history sections (the old
0.4.0/0.6.0-release snapshots, the CL-66..88 trajectory table, the delivered
FR-C21 / FR-C22–C26 / Catalog write-ups, and the session-1 "Status" + numbered
"Next steps 1–12") were removed 2026-07-17 — the per-CL audit trail lives in
`docs/casc-diablo4-format.md` Appendix A, the delivered-FR narrative in
`docs/devlog/` + project memory. What remains is live state plus the
load-bearing "don't re-discover" facts.

### ⏭️ NEXT-SESSION PICKUP (2026-07-17) — start here

**Published: `WiseOwl.Casc` + `WiseOwl.Casc.Diablo4` `0.6.0` live on NuGet.**
`main` tip **`dfabf35`**; `<Version>` = 0.6.0; working tree clean. The FR-loop
counter-round marathon (CL-92 → CL-99) shipped in 0.6.0; **CL-100 (LIB-3 R7) +
CL-101 (FR-C34) are unreleased on `main`**. Release
mechanics: `docs/RELEASING.md` (GitHub Release → `nuget` env gate → OIDC). **The
env gate is approvable via API with the owner-authed CLI** (`gh api
repos/.../actions/runs/<id>/pending_deployments` → `current_user_can_approve`;
POST `{environment_ids:[<id>],state:"approved"}`). NuGet CDN + validation lag a
few min after a successful run — cache-bust the flat-container and poll the
registration endpoint (`registration5-gz-semver2/<pkg>/<ver>.json` → 200).

**0 open CASC turns** (queue clear 2026-07-17, re-polled). Recently disposed, all
now `awaiting:optimizer`: `casc-fr#50` FR-C34 **delivered CL-101**
(`WiseOwl.Casc@dfabf35`), `casc-fr#45` R7 **delivered CL-100** (`11ba44f`), and
`casc-fr#39` disposed **`fr:by-design`**. The queue is live and the Optimizer
counter-rounds within minutes — **re-poll `awaiting:casc` before assuming idle.**

**#45 R7 — DELIVERED (CL-100, `11ba44f`).** Max legendary rank = **10**, a
**universal engine constant** — every one of the 699 `legendary_*` powers carries
an identical `("10",10.0)` script-formula-tail sentinel (0 exceptions,
`3.1.1.72836`; owner-confirmed FR-C13 R2), and it is **not** a per-aspect field.
Surfaced `PowerDefinition.MaxRank` (decoded from the sentinel) +
`PowerDefinition.MaxLegendaryRank` (const) for the affix path; rank is **1-based**
(dominant `…(CurrentLegendaryRank()-1)…` shape) → aspect span
`[formula(1) … formula(10)]` (630 g104 affixes). §8.1 rewritten as a **grammar**
(ternary/comparison, `DataAttributes` bare-var refs, `PowerTag` cross-refs).
`PowerTag.S10ChaosTuningPerClass."Script Formula N"` (86) = **identifiable but not
numerically resolved** (referent needs the deferred FR-C13 binary-AST decode — a
real follow-up if the Optimizer wants the per-class values). 32 residual = **21
GBID-backed** (computable via existing path) + **11 genuine** non-roll (Skill-Rank
grants / `Owner.*` set powers / a test affix). Spec §8.1/§11.2/§11.3, Appendix A
CL-100, devlog 0094. Residual to flag: the rank *floor* (0 vs 1) is inferred from
the `-1` convention, not oracled — the Optimizer can pin it with one in-game min.

**#50 FR-C34 — DELIVERED (CL-101, `dfabf35`).** Typed `DifficultyTiers` (1973217)
= the per-**monster-level** scaling curve: `Diablo4Storage.ReadDifficultyTiers()`
→ `DifficultyTiersTable` (150 rows; `MonsterHpScalar`/`MonsterDamageScalar` =
**inferred** labels, no oracle per AC-3; `PerLevelXpValue` L40=8/L70=11 is the
row-layout lock; raw 32-col row on `.Columns`). **Reconciled §8.2** — monster HP
scales off this far steeper curve (×101,051 vs hpScalar ×30.5 @ L70), NOT
`LevelScaling` rows 71–200. **Monster-data RE (owner ask):** monsters = Actor
SNOs (g1, ~61k; `.acr` = identity+appearance/anim, no base-HP field); base HP is
engine-assembled, not a flat field (same boundary as player base `50`); mapped
`MonsterLevelCurves` (`Raid_Tier_0..5`) / `MonsterNames` / `MonsterAffixCategories`
/ `MonsterTags` (typeable on request). Spec §8.3, Appendix A CL-101, devlog 0095.
**PENDING fast-follow (owner + Optimizer requested):** type `LevelScaling`'s 5
remaining monster columns (`monsterDr`/`powerBase`/`powerDelta`/`powerItem`/
`xpScalar`) — `powerItem` may supply `IPower()` for the §8.1 affix evaluator (#45).

**#39 FR-C27 — RESOLVED `fr:by-design` (2026-07-17, owner call; now
`awaiting:optimizer`).** The Optimizer verified CL-97 on published 0.6.0
(60/92 = 65.2 % on its looser predicate; 68.2 % on CASC's 85-id population — a
population difference, not reconciled into one figure), confirmed the `707`/`1207`
rescues and zero regressions, and accepted the §11.3 structural-ceiling argument,
but declined to self-mark `fr:consumed` (original bar was 100 % + retire the
curated map; 32 ids still null, `LabelByToken` still curated). Owner set
**`fr:by-design`**: §11.3 records the ceiling as the terminal outcome (protocol §5
— a recorded decision, never a silent drop). The residual is genuinely
node-context-dependent (`707` = DOT on one node, Bleed on another, so a
context-free resolver would re-introduce the FR-C31 wrong-name defect); the
Optimizer takes it consumer-side via `ParagonNodeStat.StatName`. A node-side read
source, if one ever surfaces, is a fresh FR — not a re-open.

**Everything else is `awaiting:optimizer`** (consume-verify against 0.6.0):
- **#41 FR-C29** — Phase 1 (universal coeffs + per-class map, CL-89) + Phase 2
  (base Max Life = `round(50 × hpScalar[level])` from `LevelScaling`, CL-99),
  both shipped. Phase-4 difficulty ladder = `StringList 216612` (not CASC's).
- **#49 FR-C33** — `ParagonMagnitudeFormula.TryEvaluate` (unsupported-fn vs
  legit-NaN), CL-98. Older #43/#44/#46/#47/#48 consumed or awaiting-optimizer.

**Surfaces shipped this arc (0.5.0 + 0.6.0):** `AffixDefinition.Effects`
(`AffixEffect`: `AttributeId`/`ParamPlus12`/`FormulaGbid`/`InlineFormula`/
`AttributeName` + `HasParam`/`IsDataDefinedAttribute`/`DataAttributeOrdinal`) +
`.StaticValues`; `Diablo4Storage.TryGetDataAttributeName`;
`AttributeFormulaTable.TryGetByGbid`; `ParagonMagnitudeFormula.TryEvaluate`;
`Diablo4Storage.ReadLevelScaling()` → `LevelScalingTable` (`HpScalar`/`BaseLife`/
`BaseHitpointsMax`/`MaxCharacterLevel`). Spec §8.1 (formula grammar),
§8.2 (LevelScaling base Life), §11.3 (affix effects + `GetAttributeName`
ceiling); Appendix A CL-92..CL-99; devlogs 0088–0093.
**Unreleased (CL-100/101, `main`):** `PowerDefinition.MaxRank` +
`.MaxLegendaryRank` (max legendary rank = 10, universal); §8.1 rewritten as a
grammar (ternary/comparison, `DataAttributes` bare-var refs, `PowerTag`
cross-refs); `Diablo4Storage.ReadDifficultyTiers()` → `DifficultyTiersTable`
(per-monster-level curve; §8.2 reconciled — monsters use a separate steeper
curve); Appendix A CL-100/101; devlogs 0094/0095.

**★ DISCIPLINE — session meta-lesson ([[feedback_calibrate-claims-to-evidence]]):**
the decodes were excellent but three closing claims overreached this session
("#39 retires the map"→re-keyed; "#46 no coverage lost"→3 lost; "305 uniques"→
verified only gear affixes). The Optimizer catches them by re-deriving before
consuming. **Scope every summary sentence to exactly what was measured; a named
residual / "not in the data" is a valid recorded answer.** Related #41 lesson
(spec §8.2): a render-time value can exist nowhere — `1526` = `round(50 ×
hpScalar)`; **search for the operands, not the result**; a lone matching number
is weak evidence in big tables (both sides found a real `860`, reasoned about the
wrong one).

**TODO next release:** modernize the package READMEs
([[project_readme-update-next-release]]) — stale `CascStorage.OpenLocal`/`ReadPath`/
hardcoded path → `Diablo4Storage.Open()` auto-detect + the typed D4 API. Docs
commit straight to `main`.

**Recon tooling on `build/SnoScan`** (all on `main`): affix — `affixcorpus`,
`affixattrmap`, `affixfloatscan`, `affixeffects`, `inlineformula`, `multcheck`;
attribute — `attrname` (+DataAttr), `attrmap`, `coverfix`, `dataattrs`; formula —
`formulafind`, `formula`, `formulagbid`, `formuladump`; raw — `rawhex`, `snoid`,
`listgroup`. **R7 (CL-100):** `inlinedump` (all g104 inline formulas),
`affixstr` (per-modifier string slots), `ranksentinel` (max-rank sentinel scan,
proved 699/699 = 10), `rollableresidual` (the 32-residual split), `powersf`;
**FR-C34 (CL-101):** `strdump` (generic per-SNO printable-string dump — for
GameBalance / monster-table RE).
Session scratchpad corpus: `affix-corpus-full.txt`, `affix-attrmap.txt`,
`affix-floatscan.txt`, `inline-corpus.tsv`.

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
  `3.1.1.72836` — Season 14, seasonal drift). Integration tests self-skip
  without it; set
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
   `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
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
