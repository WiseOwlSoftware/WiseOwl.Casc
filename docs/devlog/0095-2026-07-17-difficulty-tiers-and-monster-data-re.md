# 0095 — DifficultyTiers curve + how far monster-data RE reaches (FR-C34)

**Date:** 2026-07-17
**Work item:** casc-fr#50 (FR-C34) + owner ask ("see how much RE can uncover about monster tables/data")
**CL:** CL-101 · `Diablo4Storage.ReadDifficultyTiers` + `DifficultyTiersTable` + §8.2 reconciliation

The Optimizer asked to type `DifficultyTiers` (1973217) — the per-**monster-level**
scaling curve, the monster analogue of the player-side `LevelScaling.hpScalar`
shipped in CL-99 — and my own devlog 0084 had already partially decoded it. The
owner added: *see how much RE can uncover about monster tables/data* generally.

## The table, byte-verified

VLA @ payload `+0x50` → `dataOffset=88, byteSize=19200` = **150 rows × 128 B**,
row index = level−1. Verified against the live blob (not just the Optimizer's
recon):

| col | L1 | L40 | L70 | reading | confidence |
|---|---|---|---|---|---|
| `+0` | 1 | 40 | 70 | level | verified |
| `+4` | 1.0 | 909.8 | 101,051 | monster HP × | **inferred** |
| `+8` | 1.0 | 16.06 | 64.44 | monster dmg × | **inferred** |
| `+36` | 0 | **8.0** | **11.0** | per-level XP | **verified anchor** |
| `+40` | 0 | 2.0 | 2.75 | per-level gold | candidate |

`+36` reproduces the game's XP curve exactly — the *independent* lock that fixes
the stride/offset, so every column is correctly located. But `+4`/`+8` are
**inferred**: they're clearly per-level multipliers (×1.0 at L1), but "monster
HP/damage" can't be owner-validated — D4 shows monster health as a bar, no
number (AC-3). Shipped the 5 typed columns + the full raw 32-column row
(`DifficultyTierRow.Columns`) so the unlabeled reward/scaling coefficients aren't
hidden, without asserting names I can't verify.

## §8.2 was wrong: two curves, not one

§8.2 (CL-99) said "one `hpScalar` column serves both populations." This table
disproves it: at L70 the player `hpScalar` = ×30.5 but this monster-HP column =
**×101,051** — a ~3,300× different, far steeper curve. Monsters scale off *this*
table, **not** `LevelScaling` rows 71–200. Corrected §8.2; a test locks it
(`MonsterHpScalar(70) > 1000 × hpScalar(70)`).

## How far does monster-data RE reach?

Using the owner's anchor (a goatman sorcerer is a monster he fights):

- **Monsters are Actor SNOs (group 1, ~61k)** — named
  `<family>_<role>_<element>_<context>`: `Goatman_sorcerer_phys` (540330),
  `BSK_Goatman_sorcerer_cold`, `…_unique_DGN_Naha_…`, spawners, minibosses.
- **The `.acr` record is identity + appearance/anim, not stats.** Its header
  references resolve to a base *appearance* actor (`Goatman_shaman`, group 9) and
  an *anim tree* (`Monster_Strafe_AnimTree`, group 67) — **no base-HP field**.
- **Monster GameBalance (group 20) tables that DO exist:** `MonsterLevelCurves`
  (six named `Raid_Tier_0..5` scaling curves for raid content), `MonsterNames`
  (a 385 KB name-affix registry — `BloodSeekerBarbPrefix…`,
  `X1_Raid_Special_Add_Suffix`), `MonsterAffixCategories`, `MonsterTags`.
- **The per-monster BASE HP is not a flat readable field.** It's engine-assembled
  from base attributes × this `DifficultyTiers` curve × difficulty/raid-tier —
  the same engine boundary as the player base `Hitpoints_Max = 50` (§8.2, fitted
  not located). The per-level *scaling* is now typed; the per-monster *base* is
  not in the data as a number.

So: the scaling curve is fully typeable and shipped; the monster taxonomy
(actors + name/tag/affix tables) is readable and typeable on request; the base
HP is an engine constant per family, not a table. Honest reach, honest boundary.

Recon: `SnoScan strdump` (new — generic per-SNO printable-string dump).
