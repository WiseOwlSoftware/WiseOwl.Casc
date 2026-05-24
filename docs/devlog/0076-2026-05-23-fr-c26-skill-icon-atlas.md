# 0076 — FR-C26: `2DUI_Tooltip_Icons` skill-tag icon atlas

2026-05-23 · CL-81 · branch `fr-c26-skill-icon-atlas`

## Trigger

While running the FR-C26 round-2 recon, I tracked the "2DUI_*" atlas
family for atlases the existing chrome surface didn't expose. Found
`2DUI_Tooltip_Icons` (sno `2119840`) — 61 frames at sizes ~47×42 to
109×163. Spot-extracted a few via `build/AtlasExport frame`:

- `0xB33A2F34` (47×42) — small spiky leaf (a Druid skill mark)
- `0x5131550A` (94×100) — goat head (Demonform / Demonology icon)
- `0xC0E66984` (109×163) — chain / shackle decorative element
- …

These are **inline skill-tag icons** the engine composites into
tooltip body prose. The glyph affix description templates carry
keyword tokens like:

```
… +0.7% increased damage with {c_important}Abyss Skills{/c}.
… while {c_important}{u}Healthy{/u}{/c}.
… each {c_important}Hex{/c} → +8% Abyss damage taken.
```

The engine renders an icon next to each keyword. The consumer's
FR-C24 affix-tooltip renderer needs the same icon set.

## Surface

Extended `ParagonTooltipChrome` with one more field — the atlas
ref to `2DUI_Tooltip_Icons`:

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
    AssetRef SkillIconAtlas);    // NEW (CL-81)
```

`AssetKind.TextureAtlas` — same shape as every other atlas
`Catalog.OfKind(AssetKind.TextureAtlas)` would yield. Resolved
via `CoreToc.TryGetId(SnoGroup.Texture, "2DUI_Tooltip_Icons")` +
the existing `AssetProviders.AtlasRef` helper. No new
`AssetKind` needed — piggybacks on the existing
TextureAtlas+TextureDefinition path.

## Why surface it on the chrome record

It's not chrome in the strict panel-layout sense — it's
tooltip-body content. But it's a **sibling tooltip resource** the
consumer needs when rendering glyph affix bodies. The
`ParagonTooltipChrome` record is the natural one-stop entry for
"all tooltip-related authored resources" — putting the skill-icon
atlas there means the consumer's tooltip-rendering pipeline reads
one record and has every asset it needs.

If the Optimizer prefers it broken out as a separate
`Catalog.GetTooltipBodyIcons()` method or a new `AssetKind` (like
the singleton `AssetKind.ParagonNodeRender`), that's a one-line
refactor on a future counter-round — no API churn for consumers
that aren't already using it.

## What's NOT in this CL

- **Keyword→handle semantic mapping.** Which atlas frame is the
  "Demonology" icon? Which is "Abyss"? That's engine-coded
  (probably a string-keyed lookup in the C++ tooltip renderer
  that maps the `{c_important}` keyword to a specific frame
  handle). The library surfaces all 61 frames + their handles
  via `TextureDefinition.Frames`; the consumer calibrates the
  keyword↔frame mapping by visual inspection or by EXE RE.

  This is the same boundary FR-C7 hit for the
  `ParagonBoardUI` controller — the data side is decodable, the
  controller wiring is engine code. The consumer-side
  calibration is the right home for the keyword→handle table
  because it's a presentation concern that varies with the
  consumer's UI choices (e.g. they might want to substitute
  their own iconography for some keywords).

## Tests

The live `Acceptance_matrix_against_live_install` extends:

- `SkillIconAtlas.Sno == 2119840`, `Name == "2DUI_Tooltip_Icons"`.
- `Catalog.TryGet<TextureDefinition>(SkillIconAtlas, out var td)`
  succeeds, `td.Frames.Count == 61`.

126/126 tests green on `3.0.2.71886`.

## FR-C26 status

Three chrome slices delivered now:

- ✅ Phase A (CL-77) — per-rarity panel
- ✅ Phase A+ (CL-80) — full multi-layer composite (base + rarity
  + ornate frame + variants + banners)
- ✅ Phase A++ (CL-81 — this CL) — inline skill-tag icon atlas

Outstanding for FR-C26:

- 🟡 Divider — 4 candidates rendered; owner visual-close pending
- 🔴 Bullet glyph + icon bezel — engine-coded (runtime-bound;
  next phase if Optimizer wants pixel-exact match)
- 🔴 Layout rects + typography + per-state binding — multi-CL EXE
  RE; deferred until the consumer signals visual-close is the
  hard acceptance gate
