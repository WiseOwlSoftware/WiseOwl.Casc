# 0072 — FR-C23 Option A: `Catalog.GetParagonTooltipChrome()`

2026-05-23 · CL-77 · branch `fr-c23-tooltip-chrome`

## Trigger

Optimizer pinned the FR-C23 scope after my #35 recon comment:
**Option A — chrome-only.** The full layout / per-state binding split
off as FR-C26 (`casc-fr#38`), the FR-C7-shaped multi-CL controller
RE thread. This CL ships the small, concrete half.

## What ships

```csharp
public sealed record ParagonTooltipChrome(
    IReadOnlyDictionary<ParagonRarity, AssetRef> PanelByRarity,
    IReadOnlyDictionary<string, AssetRef> ItemSidePanelByRarityName);

// On d4.Catalog:
public ParagonTooltipChrome GetParagonTooltipChrome();
```

`PanelByRarity` is the paragon consumer's primary map — keyed by
`ParagonRarity`, populated on every install:

```
Common    → TooltipBackgroundRarity_Common     (602975)
Magic     → TooltipBackgroundRarity_Magic      (602972)
Rare      → TooltipBackgroundRarity_Rare       (602274)
Legendary → TooltipBackgroundRarity_Legendary  (602942)
```

Each `AssetRef` is an `AssetKind.TiledStyle` ref the consumer
9-slice-composites via the existing
`Catalog.TryGet<TiledStyleDefinition>` path — same pattern as every
other `TiledStyle` surface (CL-44 / CL-62 / CL-64 / CL-66).

`ItemSidePanelByRarityName` is future-proofing for any item-tooltip
work the consumer takes on later — same family of TiledStyles, but
keyed by the engine string token (Unique / Set / Mythic / Season)
because `ParagonRarity` doesn't have those members.

## Decode-free + cached

The dispatch only walks `CoreToc.TryGetId(SnoGroup.UiStyle, "...")`
for each of the eight names — no `TiledStyleDefinition` parse — and
caches the resulting record under a double-check lock for the
storage lifetime. Repeat `GetParagonTooltipChrome()` returns the
same reference (Optimizer hot-path convention; matches the
`GetNodeInfo` / `GetBoardNodes` cache-identity pattern from CL-69 /
CL-70).

## Tests

The live `Acceptance_matrix_against_live_install` extends with the
chrome-inventory assertions:

- All four paragon-rarity entries present with the right SNO ids
  + names.
- Each entry's `AssetRef` round-trips through
  `Catalog.TryGet<TiledStyleDefinition>` — i.e. the consumer can
  decode the 9-slice today via the surfaced ref.
- Item-side dictionary has all four entries
  (Unique / Set / Mythic / Season) with name prefixes.
- Cache identity holds (`Assert.Same` on the repeat call).

104/104 tests green on build `3.0.2.71886` (no count delta — the
new assertions extend the existing acceptance test).

## Open chrome pieces — explicitly deferred

The bullet glyph (small white diamond before each stat row), the
divider line (with diamond end-caps), and the icon bezel (the
diamond ornate ring around the icon) all show up in the Optimizer's
tooltip captures but aren't trivially in the data. The widgets that
SHOULD bind them (`AttributeBullet`, `CoreStatDivider`,
`TitleDecoLeft`/`TitleDecoRight`) live in the ParagonBoard scene
(657304) as bare template stubs — `bActive=unset`,
`[L=0,R=0,T=0,B=0,W=0,H=0]`, no image, no children. The engine
positions and binds them at runtime via compiled controller code
(the `ParagonBoardUI` pattern FR-C7 ran into).

These ship on FR-C26 (`casc-fr#38`) — the controller-RE thread.

## Next

- **CL-78** (FR-C25): `AttributeDescriptions` (sno 4080)
  localization — id → localized name. Retires the CL-76 basic-four
  hack and unblocks FR-C24's affix descriptions.
- **CL-79** (FR-C24): complete glyph + glyph-affix projection
  (`LocalizedTitle`, `Rarity`, `BaseRadius`, `RadiusUpgradeLevels`,
  `MaxLevel`, `Description`, `DisplayFactor`).
- **FR-C26**: multi-CL layout RE.
