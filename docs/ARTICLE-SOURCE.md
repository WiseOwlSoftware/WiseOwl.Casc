# WiseOwl.Casc — Article Source (SEED)

> **Status:** seed skeleton, drafted 2026-05-16 before the library project
> formally starts. This is *source material* for a future wiseowl.com
> session that writes the published article + dev-log series as `.mdx`
> (articles → `e:\Sites\wiseowl.com\src\content\articles\`, dev logs →
> `e:\Sites\wiseowl.com\src\content\devlogs\smalltalk-YYYY-MM-DD.mdx`).
>
> **Rules for whoever maintains this file:**
> - Expand each beat with real detail as the library is built; keep the
>   arc intact.
> - The authoritative *technical* record is
>   `e:\Paragon\docs\d4-binary-formats.md` §3–§8.15. **Cross-reference and
>   summarize it — never copy it here.** This file is the *narrative*.
> - The building session curates this file; it does NOT write into the
>   wiseowl.com site repo. The website session owns the final prose.
> - Keep it ship-quality: the website session should be able to write
>   from this without rediscovering anything.

---

## 1. Working titles (pick later)

- "WiseOwl.Casc: a modern .NET library for Blizzard's CASC — and the
  one-character typo that cost three days"
- "Reading Diablo IV's game files from scratch, and why we had to build a
  new CASC library to do it"
- "CascLib vs CascLib.NET: how a near-identical package name sent us down
  a multi-day rabbit hole"

## 2. The hook (lead with the war story, not the API)

A cosmetic bug — paragon node icons in a hobby optimizer app were subtly
wrong — turned into a multi-session excavation through Blizzard's entire
content-storage stack, a fragmented ecosystem of stale tools, and a
trap hiding in a NuGet package name. The payoff: a clean, modern,
documented, multi-game CASC library. This article is that story and the
library it produced.

Audience: .NET devs, game-data/mod tool authors, anyone who has fought
CASC/TACT. Tone: the owner's personal-site voice — narrative, technical,
honest about dead ends.

## 3. The arc (beats — each becomes one or more sections)

> Each beat: **what happened · the lesson · facts/quotes · spec xref.**

1. **The trivial trigger.** `Assets/Sprites/nodes/*.png` were mis-cropped
   in-game *screenshots* — dark node spheres, icon too low, bottom
   clipped. Captured by a screenshot tool (`tools/iconshot`), not
   extracted. Lesson: "capture" was the wrong idea from the start.

2. **CASC 101 (teaching sidebar).** What CASC is (Content Addressable
   Storage Container), the MPQ→CASC history, TACT/TVFS/BLTE, and that
   it's *Blizzard-wide* (WoW, D4, OW, D2R, HotS, SC2, WC3R) — only the
   per-game asset/record layer differs. This is *why* a general library
   is worth building. Keep it accessible: most readers don't know CASC.

3. **The asymmetry wall.** D4 exposes a texture's *pixel payload* by
   path but **not** its *metadata* (format/dimensions/mip table) — the
   meta is delivered through indirection. xref §3, §8.5. The moment it
   stopped being "crop the image better" and became reverse engineering.

4. **The honest graveyard of dead ends.** (Readers love this — keep it.)
   - "CASCConsole doesn't exist" → it does: it's the console build of
     open-source WoW-Tools/CASCExplorer; `fenris` is just D4's CASC
     *product code*, not a secret Blizzard tool. (Correcting a wrong
     belief on the record.)
   - `d4parse` (Go): stale parser, and it just shells out to CASCConsole;
     its mapping-file struct is the wrong record size / a `// TODO`.
   - `adainrivers/d4-texture-extractor`: archived, broken on current
     builds (issues literally titled "Pls help, doesn't work").
   - Heuristic BC decode: mathematically underdetermined without SerTex.
   - Hand-reversing the mapping + bundle container: multi-layer, shelved.
   xref §8.3–§8.10. Lesson: the ecosystem is fragmented, stale, and
   undocumented — the core motivation for the new library.

5. **(Optional sidebar) The session that ate itself.** A genuinely
   useful Claude Code war story: pasting a ~31 MB screenshot poisoned the
   conversation — every subsequent turn re-sent the oversized image and
   failed, with `/clear` the only apparent escape. Recovery: surgically
   patch the session `.jsonl` transcript (swap the giant base64 for a
   1×1 placeholder, keep the uuid chain) instead of nuking the
   conversation. Tangential to the library but a strong human-interest
   beat and a real technique; could be its own dev-log entry.

6. **The breakthrough — skepticism cracks it.** The owner pushed back:
   "it seems improbable Blizzard built a *totally different* mechanism
   just for texture metadata." Correct. Root cause was not exotic — it
   was the **wrong NuGet package**: the project depended on
   `CascLib.NET` (Scobalula — a thin native-Zezula wrapper with **no D4
   shared-payload logic**), while the library that actually works is
   `CascLib` (TOM_RUS / WoW-Tools — what CASCExplorer/CASCConsole use).
   Two packages, one space apart, opposite capabilities. **This is the
   thesis of the whole piece.** xref §8.11.

7. **Proving it, for real.** Vendored WoW-Tools CascLib *source* (the
   NuGet 1.0.23 overflows on the current `0xBCDE6611` CoreTOC); opened
   the live install in-process; pulled atlas payloads by SNO id; read
   the `0x44CF00F5` combined-meta bundle; cracked its variable-array
   layout *empirically* from raw bytes (`{i32 pad, i32 off@+4, i32
   size@+8, i32 pad}`, blob-relative — verified by exact packing); codec
   is **BC3, not BC7** (correcting the earlier guess); `ptFrame` slicing;
   pixel-perfect output. The node↔icon link is first-party:
   `ParagonNode.hIconMask == ptFrame.ImageHandle`. xref §8.12–§8.14.

8. **Engineering integrity — what's still not done.** Node icons are a
   *composite* (rarity disc + white symbol); the symbol-only slice isn't
   the finished icon. Documented honestly as the open gap (§8.15). The
   article should not pretend the story ends in total victory — that's
   the point of dev logs.

9. **Why a new library (the turn from story to product).** Every path
   led through stale, fragmented, undocumented tooling and a dangerous
   naming collision. Hence **WiseOwl.Casc**: modern, unified, thoroughly
   documented, multi-game .NET CASC — Diablo IV first, WoW/Overwatch/D2R
   designed-for.

10. **Naming & trademark — a mini-essay readers will quote.**
    - Why not `*CascLib*`: that one-space-apart stem is exactly the trap
      that cost days.
    - Why "CASC" is opaque to a layperson but *correct for the actual
      audience* (people who'd use it already search "C# CASC library").
    - The real tension: a single brand word **cannot** be both
      trademark-distinctive *and* self-explanatory — that's trademark
      law, not a failure of imagination. Comprehension must live in the
      descriptive package id + title/tags.
    - Nominative fair use: "for Diablo IV" as a compatibility descriptor,
      confined to the `.Diablo4` leaf, with a non-affiliation disclaimer
      and no Blizzard art/logos.
    - The structural fix: the **reserved, verified `WiseOwl.*` NuGet
      prefix** makes a one-character-off impostor impossible — the
      naming disaster cannot recur by construction.
    - The layered design that falls out of it (transport vs game module).

11. **The library: design & philosophy.** Clean-room redesign (not a
    fork): game-agnostic CASC/TACT/TVFS/BLTE core + per-game modules;
    span-based parsing; image-library-agnostic; multi-targeted;
    documented to the byte. Absorbs the verified knowledge from the
    origin project's spec rather than re-deriving it.

12. **Roadmap.** D4 module completion (the composite), then WoW /
    Overwatch / D2R modules; the ParagonOptimizer app eventually drops
    its vendored CascLib for `WiseOwl.Casc`.

13. **Appendix / credits.** Point to `e:\Paragon\docs\d4-binary-formats.md`
    §3–§8.15 as the byte-level technical deep dive (the article stays
    narrative). Credit references: WoW-Tools/CascLib (MIT),
    Dakota628/d4parse, DiabloTools/d4data, HoldMyBeer-gg/rustydemon.
    Disclaimer: not affiliated with/endorsed by Blizzard Entertainment;
    Diablo/WoW are Blizzard trademarks; use only your own legally-
    obtained game files.

## 4. Through-lines to keep consistent

- **Honesty about dead ends and unfinished work** is the brand. Don't
  sand off the rabbit hole; it's the value.
- **The thesis:** ecosystem fragmentation + a naming collision = real
  cost; a documented, well-named, unified library is the remedy.
- **Teach, don't just recount:** CASC/TACT primer, the trademark essay,
  and the binary-format insights should each leave the reader abler.

## 5. Dev-log series plan (companion `smalltalk-YYYY-MM-DD.mdx` posts)

Chronological, informal, one beat-cluster each. Suggested entries:
1. "The icon that wouldn't crop" — the trigger + CASC primer (beats 1–2).
2. "Everything that didn't work" — the dead-end graveyard (beat 4) (+
   optional: "the session that ate itself", beat 5).
3. "It was the package name all along" — the breakthrough (beats 6–7).
4. "Naming a library so this never happens again" — trademark/naming
   essay (beat 10).
5. "Building WiseOwl.Casc" — design + first milestones (beats 11–12);
   then ongoing dev logs as the library progresses.

> Maintain a real chronological log in `docs/devlog/NNNN-YYYY-MM-DD-*.md`
> as the library is built; these dev-log *posts* are distilled from
> those, by the website session.

## 6. Open items for the building session to fill in here

- Final library API snapshots (quickstart code) once stable.
- The composite-icon resolution (closes beat 8).
- Real benchmarks / "it just works on the current patch" evidence.
- Final chosen title; confirm trademark clearance results.
