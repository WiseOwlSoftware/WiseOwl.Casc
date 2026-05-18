# Resume prompt — WiseOwl.Casc

> Full-context handoff. After a compaction or a new session, read this
> end-to-end before continuing. Companion docs: the two canonical
> byte-format specs `docs/casc-format.md` (CASC/TACT/TVFS/BLTE transport)
> and `docs/casc-diablo4-format.md` (Diablo IV SNO/container/record
> layer) — each with its own correction log; `docs/devlog/` (the
> narrative), `docs/ARTICLE-SOURCE.md` (wiseowl.com article source).

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
- **Build:** .NET 10 SDK. `dotnet build/test WiseOwl.Casc.slnx`. Core
  multi-TFMs `netstandard2.0;net8.0;net10.0` (ns2.0 uses
  `src/Shared/Polyfills.cs` for `IsExternalInit`).
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
   authored timing. Legendary/socket *composition* UNCHANGED (FR-C7
   Common path).
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
