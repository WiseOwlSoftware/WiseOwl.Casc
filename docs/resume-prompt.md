# Resume prompt — WiseOwl.Casc

> Full-context handoff. After a compaction or a new session, read this
> end-to-end before continuing. Companion docs: the two canonical
> byte-format specs `docs/casc-format.md` (CASC/TACT/TVFS/BLTE transport)
> and `docs/casc-diablo4-format.md` (Diablo IV SNO/container/record
> layer) — each with its own correction log; `docs/devlog/` (the
> narrative), `docs/ARTICLE-SOURCE.md` (wiseowl.com article source).

## CURRENT STATE (2026-05-21) — read this first

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

### Active branch / PR / release state

- **PR #34 (`fr-c14-r9-tiled-style`) MERGED and RELEASED** in
  `v0.3.0-alpha` (CL-42..CL-49). **CL-50** (PR #36, FR-C16 R9 / FR-C18
  child sub-record rects, squash `5397868`) and **CL-51** (PR #37, FR-C16
  R11/R12 typed `NodeActivation` surface + EXE-RE of the binding mechanism,
  squash `2614e9b`), and **CL-52** (PR #38, FR-C16 R14 flat
  `ParagonNodeRecipe.Components` + `bActive`-driven activation, squash
  `d97ff8b`), **CL-54** (PR #40, FR-C16 #26.4 socket disc remapped to the
  base-disc band, squash `4d3efaa`), and **CL-55** (PR #41, FR-C20 `d4.Catalog`
  asset discovery/retrieval API + folds in CL-53 selection highlight, squash
  `0f1764f`) MERGED to `main`, **unreleased** (no package). **PR #39 closed —
  superseded** by #41 (selection highlight folded into the Catalog). No open
  PRs. New code work starts a fresh branch off `main`; docs-only commit straight
  to `main` (pref §7).
- **Published on nuget.org (immutable): `0.1.0-alpha`, `0.2.0-alpha`,
  `0.3.0-alpha`.** **CL-50/51/52/54/55 are unreleased** — on `main`, in no
  package (CL-53 folded into CL-55). Release is owner-driven & batched (never
  cut for one fix without explicit "release now").
- **`d4.Catalog` (FR-C20/CL-55) is the discovery surface:** `Find(AssetQuery)`/
  `OfKind`/`TryResolve` → `AssetRef(Kind,Group,Sno,Name,Tags)`; `TryGet`/
  `TryGet<T>` → the real decoded type (exception-safe). New family = one
  `IAssetProvider` + one `AssetKind`, zero facade edits. **FR-C20 #32 is OPEN,
  `awaiting:optimizer`** — soliciting consumer feedback on missing kinds/tags/
  filters/ergonomics (owner: Optimizer is the proxy for other customers). The
  existing typed `ReadX()` accessors remain as shortcuts.
- 50/50 Diablo4 + 8/8 transport tests green on live build `3.0.2.71886`.
- **FR-C16 node recipe is the flat `Components` model (CL-52, R14).** Draw
  every `ParagonNodeComponent` whose `Activation.Evaluate(facts)` holds, in
  z-order, at its rect/alpha/tint. Owner-oracle-validated this session: base
  disc = **Unpurchased↔Purchased** swap (`bActive`-driven, NOT selection);
  node KIND is one mutually-exclusive dimension; purchased add-on = arrows
  (→purchasable nbr) + connectors (→purchased nbr); `rgbaTint` + anchoring
  applied; selection highlight is an EXTERNAL engine cursor (→ FR-C19).
  Hash-dictionary mislabel fixed (`0x093CBAA8` = `eHorizontalAnchoring`, not
  `eGroupType`) via the new `build/SnoScan checkfields` validator. The
  engine binds visibility BY NAME in the compiled `ParagonBoardUI`
  controller; EXE field names are hashed/absent; see [[reference_exe-symbol-re]].

### Catalog discovery API — SHIPPED (FR-C20 / CL-55), feedback open

