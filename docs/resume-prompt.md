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
- 12 tests pass, 1 honest skip; solution builds 0 warnings.

**THE ONE OPEN GAP — resume here:** per-SNO resolution by id
(`Diablo4Storage.ReadSno` → `Base\Meta\<grp>\<name><ext>`). Top-level
`Base\*.dat` resolve; per-SNO records do not yet — they live in a deeper
nested `vfs-N` sub-manifest the TVFS walk doesn't fully descend, and/or the
D4 root applies an extra name/shared-payload transform here (cf. upstream
§8.11 `D4RootHandler.CreateSNOEntry` + `0xABBA0003`
`CoreTOCSharedPayloadsMapping`). `TvfsManifest` already has sub-manifest
recursion + a path accumulator; the next step is to trace **which** `vfs-N`
carries the SNO subtree (instrument the walk: dump resolved top-level paths
and which sub-manifests are entered), confirm the prefix accumulation across
the recursion boundary, then layer shared-payload aliasing for texture
payload de-dup. The test `Reads_a_SNO_record_by_id` auto-promotes from
skip→pass once `ReadSno` works.

## Next steps (priority order)

1. Close the per-SNO TVFS gap (above) → un-skip; full DoD #2.
2. Shared-payload `0xABBA0003` mapping (texture payload aliasing).
3. `WiseOwl.Casc.Diablo4` `.tex` BCn decode (image-lib-agnostic: raw
   RGBA / DDS out) + atlas `ptFrame` slicing; node↔icon via
   `hIconMask`/`hIcon` == `ptFrame.ImageHandle` (upstream §8.13).
4. Async stream-y `OpenRead` (currently eager MemoryStream); key store for
   BLTE `'E'` (encrypted) chunks if ever needed.
5. CHANGELOG/devlog/ARTICLE-SOURCE upkeep each session.
6. Later: ParagonOptimizer drops vendored CascLib for this package;
   future `.Wow`/`.Overwatch`/`.D2R` modules (core is designed for them).

## Gotchas

- GitHub rejects commits authored as `brent@wiseowl.com` (email privacy);
  use the `<id>+BrentRector@users.noreply.github.com` form (already set in
  the local repo `user.email`).
- `.idx`/`data.NNN`: open `FileShare.ReadWrite` (game holds them).
- Encoding header `ESpecBlockSize` is at byte **18** (correction CL-1).
- Git on Windows warns LF→CRLF; harmless.
- Re-verify trigger: `.build.info` Build Key change (seasonal). Re-run
  integration tests; update `docs/casc-format.md` correction log on drift.
