# 0003 — 2026-05-16 — Reversing the StringList container (FR-13)

> Narrative source for the wiseowl.com session. Continues 0002. This is a
> clean "we became the expert on an undocumented format" beat.

## Brief

FR-13 (localized strings) had been *accepted but deferred* — it needed its
own reverse-engineering and there was no spec for the
`StringList-Text-<locale>` container. The owner said: become the expert,
use all resources, spin it up. So we did.

## Method (worth keeping for the article — it's the repeatable recipe)

1. **Exhaust local refs.** Confirmed only the cheap facts: SNO group 42,
   `.stl`. The upstream spec never reversed it ("kept from Maxroll until
   the string table is wired").
2. **Web research for prior art.** Found `alkhdaniel/diablo-4-string-parser`
   — a dedicated standalone `.stl`→JSON parser. Fetched its source: header
   48 bytes, entries 40 bytes (`keyOffset/keyLen` then `valOffset/valLen`),
   offsets `+16` (= the 16-byte SNO header). That gave the *standalone*
   layout — but not how the game ships strings (the consolidated bundle).
3. **Empirically measure, don't theorize.** (The CL-4 lesson from devlog
   0002, applied deliberately.) Probed group 42 against the live install:
   59,515 StringList SNOs with domain names (`AttributeDescriptions`,
   `Bnet_Chat`, `Lore`…) — but `Base\Meta\<id>` does **not** resolve for
   them. Same asymmetry as texture meta.
4. **Discover the real path names.** Added a diagnostic path-capture to the
   TVFS walk and filtered for StringList. Out fell the exact tree:
   `base/StringList-Text-<locale>.dat` (consolidated),
   `…-<locale>-0x<16hex>.dat` (shards), `…-Global.dat`, across 14 locales.
5. **Reverse the bundle.** Dumped `base/StringList-Text-enUS.dat`: magic
   **`0x44CF00F5`** — the *same combined-meta container as
   `Texture-Base-Global.dat`*. `count=58286`, index `{i32 sno,u32 size}`,
   `idx[0]={4080,98569}` (= `AttributeDescriptions`).
6. **Crack the per-entry placement by brute-decode.** The texture
   convention (`descStart=alignUp8(prevEnd)+8`, `snoId@descStart`) gave
   all-zero bodies. Brute-forced `(payloadBase, stringBase)` offsets,
   printing decoded text, until readable strings appeared: the answer is
   **`B = alignUp8(prevEnd)`, no `+8`, no SNO id in the body** (it's
   positional, from the index). Keys `Druid_BloodHowl_GrantsStealth`,
   values `{c_important}Blood Howl{/c} Grants Stealth for +[{VALUE}|1…]`.
7. **Validate bundle-wide.** Walked all 58,286 tables: 0 out-of-bounds,
   175,014 strings, `finalPrevEnd = 20,207,724` vs `blobLen = 20,207,728`
   (4-byte pad) — it lands *exactly* at EOF. Spot-checked first / middle /
   last tables: all decode (`Bnet_Chat/ChatLink_WhisperedTo` →
   `"{s1} whispers: {s2}"`; last table's `AffixName` →
   `"{c_white}Dungeon Delve{/c}"`).

## The finding (the article's technical payoff)

D4 ships localized text in **per-locale `0x44CF00F5` bundles** — the very
same container as textures, *reused*. The only differences: no `+8`
per-entry offset and no per-body SNO id (positional from the index). One
container family, two payload shapes. That "Blizzard reuses one container
across subsystems" is a clean, quotable insight — and exactly why a
*general* CASC library that models the container once is the right design.

A discipline beat too: the texture `+8`/`snoId` convention *looked*
applicable and produced plausible-shaped (but all-zero) output. Only
decoding to **readable text** — not "the numbers look sane" — proved the
layout. Same lesson as the encoding-offset-18 bug (devlog 0001): validate
on meaning, not on shape.

## Shipped

- `StringListCatalog` (parser, clean-room) + `StringListTable`;
  `Diablo4Storage.GetStrings(locale="enUS")` (cached per locale) +
  `TryGetString(tableSno,label)` / `TryGetString(label)`.
- Full definitive spec: `casc-format.md §9` + correction `CL-7`.
- CI-safe synthetic test (hand-built `0x44CF00F5` StringList bundle) +
  live test (`d4.TryGetString(4087,"ChatLink_WhisperedTo")` ==
  `"{s1} whispers: {s2}"`, table 4080 = 646 entries).
- The throwaway RE probe was deleted; the genuinely-useful
  `CascStorage.DiagnosticPaths` / `TvfsStats.CapturePathIf` it needed were
  kept as documented diagnostic API.
- 20 tests pass, 1 unrelated honest skip, 0 build warnings. FR-13 moved
  from "deferred" to **done & proven**; nothing in the round-2 backlog
  remains deferred.

## Credits

`alkhdaniel/diablo-4-string-parser` (standalone `.stl` layout cross-check);
`Dakota628/d4parse`, `DiabloTools/d4data` (ecosystem). No third-party code
incorporated — clean-room from the measured bytes + the public references.
