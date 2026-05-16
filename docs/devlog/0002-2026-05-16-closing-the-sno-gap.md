# 0002 — 2026-05-16 — Closing the per-SNO gap (consumer feedback round)

> Narrative source for the wiseowl.com session. Continues 0001.

## What happened

The future first consumer — the Diablo IV ParagonOptimizer project — was
handed a structured prompt asking whether WiseOwl.Casc was sufficient to
adopt as-is. It walked its own D4Extract call sites and returned a precise,
prioritized backlog (FR-1 … FR-10). Verdict: foundations solid and already
*better* than its vendored code on CoreTOC + combined-meta, but **blocked on
one capability — per-SNO read by id (P0)**. Every board/node/glyph/gam read
in that project is a by-id read; without it nothing migrates.

This is a good article beat: the library proving itself against a real
consumer's real call sites, not a synthetic wishlist — and the consumer's
single most valuable contribution being a *hint*, not a complaint.

## The hint that cracked it

The Optimizer's note: D4Extract's working path is the **id-keyed**
`base:meta\<id>` / `base:payload\<id>` namespace (what CascLib.NET resolved
directly), whereas WiseOwl's `ReadSno` was building the WoW-Tools-style
**name path** `Base\Meta\<grp>\<name><ext>`. "Resolving the id-keyed
namespace may be the faster, more reliable route."

So instead of theorizing, I instrumented the TVFS walk and dumped reality
against the live install. The result reframed everything:

- The clean-room TVFS walk was **never incomplete**. It resolves
  **1,759,690** entries; all 37 nested `vfs-N` sub-manifests are descended;
  the entire install tree is there. The "deep traversal gap" in devlog 0001
  was a wrong diagnosis.
- The actual D4 SNO address is **`Base\<Folder>\<id>`** — numeric id, no
  group folder, no name, no extension. `Base\Meta\2458674` → **HIT**. Both
  the name-path *and* the `base:meta\<id>` colon form miss. Neither the
  WoW-Tools scheme nor the CascLib.NET scheme; a third, simpler form.

Lesson worth keeping for the article: the bug wasn't where the symptom
pointed (traversal depth). One empirical probe replaced a session of
plausible-but-wrong theory. "Measure the tree, don't reason about it."

## What got built (the backlog, adopted in full)

- **FR-1** `ReadSno`/`TryReadSno` by id → `Base\<Folder>\<id>`;
  `SnoNotFoundException` (a `CascContentNotFoundException` subtype) so a
  legitimately-absent SNO is *skippable*, not a crash. CoreTOC is now
  needed only for name↔id, not addressing.
- **FR-2** `Payload` by id + `0xABBA0003` `CoreTOCSharedPayloadsMapping`
  as a **transparent fallback** (empty/absent direct payload follows the
  alias). Surprise from the data: with the *complete* TVFS the per-class
  atlases (e.g. Warlock 2550887) have **direct** payloads — the upstream
  "no direct entry" was a CascLib.NET narrow-view artifact. Aliasing still
  matters (35,616 entries) and is proven on a real mapping entry.
- **FR-3** `SnoRecord.Ascii` (+ `AsciiAbsolute`); confirmed the
  record-style `DT_VARIABLEARRAY` (`{i64 pad, off@+8, size@+12}`,
  payload-relative) is distinct from the combined-meta variant the library
  already owns internally.
- **FR-4** image-library-agnostic `TextureDefinition.DecodeMip0` → raw
  straight-alpha RGBA32 `DecodedImage` + `Crop`; clean-room BC1/BC3, no
  imaging dependency, `Align(64)`-decode-then-crop. A real `ptFrame` slice
  verified as non-blank art. (The sample caught a `stackalloc`-in-a-loop
  stack overflow — fixed by hoisting the per-block scratch buffer; good
  reminder that a runnable sample is a test.)
- **FR-5** the library/consumer **boundary documented** in
  `casc-format.md`: transport + CoreTOC + combined-meta + `SnoRecord` +
  BCn live in the library; typed paragon records + GameBalance intrinsics
  stay in the consumer.
- **FR-6** `CoreTOCReplacedSnosMapping` intentionally **deferred** (not
  needed on this build; gated on a future 404).
- **FR-7/8** `CoreToc.Load(path)`/`Parse(span)` offline (already there) +
  `TryGetId`/`GetId` name→id index.
- **FR-9** archive `FileStream` handle cache in `CascStorage` (a dataset
  run does hundreds of by-id reads; re-opening `data.NNN` each time was the
  cost). FR-10 (node↔icon helper) left to the consumer by design.

## Status

All P0/P1 proven end-to-end against live D4 `3.0.2.71886`; 14 tests pass,
0 skipped, 0 build warnings. The migration blocker is **closed** — the
Optimizer can consume the library for its full meta pipeline + textures.

## Next

1. Hand the resolution back to the Optimizer session (it can now migrate
   D4Extract off vendored CascLib).
2. Optional: BC7 decode (only if a needed atlas is BC7; paragon is BC3),
   BLTE `'E'` key store, streaming reads — none currently blocking.
