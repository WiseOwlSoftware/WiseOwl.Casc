# 0077 — FR-C26: `ParagonTooltipChrome.Divider` (`Center_Divider_White`)

2026-05-24 · CL-82 · branch `fr-c26-divider-field`

## Trigger

Optimizer round-3 on `casc-fr#38` (2026-05-24) consume-verified
CL-80/81's chrome surface + picked the divider candidate
structurally:

> *Owner-directive 2026-05-23 to approximate without graphic-picking
> from the owner. So the structural pattern-match on your end is the
> authoritative pick: Center_Divider_White (1559055) is the only
> white candidate of the four; the other three are dark-teal and
> would render invisible against the panel background. Locking in
> Center_Divider_White (1559055) for the divider slot.*

Asked for a typed `Divider` field on `ParagonTooltipChrome` so the
consumer doesn't hardcode the SNO. One-line CL.

## What ships

```csharp
public sealed record ParagonTooltipChrome(
    AssetRef BaseLayer,
    IReadOnlyDictionary<ParagonRarity, AssetRef> PanelByRarity,
    IReadOnlyDictionary<string, AssetRef> ItemSidePanelByRarityName,
    AssetRef OrnateFrame,
    AssetRef OrnateFrameLight,
    AssetRef DefaultFrame,
    AssetRef TextFrame,
    IReadOnlyDictionary<string, AssetRef> BannerByPlacement,
    AssetRef Divider,         // NEW (CL-82) — Center_Divider_White 1559055
    AssetRef SkillIconAtlas);
```

Resolved via `CoreToc.TryGetId(SnoGroup.UiStyle, "Center_Divider_White")`
through the same `TryGetTiledStyleRef` helper the other layers use.
Round-trips via `Catalog.TryGet<TiledStyleDefinition>`.

## FR-C26 closure status

Per the Optimizer's round-3 acceptance list:

| Step | Status |
|---|---|
| 1. CL-77 chrome surface base | ✅ |
| 2. CL-80 multi-layer composite + CL-81 skill-icon atlas | ✅ |
| 3. CL-82 typed Divider field (this CL) | ✅ |
| 4. Consumer visual-close iteration | consumer-owned, no CASC work |

After this CL lands, FR-C26 closes pending the consumer's visual
close.

## What stays NOT in this delivery

Per the Optimizer's round-3 — accepted as procedural fallback:

- **Bullet glyph** — Unicode `◆` (U+25C6 BLACK DIAMOND). The
  consumer's WPF tooltip view already wires this. Matches the
  engine's visual archetype.
- **Icon bezel** — deferred consumer-owned residual. No bezel
  art today; the per-node icon centers above the panel without
  an ornate ring.

The engine-controller code that binds these at runtime is
encrypted (per the Optimizer's note referencing
`project_engine-controller-code-encrypted`), so Phase C-style EXE
RE is **permanently impossible**. The procedural fallback is the
correct accepted answer — same shape as FR-C7 §6 residuals.

## Tests

The live `Acceptance_matrix_against_live_install` extends:

- `Divider.Sno == 1559055`, `Name == "Center_Divider_White"`,
  `Kind == AssetKind.TiledStyle`.
- `Catalog.TryGet<TiledStyleDefinition>(Divider, out var td)`
  succeeds (the 9-slice can be composed today).

126/126 tests green on `3.0.2.71886`.

## What's next

After CL-82 lands, the `awaiting:casc` queue should reduce to:

- **`#36` FR-C24** — Optimizer counter-roundered for the
  structural-8 (BaseRadius / RadiusUpgradeLevels / MaxLevel on
  glyph; DisplayFactor / AffectedAttributes / SkillTagSelector /
  Requirements on affix). That's the next CL — deep byte-layout
  RE on the `.gph` / `.gaf` payloads.
