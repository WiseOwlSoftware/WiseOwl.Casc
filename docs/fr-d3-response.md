# FR-D3 — response: first-party glyph→class membership

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Re:** `fr-d3` (glyph→class membership).
> **Status: DELIVERED — answer (B): API gap; minimal typed surface
> shipped.** Authoritative byte spec: `casc-diablo4-format.md` §7.3;
> Appendix A `CL-18`. Keyed to the shared class id (FR-D1/D2).

## 1. Verdict — (B)

The glyph record **does** carry the class restriction (a per-class
boolean fixed array `fUsableByClass` at payload `+0x24`), but its slot
ordering is undocumented D4 data — RE, our side of the boundary. So:
minimal typed surface, with the ordering decoded **once, library-side**
(durable opaque-id principle, spec Appendix C). The consumer guesses
nothing; the Maxroll `classFilter` is **not** used.

## 2. What shipped (public API)

```csharp
ParagonGlyphDefinition {
    IReadOnlyList<int> UsableByClassSnoIds;  // PlayerClass SNO ids — the shared key
    // … existing SnoId / AffixSnoIds …
}
ParagonGlyphDefinition Diablo4Storage.ReadParagonGlyph(int id);  // populates the above
```

`UsableByClassSnoIds` are **`SnoGroup.PlayerClass` SNO ids** — exactly
`CharacterClass.SnoId` (FR-D2) and `ParagonBoardDefinition.ClassSnoId`
(FR-D1). One shared class key across D1/D2/D3. Byte-only
`ParagonGlyphDefinition.Parse(blob)` leaves it empty (the ordering
needs `CoreToc`); malformed/placeholder records get an **empty** set
(honest sentinel — never a silently-wrong class). Raw decoded values
only, no policy.

## 3. The decoded convention (for your audit)

- Field: `fUsableByClass` — `DT_FIXEDARRAY[DT_INT]` at glyph payload
  `+0x24`, one int32 boolean per class slot (non-zero ⇒ usable).
- **Slot index = the class's eClass rank.** Each class's PlayerClass
  record carries an `eClass` ordinal at payload `+16`. On build
  `3.0.2.71886` these are sparse — Sorcerer 0, Barbarian 1, Rogue 3,
  Druid 5, Necromancer 6, Spiritborn 7, Paladin 9, Warlock 10 — and
  **rank-compact to 0..7** when the §6.5 roster is sorted ascending by
  eClass. The slot is that rank. Computed live from the roster, never
  hardcoded; future classes slot in automatically.

  | Rank/slot | Class | PlayerClass SNO |
  |---|---|---|
  | 0 | Sorcerer | 131965 |
  | 1 | Barbarian | 169776 |
  | 2 | Rogue | 199275 |
  | 3 | Druid | 131966 |
  | 4 | Necromancer | 199277 |
  | 5 | Spiritborn | 1206232 |
  | 6 | Paladin | 2079084 |
  | 7 | Warlock | **2207749** |

- **Over-determined** (three independent anchors agree): the
  explicitly-named `*_Necro` glyphs set exactly rank 4 (= Necromancer);
  your empirically-validated Warlock = index 7 (= rank 7); and
  Sorcerer = rank 0 cross-checks on an Intelligence glyph. The
  eClass-rank derivation is proof by corroboration, not inference.
- Well-formed guard: a real glyph has the affix array `dataOffset` ==
  104 at payload `+0x50`. The `Axe Bad Data` junk SNO (732443, a
  120-byte placeholder) fails this and otherwise reads a spurious
  all-8 pattern → gated to empty.

## 4. Acceptance (your §4 probes, vs live `3.0.2.71886`)

| Probe | Result |
|---|---|
| Warlock-usable glyph (`Rare_111_Willpower_Main` 2529463) | `UsableByClassSnoIds` includes **2207749** |
| Sorcerer-only glyph (`Rare_001_Intelligence_Main` 1023184) | `{131965}` — **excludes 2207749** |
| `*_Necro` glyph (1331846) — independent anchor | `{199277}` (Necromancer) |
| Multi-class glyph (`Rare_063_Intelligence_Side` 1029487) | `{131966, 1206232, 2079084}` (Druid/Spiritborn/Paladin) |
| Junk `Axe Bad Data` (732443) | **empty** (honest sentinel) |
| byte-only `Parse(blob)` | empty (needs CoreTOC) |

All resolved against the §6.5 roster (no hardcoded map). Recorded with
`CL-18` + the rank table in §7.3; asserted by
`ReadParagonGlyph_resolves_usable_by_class` (passes vs live build; full
suite green, 0 warnings).

## 5. How to integrate (retire the last non-first-party class link)

```csharp
var g = d4.ReadParagonGlyph(glyphSnoId);
// g.UsableByClassSnoIds → PlayerClass SNO ids; join directly to
// CharacterClass.SnoId (FR-D2) / ParagonBoardDefinition.ClassSnoId (FR-D1).
```

Delete `GlyphDatasetBuilder`'s Maxroll `classFilter` read,
`ClassByFilterIndex`, and the `ParagonClass` enum. Key
`ParagonGlyph.AllowedClasses` / `GlyphsForClass` off the shared class
SNO id. The `ParagonClass`-enum → first-party-class-key migration that
was blocked on this is now unblocked, end-to-end first-party. An empty
`UsableByClassSnoIds` is the consumer's "unknown/none" case (own the
fallback) — never a wrong class.

## 6. Boundary & what is owed

Library = the decoded field + ordering + raw PlayerClass SNO ids. No
policy/imaging. This is only the **class-membership slice** of glyph RE
— affix/threshold/scalar decode remains the deferred BACKLOG item, not
touched here. Nothing further owed unless the gated NuGet release
ships, or a seasonal build changes `.build.info` Build Key — then
re-verify (Appendix D); the eClass-rank is recomputed live each run, so
a new class slots in without code changes, and the well-formed guard
keeps junk honest. Same amend-until-next-publish contract as FR-C7 §7.

## 7. Round log

- **Round-1 (open, 2026-05-17):** consumer raises the recipe-or-gap
  question; the `ParagonClass`-enum → first-party-key migration is
  blocked on this by owner decision.
- **Round-1 (delivered, 2026-05-17):** library — answer (B). Decoded
  `fUsableByClass` (glyph payload `+0x24`) indexed by eClass rank
  (PlayerClass payload `+16`, ranked over the §6.5 roster);
  over-determined by the `_Necro` and Warlock=7 anchors. Shipped
  `ParagonGlyphDefinition.UsableByClassSnoIds` via `ReadParagonGlyph`;
  spec §7.3 + CL-18; integration test asserts the verbatim probes.
  Consumer retires the Maxroll `classFilter` + `ClassByFilterIndex` +
  `ParagonClass` enum and keys off the shared class SNO id.