`d4.Catalog` shipped (commit `0f1764f`): generic discovery/retrieval so the
consumer finds/enumerates(filtered)/retrieves any RE'd recipe or definition
dynamically. Open generic-retrieval model (`TryGet`→real decoded type, no
wrapper union); providers (`IAssetProvider`) one per `AssetKind`; exception-safe.
Next increments (await consumer feedback on **FR-C20 #32**): richer `Item` tags
(slot/type/class), more kinds (chrome, item-types, game-balance, string tables),
typed enumerators / relationships / build-stable refs — **don't pre-build these;
the owner wants the Optimizer (proxy for other customers) to drive what's
needed.** When asked to add a kind: write one provider in
`src/WiseOwl.Casc.Diablo4/Catalog/AssetProviders.cs` + one `AssetKind` value.

### Open casc-fr issues (2026-05-21 snapshot — re-poll, this drifts)

- **#32** FR-C20 Catalog discovery/retrieval API — **DELIVERED** (CL-55,
  `0f1764f`, unreleased). `awaiting:optimizer` — explicitly soliciting consumer
  feedback on missing kinds/tags/filters/ergonomics/ref-stability/relationships.

- **#26** FR-C16 node render recipe — flat `Components` model. **CL-52**
  (`d97ff8b`) base; **CL-54** (`4d3efaa`) socket-disc fix (#26.4): `Usage_Slot_*`
  is the `KindSocket` type-disc carrier → its disc children remapped into the
  base-disc band (below symbol/arrows/connectors), 12² side-panel pip dropped.
  Owner visually validated ("socket looks good"). `awaiting:optimizer`. Open
  observation passed back: `Usage_Slot_2[0]` (grey, centred) vs `[1]` (untinted,
  absolute) are the same handle `0xF6443089`, both `bActive=1` → both draw.
- **#30** FR-C19 selection-highlight resource — **DELIVERED** (`fr:delivered`,
  `awaiting:optimizer`). Authored as named TiledStyle 9-slice recipes (group 103)
  over `2DUI_SelectionHighlight` (337357)/`2DUITiled_SelectionHighlight` (585030):
  `SelectionRectangleInset` + `ControllerSelection{Rectangle,Circle,Diamond,
  TearDrop,APS}`. Shape from the authored name (corrected eyeballed labels:
  `0xBA7D2638`=TearDrop not "circle"; `0x0BD8A829`=Circle not "diamond").
  Surfaced via `ReadSelectionHighlight()` AND `Catalog.OfKind(SelectionHighlight)`
  (CL-53 folded into CL-55).
- **#31** FR-T1 UI texture-atlas catalog API + browser app — `awaiting:casc`
  (scoping done: 4,726 atlases, BC1/BC3=99%; hierarchical-tree API design).
- **#29** FR-C18 rarity-template WidgetRect all-zero — `fr:delivered`; CL-50
  (parent rect faithful; disc inset 7 on the children). `awaiting:optimizer`.
- **#27** FR-C17 board grid/composition — `awaiting:optimizer` (CL-45).
- **#22** FR-C12 special-node composites — `awaiting:optimizer`.
- **#24** FR-C14 ParagonBoardChrome — `needs:owner` (CL-42/43; CL-48 note).
- **#25** FR-C15 per-node cell tile — `needs:owner`.
- **#28** `DecodeMip0` BC row-pitch bug — resolved (Optimizer: CL-49 stone
  decodes clean ✓).

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

### Devlogs for this arc

0016–0026 (FR-C8/C9/C10/C11/C12), 0033–0034 (FR-C13 power formulas),
0039 (FR-C13 phase 3), 0040 (TiledStyle), 0041 (node recipe + grid),
0042 (per-state split), 0043 (tag-2 grammar crack), 0044 (tag-2 shipped),
0045 (DecodeMip0 row-pitch). `docs/casc-diablo4-format.md` Appendix A is
the authoritative CL log; `docs/d4-hash-dictionary.md` the hash registry.

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
