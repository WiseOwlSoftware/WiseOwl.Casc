# Resume prompt — WiseOwl.Casc

> Full-context handoff. After a compaction or a new session, read this
> end-to-end before continuing. Companion docs: `docs/casc-format.md`
> (self-contained transport spec + correction log), `docs/devlog/` (the
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
   FR-14 mechanism done (folder-generic resolver; concrete Child id gated
   on RE). **FR-13 StringList: REVERSE-ENGINEERED, implemented & proven**
   — per-locale `0x44CF00F5` bundle `base/StringList-Text-<locale>.dat`;
   `StringListCatalog` / `Diablo4Storage.GetStrings(locale)` /
   `TryGetString`. Definitive spec: `casc-format.md §9` + CL-7; narrative
   `devlog/0003`. (Container = texture combined-meta family but body at
   `B=alignUp8(prevEnd)`, no `+8`, SNO positional from index.)
4. Round-3 typed readers (B1–B6) — **DONE & PROVEN** (converged design,
   owner-approved): `ParagonBoardDefinition`/`ParagonNodeDefinition`
   (+`NodeAttribute` with `NParam`+`ParamPlus12`, `ParagonRarity`,
   `SnoPassivePower`)/`ParagonGlyphDefinition`/`ParagonGlyphAffixDefinition`
   /`AttributeFormulaTable` + `Diablo4Storage.Read*` + `TryGetIconFrame`.
   Raw fields only; **library ships NO formula evaluator** (decided). §7
   acceptance matrix passes verbatim (201912=1038 entries, CoreStat_Normal
   →"5"). **Library scope FROZEN at "B1–B6 + existing"** for the
   eliminate-D4Extract goal. Typed Item/Affix/Power/Class deferred (C6).
5. **SPEC AUTHORITY: `e:\Casc\docs\casc-format.md` is the single canonical
   CASC+D4 byte-format reference** (§§1–14 + §15 provenance map + CL-* log).
   Upstream `e:\Paragon\docs\d4-binary-formats.md` §3–§8.15 is SUPERSEDED
   for layouts (frozen, history/article source only). Policy carve-out (6
   intrinsic values, scoring, relight, JSON schema) referenced, never
   absorbed. Keep this file definitive; never re-introduce a second
   layout doc.
6. CHANGELOG/devlog/ARTICLE-SOURCE upkeep each session.
7. Later: future `.Wow`/`.Overwatch`/`.D2R` modules (core designed for them).

## Gotchas

- GitHub rejects commits authored as `brent@wiseowl.com` (email privacy);
  use the `<id>+BrentRector@users.noreply.github.com` form (already set in
  the local repo `user.email`).
- `.idx`/`data.NNN`: open `FileShare.ReadWrite` (game holds them).
- Encoding header `ESpecBlockSize` is at byte **18** (correction CL-1).
- Git on Windows warns LF→CRLF; harmless.
- Re-verify trigger: `.build.info` Build Key change (seasonal). Re-run
  integration tests; update `docs/casc-format.md` correction log on drift.
