# 0084 — FR-C29: character-stat derivation formulae — data-mine across all four phases

**Date:** 2026-07-15
**FR:** casc-fr#41 (FR-C29 — per-class character-stat derivation formulae; Character Sheet projection)
**Outcome:** research finding, no code shipped. Phase 1 (the unlock) is **engine-coded**; turn → `needs:owner`.
**Recon tooling:** `build/SnoScan` gained dataset-wide `f32grep` + `hashgrep`.

## The ask

The consumer's build planner hosts ~25 Character-Sheet stats as oracle-hardcoded
values for one captured combo (Warlock@70 Torment VI naked). It needs the engine's
derivation formulae to compute them for any (class, level, Torment, paragon) combo.
Owner directive (2026-05-24): search **all** non-identified data sources, starting
with the flagged leads; short-circuit the blind float-grep with a **name-hash-grep**
(DJB2 the in-game stat names → grep unidentified SNOs). If the mine dries, the honest
engine-coded boundary (owner's pre-accepted **option 4**) is acceptable, and the FR
converts to "consumer hard-codes a per-class table from owner observation."

## Method

Two new recon commands (dev-only, `build/SnoScan`, not in the shipped solution):

- `f32grep <value> [tolpct] [gid]` — scan a whole group's Meta for an IEEE-754 float
  within a relative tolerance; print SNO + offset + neighbouring floats (so a
  coefficient adjacent to a name-hash key surfaces).
- `hashgrep <gid> <name...>` — hash each candidate name (fieldHash / typeHash / gbid),
  grep the group for any 32-bit-aligned match, report the adjacent float.

Anchor coefficients (from the FR's Zanthara oracle, Warlock@70 Str76/Int77/Will79/Dex76):
Armor=Str×2.0, AllResist=Int×0.4, SkillDmg=Will×0.00125, Healing≈Will×0.000354,
Dodge≈Dex×0.0000658, Crit≈Str×0.0000263, ResGen≈Int×0.000052 — searched in **both**
fraction and percent representations.

## Phase 1 — per-class core→stat coefficients: ENGINE-CODED

Nine sources, zero hits on the distinctive coefficients (0.4, 0.125, 0.00125) anywhere:

| Source | SNO / group | Result |
|---|---|---|
| PlayerClass (Warlock + all) | g74, 9 SNOs | f32grep + hashgrep: **0** |
| Hero (Warlock + all) | g39, 21 SNOs | f32grep + hashgrep: **0** |
| AttributeFormulas | 201912 (g20) | gear/affix/paragon magnitude only (FR-C21 territory) |
| AttributeDescriptions / HeroDetails | 4080 / 4123 (g42) | tooltip templates — see clincher |
| SimpleScalarFormulas | 2536879 (g20) | reroll/glyph-cost formula table; anchors absent |
| LevelScaling | 206158 (g20) | anon per-level float curves; anchors absent as raw floats |
| DataAttributes | 1907204 (g20) | per-power/item designer subset (Socketable_*/Witch_*/class-power); confirms CL-88 — NOT a stat registry |
| DamageMitigation | 1846727 (g20) | empty (8 zero bytes) |
| GameBalance broad | g20 (217) + g49 (2086) | full f32grep 0.4/0.125/0.00125: no coefficient home |

**The clincher — the tooltip is engine-computed.** `HeroDetails [TipStrength]` (sno 4123):

```
{c_label}Strength:{/c} {c_number}{s1}{/c} … {icon:bullet} Increases Armor by {c_number}{s3}{/c}
```

The coefficient is **not in the template** — `{s1}` (the Str value) and `{s3}` (the
computed Armor) are runtime substitutions. The engine multiplies; the data only names
the label. Every core-stat tip is label-only (`TipCoreStatCriticalStrike` = "Increases
Critical Strike Chance by"). The conversion is compiled into the engine
(consistent with `project_engine-controller-code-encrypted`). → Option 4 for Phase 1.

### Rescue path (universal vs per-class)

The "varies by class" premise rests on a single class's oracle. Real D4 primary-attribute
bonuses are plausibly **universal**, with the only per-class datum being *which core is
the class's primary stat* (Warlock primary = Willpower → Will grants Skill Damage; a
Strength-primary class would grant Skill Damage from Str at the same rate). If so, Phase 1
is library-deliverable: bake universal coefficients as **validated engine constants**
(`project_engine-constants-pattern`, precedent CL-68/CL-83) + a per-class primary-stat
lookup (decodable from PlayerClass). Decisive test = one non-Warlock class's core-stat
tooltips (owner oracle). Match → universal → ship as constants; differ → per-class → option 4.

## Phases 2–4 (located; each needs an oracle to finish)

- **Phase 2** — base Life 1526 is **not** a raw float; computed (`base × level-curve`).
  `LevelScaling` (206158) is the player-side per-level curve table (~150 anon rows, no
  field names). Decodable only against per-curve anchors (base Life at ~3 levels × 2 classes).
- **Phase 3** — `DamageMitigation` (1846727) empty → mitigation formula engine-coded (option 4).
- **Phase 4** — no discrete Torment-tier table. `DifficultyTiers` (1973217) is a
  per-**monster-level** curve: 150 records, 128-byte stride, `record[i]` at `0x68+i*128`,
  level = `i+1` (1..150). Row = `level | monsterHP | monsterDmg | 7/9 | 4 | 4 | 3 |
  s | s | perLevelXpValue | perLevelGoldValue | …`. **The FR's "Torment VI XpBonus = 8.0"
  coincidentally equals this table's level-40 XP value** (level 70 = 11.0; scales +0.1/level)
  — it is a per-level XP curve, **not** a tier bonus. Discipline win
  (`feedback_validate-decode-on-structured-content`): digging past the coincidental match
  avoided a false "Torment table" claim. Discrete Torment I–VI multipliers appear
  engine/UI-config-coded; if the owner supplies exact XP/Gold for 2–3 named tiers I can
  test whether they reduce out of this table.

## Net

FR-C29 is dominantly engine-coded (Phases 1/3, Phase-4 discrete tiers). Two paths could
still yield library deliveries — the universal-coefficient rescue (Phase 1) and the
LevelScaling decode (Phase 2) — both gated on a small owner oracle. No code shipped:
the honest boundary was reached before writing any unverifiable field name
(`feedback_re-all-fields` / `feedback_validate-decode-on-structured-content`). `casc-fr#41`
→ `needs:owner`.
