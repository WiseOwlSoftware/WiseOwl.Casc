# 0027 — FR-C11 R3 §1 Phase 2 R&D: rim texture lookup is a non-trivial subsystem

*2026-05-19*

The CL-32 rim sides (`Template_Board_Background_{Top,Right,Bottom,Left}`)
bind handles `0x900C7D87` and `0x225F2DA8` via the standard
`0x6B1C5D9C` (DT_TEXFRAME) texture-handle field — scene-authored,
not engine-internal. But neither handle resolves via CASC's existing
icon-catalog index (`Diablo4Storage.TryGetIconFrame` → the multi-frame
`0x44CF00F5` combined-meta bundle), so consumers can't extract or
render the rim from the texture catalog the library currently
exposes.

FR-C11 R3 §1 asked CASC to surface the non-icon-catalog texture
resolution path for these two handles. Phase 2 R&D explored the
standard hypotheses. **All dead-ended.** This devlog records the
search so a future investigator doesn't repeat it.

## Dead-ends explored

1. **Full TextureMeta brute search** — iterated every SNO key in
   `d4.TextureMeta.BySno` (140,197 textures, 176,376 frames) checking
   each frame's `ImageHandle`. Neither rim handle appears anywhere in
   the icon catalog.

2. **GBID hash of texture file names** — tried `Diablo4.GbidHash` /
   `TypeHash` / `FieldHash` on `ui_paragon_glowLine`,
   `ui_paragon_glowLineThin`, `UI_Paragon_FrameGlow`,
   `2DUI_ParagonBackground`, `2DUI_Paragon`, and case variants
   (`UI_PARAGON_GLOWLINE`, etc.). None match either rim handle.

3. **SNO ID = handle value** — checked every SNO group for an entry
   whose ID equals `0x900C7D87` (= 2 416 540 039) or `0x225F2DA8`
   (= 576 662 952). No match in any group.

4. **Byte-scan of candidate texture-def Meta blobs** — read the SNO
   Meta blobs of the candidate single-frame textures
   (`ui_paragon_glowLine` 1302551, `ui_paragon_glowLineThin` 1302489,
   `UI_Paragon_FrameGlow` 1364280, `2DUI_ParagonBackground` 1447773,
   plus the multi-frame `2DUI_Paragon` 447106 and
   `2DUI_Paragon_transparentElements` 2061536) and scanned for the
   target handles as any-offset `u32`. Neither handle is present in
   any of those Meta blobs.

5. **Hash combinations** — quick checks on `gbid ^ sno`, `sno + gbid`,
   and a few other arithmetic combinations of SNO IDs with name
   hashes. No match (results not captured here as they were
   exploratory only).

## What CASC would need to add

A non-icon-catalog texture-handle index. Plausible paths:

- A separate handle-resolution table somewhere in the game data CASC
  currently doesn't parse (a CoreTOC variant, an unparsed segment of
  the `0x44CF00F5` combined-meta bundle, or a different SNO group's
  data).
- A compiled-material / 9-slice handle table referencing single-frame
  textures by a handle distinct from `TexFrame.ImageHandle`.
- A content-key / file-path hash CASC hasn't tried yet (the standard
  `GbidHash` / `TypeHash` / `FieldHash` don't match — there may be a
  different D4 hash variant for this surface).

None of these are quick wins; each is its own subsystem-level R&D
task with no guarantee of success.

## Disposition

§1 is paused at `needs:owner` on the FR-C11 #21 thread per the
B-6 default ("pause rather than guess on anything the authoritative
records can't settle"). Options for the owner:

- **Accept the procedural rim fallback** — the consumer composites
  a procedural orange border (the interim that's been working) and
  CASC tracks the missing subsystem as a deferred backlog item.
- **Invest in the subsystem** — direct CASC to do the deeper R&D
  (probably starting with the unparsed segments of the combined-meta
  bundle), with no guarantee of finding the resolution path and
  significant context cost.

`Template_Board_Background_{Top,Right,Bottom,Left}` and the two rim
handles are surfaced in `ParagonBoardChrome.{BorderTop, BorderRight,
BorderBottom, BorderLeft}` since CL-32 — handle, scene-bind verified
against raw `ReadUiScene`, `AtlasSno`/native px = 0. No code change
from CL-33 to CL-34 (R&D-only round); just this devlog + the
`needs:owner` flag on #21.
