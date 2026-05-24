# 0075 — FR-C26 (chrome side): multi-layer tooltip composite

2026-05-23 · CL-80 · branch `fr-c26-tooltip-chrome-composite`

## Trigger

Owner asked: *"Can we determine tooltip background and frame
graphics from the game oracles? Are there tooltip recipes we've
not yet decoded?"*

The CL-77 chrome surface answered "per-rarity panels — yes, here
they are." Both questions then push further: is the per-rarity
panel the WHOLE chrome, or just one layer? And which authored
recipes did I miss?

## What I found

The tooltip is a **multi-layer composite**, not a single
TiledStyle. The engine stacks three layers:

```
┌─────────────────────────────────────────────────┐
│ TooltipFrame (602013) - ornate spiky border    │  ← layer 3
│   • 8 corner+edge pieces from atlas 369421     │
│   • centre handle 0xD756FD92 from atlas 141461 │
│   ┌─────────────────────────────────────────┐  │
│   │ TooltipBackgroundRarity_<R> (CL-77)    │  │  ← layer 2 (per-rarity)
│   │   • 9 frames from per-rarity atlas     │  │
│   │   ┌─────────────────────────────────┐  │  │
│   │   │ TooltipBaseBackground (602266) │  │  │  ← layer 1 (universal)
│   │   │   • 9 frames from atlas 602265 │  │  │
│   │   └─────────────────────────────────┘  │  │
│   └─────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

The Optimizer's "red+blue spiky panel border" description on the
Common-rarity Gate screenshot comes from the per-rarity overlay's
spike colors — the universal `TooltipFrame` is dark teal across
all rarities. Verified by extracting the 8 perimeter atlas frames
from `2DUITiled_TooltipFrame` (369421) via
`build/AtlasExport frame`:

| Handle | Position | Verified visually |
|---|---|---|
| `0xDD5CE706` | top-left corner | ornate spikes outward (up+left) |
| `0xF72E00F3` | top-right corner | ornate spikes outward (up+right) |
| `0x5813EB55` | bottom-left corner | ornate spikes outward (down+left) |
| `0x5B001DA8` | bottom-right corner | ornate spikes outward (down+right) |
| `0xE00A64E8` | top edge | horizontal spikes up |
| `0x69266E4A` | bottom edge | horizontal spikes down |
| `0xE1527380` | left edge | vertical spikes left |
| `0x0C0FAB33` | right edge | vertical spikes right |
| `0xD756FD92` | center (in atlas 141461) | transparent base |

PNG dumps live in `artifacts/fr-c26-tooltip-recon/` for posterity.

## Recipes I missed in CL-77

| TiledStyle SNO | Name | Role |
|---|---|---|
| 602266 | `TooltipBaseBackground` | Universal dark backdrop (layer 1) |
| 602013 | `TooltipFrame` | Ornate spiky border (layer 3, default) |
| 603057 | `TooltipFrameLight` | Light variant of layer 3 |
| 478952 | `DefaultTooltip` | Compact 28×28-corner variant |
| 478948 | `TextTooltip` | Text-only compact variant |
| 734179 | `TooltipBanner_Map` | Non-tooltip banner placement |
| 967402 | `TooltipBanner_Town` | Non-tooltip banner placement |

The first three are the meaningful additions for paragon node
tooltips. CL-80 surfaces all seven on `ParagonTooltipChrome`.

## Surface (additions to CL-77)

```csharp
public sealed record ParagonTooltipChrome(
    AssetRef BaseLayer,                                    // NEW (CL-80)
    IReadOnlyDictionary<ParagonRarity, AssetRef> PanelByRarity,
    IReadOnlyDictionary<string, AssetRef> ItemSidePanelByRarityName,
    AssetRef OrnateFrame,                                  // NEW
    AssetRef OrnateFrameLight,                             // NEW
    AssetRef DefaultFrame,                                 // NEW
    AssetRef TextFrame,                                    // NEW
    IReadOnlyDictionary<string, AssetRef> BannerByPlacement);  // NEW
```

Each `AssetRef` decodes through the existing
`Catalog.TryGet<TiledStyleDefinition>` path — no new decode work
needed at the consumer.

## Tests

126/126 tests green on `3.0.2.71886` (no count delta — the new
assertions extend the existing
`Acceptance_matrix_against_live_install`). The CL-80 block
asserts each new layer:
- Right SNO + name (`602266 TooltipBaseBackground`,
  `602013 TooltipFrame`, etc.)
- Round-trips through `TryGet<TiledStyleDefinition>` (the
  composite is consumable today)
- Banner dictionary has both Map + Town variants

## Still NOT located in data (recon continues)

Three chrome pieces from the Optimizer's screenshots remain
unresolved:

- **Bullet glyph** (white diamond before each stat row)
- **Divider line** (horizontal with diamond end-caps — 4
  candidate TiledStyles from the #38 recon)
- **Icon bezel** (diamond ornate ring around the top-centre
  icon)

None match obvious tooltip-family atlases. Likely live in shared
UI chrome atlases or are widget-bound at runtime. **Next task:**
extract candidate atlas frames and have the owner do visual-close
against their tooltip screenshots — same approach that closed
FR-C19 #30 (the selection-highlight recipe).

## What's next on FR-C26

- **Slice 3 (recon)**: render divider candidates +
  search-for-bullet-bezel atlas frames; owner visual-close.
- **Slice 4 (if Optimizer needs full visual close)**:
  EXE-controller RE for layout rects + typography + per-state
  binding (the deep multi-session work).
