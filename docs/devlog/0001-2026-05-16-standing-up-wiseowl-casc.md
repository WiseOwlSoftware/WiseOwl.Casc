# 0001 — 2026-05-16 — Standing up WiseOwl.Casc

> Chronological narrative source for the wiseowl.com dev-log/article
> session. Decisions, discoveries, dead-ends, and *why*. Not a changelog.

## Goal of this session

First session of the new library. Definition of done: repo + scaffold;
clean-room CASC transport that opens the live Diablo IV install and
BLTE-reads content; the `0xBCDE6611` CoreTOC parser proven on real data;
docs/resume/memory; an honest status report.

## What we did, and why

**Absorbed the prior art instead of re-deriving it.** The ParagonOptimizer
project already reverse-engineered the whole D4 pipeline
(`e:\Paragon\docs\d4-binary-formats.md` §3–§8.15). We treated that as
authoritative and did *not* re-discover SNO/CoreTOC/`.tex` facts. The new
work was the **CASC transport**, which that spec deliberately omits ("CascLib
already implements this"). So the transport had to be specified from scratch
— now `docs/casc-format.md`, self-contained, with a correction log.

**Clean-room, not a fork.** The locked decision: this is a redesign
*informed by* the MIT WoW-Tools CascLib (and other references), not a copy.
We studied the reference to learn the byte layouts (idx header math, BLTE
frame table, TVFS path-table state machine, encoding pages) and then wrote a
modern API from understanding. Credits live in `NOTICE`/`THIRD-PARTY.md`.

**Modern API, no back-compat.** Per an explicit mid-session directive:
value-type `ContentKey`/`EncodingKey` (typed so you can't pass the wrong
one — the exact bug class older `byte[]`-everywhere libraries had), records,
`init` options, spans, async, file-scoped namespaces. Multi-TFM
(`netstandard2.0;net8.0;net10.0`) with a tiny `IsExternalInit` polyfill.

**Ownership, settled.** A few mid-session corrections converged here:
Brent Rector is sole owner/admin of *both* the `BrentRector` account and
the `WiseOwlSoftware` org, so there's no third-party ambiguity. Final
state: the repo lives at **`WiseOwlSoftware/WiseOwl.Casc` (public)**;
package `Authors` = "Brent Rector", `Company`/copyright = "Wise Owl
Software". The `WiseOwl.Casc` package id uses the reserved
`WiseOwl.*` NuGet prefix (a naming/anti-impersonation decision, not an
ownership claim).

## Discoveries / gotchas

- **The encoding-header off-by-one (CL-1).** `ESpecBlockSize` is at byte
  **18**, not 17 (a 1-byte `unk1` sits at 17). Reading it at 17 misaligned
  every CKey page; the table still yielded *some* entries so a count check
  passed, but real CKey→EKey lookups silently failed. Caught only by a
  *closed-loop* assertion on real data (`install` CKey must resolve to an
  EKey that is in the local index). Lesson: validate transport layers with
  a closed loop, not a "looks big enough" heuristic.
- **File locking is real.** `.idx`/`data.NNN` must be opened
  `FileShare.ReadWrite`; the running game / Battle.net agent holds them
  open. The user shut the game down mid-session, which is how we first saw
  the index parse succeed — then we made the reader share-tolerant so it
  works with the game running too.
- **"Jenkins96" is a bad public name.** Reviewer question: "why are we
  using someone's name?" Correct. It's Bob Jenkins' public-domain *lookup3*
  (`hashlittle2`). Renamed the type to `CascPathHash` (named for its job);
  algorithm attribution moved to docs/`NOTICE`. A good clean-room-redesign
  naming principle: types say what they do; lineage goes in credits.
- **The TVFS asymmetry, confirmed first-party.** Top-level `Base\*.dat`
  (`CoreTOC.dat`, `Texture-Base-Global.dat`) resolve cleanly through our
  clean-room TVFS. Per-SNO records (`Base\Meta\<grp>\<name><ext>`) do
  **not** yet — they sit deeper, in a nested `vfs-N` sub-manifest. This is
  the same "asymmetry wall" the upstream saga hit (§3/§8.5), now seen from
  the transport side: it isn't an exotic mechanism, it's a deeper TVFS
  subtree + the D4 shared-payload layer on top.

## Proven this session (against live D4 build 3.0.2.71886)

- `.build.info` → build config → 16-bucket local `.idx` (1,086,119 blobs)
  → archive envelope → **BLTE** decode of the real ~100 MB multi-chunk
  `encoding` blob → encoding table → closed-loop CKey→EKey→index. ✅
- Clean-room **TVFS**: resolves + BLTE-reads `Base\CoreTOC.dat` and
  `Base\Texture-Base-Global.dat`. ✅
- **CoreTOC `0xBCDE6611`** parsed from the real ~40 MB current-build file
  (849,257 SNOs / 181 groups) — the file the stock CascLib NuGet
  *overflows* on. ✅
- **Combined-meta `0x44CF00F5`**: 140,197 `TextureDefinition`s;
  `2DUI_ParagonNodes` → **BC3, 4224×192, 31 ptFrames** — exactly the
  upstream §8.13/§8.15 verified facts, reproduced by the clean-room parser. ✅
- Sample console does all of the above end-to-end.

## The one open gap (honest)

Per-SNO resolution by id (`ReadSno`) — the deep TVFS subtree + D4
shared-payload (`0xABBA0003`) layer. The test for it **self-skips with a
precise reason** rather than reporting a false pass (honesty about
unfinished work is the brand). Next: trace which `vfs-N` holds the SNO
subtree; confirm sub-manifest recursion + path-prefix accumulation; then
layer shared-payload de-duplication (upstream §8.11).

## Next session

1. Close the per-SNO TVFS gap → `ReadSno` green; un-skip the test.
2. Shared-payload `0xABBA0003` mapping for texture payload aliasing.
3. `.tex` BCn decode helper in `WiseOwl.Casc.Diablo4` (image-library-
   agnostic: return raw RGBA / DDS); atlas `ptFrame` slicing.
4. Then: ParagonOptimizer migrates off vendored CascLib onto this library.
