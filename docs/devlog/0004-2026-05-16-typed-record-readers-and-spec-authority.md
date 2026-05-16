# 0004 — 2026-05-16 — Typed record readers + the spec-authority handoff

> Narrative source for the wiseowl.com session. Continues 0003. Two beats:
> a clean cross-session design convergence, and a library "graduating" to
> own the canonical spec.

## The convergence (process beat — worth keeping)

Instead of one side dictating, the consumer (ParagonOptimizer) wrote a
requirements spec; the casc session reviewed it and pushed back on three
points; the consumer accepted, refined one (NodeAttribute), and ceded spec
authority. Two short round-trips, zero disputes, then the owner lifted the
hold. The article angle: the same precise, acceptance-bearing,
code-grounded loop that drove FR-1…FR-16 also works for *design*, not just
bug reports. No speculative wishlists; every ask had a §-reference and an
expected byte value.

Three pushbacks that stuck:
1. **No evaluator, ever.** The consumer asked whether a generic
   formula evaluator should move into the library (intrinsics injected).
   The casc answer was firmer than their own recommendation: a
   container/record-*format* library has no business shipping an
   arithmetic engine — it'd be identical for any domain and dilutes
   cohesion. Library exposes formula *text* + name/GBID indices; the
   evaluator and the 6 season-volatile calibrated intrinsics stay
   permanently with the consumer. Accepted as preferred.
2. **`*Definition` naming.** A `ParagonBoard` *class* collides with the
   `SnoGroup.ParagonBoard` enum member. Use the game's own struct names
   (`ParagonBoardDefinition`, …) — which also matches our existing
   `TextureDefinition`. Accepted.
3. **Two raw param fields.** The 88-byte `AttributeSpecifier` has *two*
   distinct ints (`nParam@+4` and a different value `@+12`). Expose both
   raw (`NParam`, `ParamPlus12`) — making the consumer derive `+12` would
   reintroduce the very byte-parsing the migration deletes. Accepted; the
   principle ("expose verified raw fields so there is zero consumer
   re-parse") is the whole point of the boundary.

## What shipped (B1–B6)

Clean-room typed readers, each `static Parse(ReadOnlySpan<byte>)` +
`Diablo4Storage.Read*` facade, raw fields only:
`ParagonBoardDefinition` (cells row-major, `Width*Width`, `0xFFFFFFFF`→
null), `ParagonNodeDefinition` (+`NodeAttribute` with both param fields,
`RarityOverride` raw int + `ParagonRarity` convenience, `SnoPassivePower`,
`hIcon/hIconMask`), `ParagonGlyphDefinition` (≤3 affix SNOs, bounds-safe
for placeholder records — a real edge that bit the live test and got
hardened), `ParagonGlyphAffixDefinition`, and `AttributeFormulaTable`
(the GameBalance polymorphic walk; `eGameBalanceType==22` enforced;
`ByName`/`TryGetFormulaText` = `arRanges[0]` text; `TryGetNameByGbid`
keyed on `GbidHash(szName)` since the in-record gbid is null). Plus B6
`Diablo4Storage.TryGetIconFrame` (lazy handle→atlas/frame index — the
first-party `hIconMask == TexFrame.ImageHandle` link).

The §7 acceptance matrix passes **verbatim** against live D4
3.0.2.71886: board 2458674 → W21/441; node 678776 → sig 0xDEADBEEF,
Common; GameBalance 201912 → **1038 entries**,
`ParagonNodeCoreStat_Normal`→"5", `_Magic`→"7",
`GbidHash`==`0x42C16A1B`. 27 tests pass, 1 unrelated honest skip, 0
build warnings. CI-safe synthetic blob builders prove every walk without
game bytes.

## The bigger beat — the library graduated to own the spec

Because all D4 access/format code now lives in WiseOwl.Casc, spec
ownership followed code ownership. `casc-format.md` was re-scoped from
"CASC transport" to **the single canonical CASC + Diablo IV byte-format
reference** (§§1–9 transport/StringList, new §§10–14 the D4 SNO/record/
texture layer, §15 a provenance map). The original direction —
"clean-room *from* `e:\Paragon` d4-binary-formats §3–§8.15" — **inverted**:
that file is now frozen for layouts and demoted to project history /
article source; the verified truth is re-derived and recorded here under
the `CL-*` log. The merge was inventory-driven with an auditable
provenance table so nothing dropped, and the policy carve-out (the 6
intrinsic *values*, scoring, relight, JSON schema) is referenced, never
absorbed.

That "the library earned the spec" is the strongest architecture beat so
far: a clean-room reimplementation that started by reading someone else's
reverse-engineering notes is now itself the upstream of record.

## Next

Library scope is FROZEN at "B1–B6 + existing" — nothing further for the
eliminate-D4Extract goal. The consumer now migrates D4Extract's parsers
onto these readers, deletes the project, folds residual policy into a
tiny app build generator, banners/freezes its old format doc, and
repoints its requirements reference here. Typed Item/Affix/Power/Class
readers stay deferred (C6) until that RE exists.
