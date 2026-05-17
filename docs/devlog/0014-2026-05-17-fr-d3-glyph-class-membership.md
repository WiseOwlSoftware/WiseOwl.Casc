# 0014 — FR-D3: first-party glyph→class membership

*2026-05-17*

The last non-first-party class link. FR-D1 made board→class first-party,
FR-D2 the class roster; the consumer still derived glyph→class from a
Maxroll `classFilter` 8-bool array via a provisional bit-index→class
guess (only Warlock=idx7 verified). Answered **(B)**.

## Recon

`dump 111` showed the `ParagonGlyphDefinition` record (132 B; the
upstream §5 size-96 layout is stale — formatHash changed to
`0x7C86053A`). A new recon command `glyphclass` dumped, for all 161
group-111 glyphs, the int32 region at abs 0x34 (payload `+0x24`). It is
a per-class boolean fixed array: single-bit glyphs cluster into slots
0–7, plus a few multi-bit (multi-class) and one all-zero (honest none).

The slot→class ordering had to come from D4 data, not a guess. Dumping
all eight PlayerClass records revealed an `eClass` ordinal at payload
`+16`: Sorcerer 0, Barbarian 1, Rogue 3, Druid 5, Necromancer 6,
Spiritborn 7, Paladin 9, Warlock 10 — sparse, but **rank-compact to
0..7**. The glyph slot index = that eClass rank. This is
**over-determined**: the explicitly-named `*_Necro` glyphs set exactly
rank 4 (= Necromancer), and the consumer's empirically-validated
Warlock = index 7 equals rank 7 — two independent anchors confirming
the eClass-rank derivation, with Sorcerer = rank 0 cross-checking. No
Maxroll, no hardcoded order, no name parsing in the shipped path.

The `Axe Bad Data` junk SNO (732443, a 120-byte placeholder) reads a
spurious all-8 pattern; gated by a structural well-formed check (affix
`dataOffset` at payload `+0x50` == 104) → empty set, never a
silently-wrong class.

## Delivery

- `ParagonGlyphDefinition.UsableByClassSnoIds` (PlayerClass SNO ids —
  the shared key with FR-D1/D2), populated by
  `Diablo4Storage.ReadParagonGlyph(int)`; byte-only `Parse(blob)`
  leaves it empty. Internal `PlayerClassRanks()` ranks the §6.5 roster
  by live eClass; cached.
- Spec §7.3 (field + rank table + guard) + Appendix A CL-18; durable
  opaque-id principle already in Appendix C (D1).
- Integration test `ReadParagonGlyph_resolves_usable_by_class` asserts
  the verbatim probes vs live `3.0.2.71886`; full suite green, 0
  warnings; API docs regenerated. Report `docs/fr-d3-response.md`.
- `SnoScan glyphclass` recon-only.

Scope: the class-membership slice only — affix/threshold/scalar glyph
RE remains the deferred BACKLOG item. This unblocks the consumer's
`ParagonClass`-enum → first-party-class-key migration, end-to-end.
Amend-until-next-publish, like FR-C7.
