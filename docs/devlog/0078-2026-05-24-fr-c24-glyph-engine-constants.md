# 0078 — FR-C24 slice 2a: glyph engine constants (BaseRadius / UpgradeLevels / MaxLevel)

2026-05-24 · CL-83 · branch `fr-c24-structural-8`

## Trigger

Optimizer's CL-79 consume-verify on `casc-fr#36` counter-roundered
for the 8 structural fields I deferred in slice 1. Splitting the
work: this CL closes the 3 glyph-side fields; the affix-side 4
(plus the `AffectedRarity` refinement) stays open as the next CL.

## What I went looking for

| Field | Expected source per Optimizer |
|---|---|
| `BaseRadius` | game `nStartingSize` (default 3) |
| `RadiusUpgradeLevels` | game `arSizeUpgradeLevels` (default [25, 50]) |
| `MaxLevel` | game `nMaxLevel` (default 150) |

## What I found

The `.gph` record carries **no per-glyph variance** on these
fields. Dumped `Rare_011_Intelligence_Side` (sno 1023194):

```
payload+0    : 1023194 (snoId)
payload+12   : 0x926E4F79 (a 4-byte hash)
payload+16   : 0xCFCBA153 (a 4-byte hash)
payload+20   : 1
payload+24   : -1 (sentinel)
payload+28   : 2082980 (probably a SNO ref)
payload+36..+79 : fUsableByClass (11 slots × 4 bytes, CL-18)
payload+80,84: 104, 12 — affix array descriptor (3 SNOs)
payload+104..+115: affix SNOs
```

Payload ends at +115. No 3 / 25 / 50 / 150 anywhere in there.
Cross-checked: searched for `nStartingSize` / `GlyphSettings` /
`ParagonGlyphData` SNOs — only `ParagonGlyphExperienceTable`
(sno 810212, GameBalance type 49) which is the 201-entry XP curve,
not the cap.

The Optimizer's Warlock-21 oracle (21 glyphs, every L1 row showing
Radius 3, every L25-49 row showing Radius 4, every L50+ row
showing Radius 5) confirms there's no per-glyph variance — these
ARE engine constants.

Per the FR-C26 finding (engine controller code is encrypted; Phase
C-style EXE RE is permanently impossible), the constants stay in
the library as the right answer — they apply universally to every
glyph the engine ships today, and consumer code can rely on them
without hard-coding.

## What ships

```csharp
public sealed class ParagonGlyphDefinition
{
    public int BaseRadius => 3;
    public IReadOnlyList<int> RadiusUpgradeLevels { get; }
        = ParagonGlyphEngineConstants.RadiusUpgradeLevels;
    public int MaxLevel => 150;
}

internal static class ParagonGlyphEngineConstants
{
    public static IReadOnlyList<int> RadiusUpgradeLevels { get; }
        = new int[] { 25, 50 };
}
```

**Forward-compat instance shape**: exposed as instance properties
(not static), so if a future season ships per-glyph variance the
property migrates from constant-return to record-decode without
consumer API churn. `[SuppressMessage(CA1822)]` on the constant
properties documents the rationale.

Pattern parallels `ParagonPowerBudget` (CL-68 budget-multiplier
intrinsics) — same shape: engine constants cross-validated against
owner-oracle readings, baked in the library with explicit Appendix
D re-verify trigger.

## What stays open on FR-C24

The affix-side 4 fields + the rarity refinement need byte-layout
RE on the `.gaf` record. Initial recon on
`DamageWhileHealthy_Intelligence_Side` (sno 1068542) shows:

```
payload+24  : 0    (AffectedRarity; was Maxroll-compact 1/2/3,
                   but 0 = "any" — refinement target)
payload+48  : 2    (eBonusOperation)
payload+64,68: 136,16 — DT_VARIABLEARRAY (4 entries × 4 = 16)
payload+72  : 0x16A2B4DF (GBID-shaped — SkillTagSelector candidate)
payload+76  : 1.325f  (Base — confirmed via spec)
payload+80  : 0.069f  (PerLevel — confirmed via spec)
payload+84  : 500.0f  (??? — looks like a float, position fits a
                       potential DisplayFactor but value 500 doesn't
                       match the consumer's assumption of 100; need
                       more affix dumps to disambiguate)
payload+120,124: 152,12 — second DT_VARIABLEARRAY (3 entries)
payload+152..+163: 3 GBIDs (one matches attr 288's gbidArr value
                            from CL-66; these may be the AffectedAttributes
                            or threshold-keyword refs)
```

Open questions worth chasing on the next slice:
- Confirm `DisplayFactor` offset by dumping affixes with known
  in-game magnitudes (Optimizer's Warlock-21 oracle gives the
  cross-validation point).
- Map the GBID-shaped fields at +72 / +152..+163 to either
  skill-tag scopes or attribute references.
- Locate the threshold-rule rows (Requirements list).

That's the next CL — keeping it focused on the affix half so the
RE stays tight.

## Tests

The live `Acceptance_matrix_against_live_install` extends:

- `ReadParagonGlyph(1023194).BaseRadius == 3`
- `ReadParagonGlyph(1023194).RadiusUpgradeLevels` ≡ `[25, 50]`
- `ReadParagonGlyph(1023194).MaxLevel == 150`

126/126 tests green on `3.0.2.71886`.
