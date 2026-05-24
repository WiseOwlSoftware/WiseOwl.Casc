# ParagonTooltipChrome constructor

FR-C23 (Option A) — the engine's authored paragon-node tooltip chrome surfaced as a typed asset-reference recipe. The consumer looks up the entry for a node's [`ParagonRarity`](../ParagonRarity.md) and composites the panel via the existing `Diablo4Storage.ReadTiledStyle` / `Catalog.TryGet<TiledStyleDefinition>` path (the returned [`AssetRef`](../AssetRef.md)s are the same shape as everything else [`Catalog`](../Catalog.md) hands out).

```csharp
public ParagonTooltipChrome(AssetRef BaseLayer, 
    IReadOnlyDictionary<ParagonRarity, AssetRef> PanelByRarity, 
    IReadOnlyDictionary<string, AssetRef> ItemSidePanelByRarityName, AssetRef OrnateFrame, 
    AssetRef OrnateFrameLight, AssetRef DefaultFrame, AssetRef TextFrame, 
    IReadOnlyDictionary<string, AssetRef> BannerByPlacement, AssetRef SkillIconAtlas)
```

| parameter | description |
| --- | --- |
| BaseLayer | The universal dark backdrop (`TooltipBaseBackground`, sno `602266`) — the bottom- most layer in the multi-layer composite, common across every rarity. The consumer composites this first, then layers a [`PanelByRarity`](./PanelByRarity.md) overlay on top. |
| PanelByRarity | Map from the four paragon [`ParagonRarity`](../ParagonRarity.md) values to the `TooltipBackgroundRarity_<Rarity>`TiledStyle SNO. Iteration order is [`ParagonRarity`](../ParagonRarity.md) ascending (Common → Magic → Rare → Legendary). Always populated for every paragon rarity on a live install; the consumer can rely on indexing without a `TryGet` guard. |
| ItemSidePanelByRarityName | Future-proofing handle on the four item-side rarity panels (Unique / Set / Mythic / Season). Keyed by the engine's string rarity token (the suffix from the `TooltipBackgroundRarity_*` SNO name — `"Unique"`, `"Set"`, `"Mythic"`, `"Season"`); not keyed by [`ParagonRarity`](../ParagonRarity.md) because none of these rarities apply to paragon nodes. Available for any item-tooltip work the consumer takes on. |
| OrnateFrame | The decorative ornate spiky outer border (`TooltipFrame`, sno `602013`) — the engine's top layer over [`BaseLayer`](./BaseLayer.md) + [`PanelByRarity`](./PanelByRarity.md); the 9-slice's centre comes from `2DUI_BackgroundSquares` (handle `0xD756FD92`), the 8 perimeter pieces from `2DUITiled_TooltipFrame`. The dark-teal universal frame the Optimizer described as the "ornate spiky panel border" (the per-rarity color tint comes from [`PanelByRarity`](./PanelByRarity.md); this frame stays universal). |
| OrnateFrameLight | Light variant of [`OrnateFrame`](./OrnateFrame.md) (`TooltipFrameLight`, sno `603057`) — same 9 piece handles, alternative composition. The consumer picks whichever matches the desired brightness. |
| DefaultFrame | A smaller simple-bordered tooltip (`DefaultTooltip`, sno `478952`) — 9 small frames (28×28 corners) at the bottom of `2DUITiled_TooltipFrame`. Lower-decoration alternative for compact tooltips. |
| TextFrame | A text-only tooltip (`TextTooltip`, sno `478948`) — same nine atlas handles as [`DefaultFrame`](./DefaultFrame.md), alternative composition. Used by the engine for text-heavy tooltips. |
| BannerByPlacement | Banner-style chrome variants (`TooltipBanner_Map`, `TooltipBanner_Town`) keyed by their placement-token (the suffix on the SNO name). Not the same shape as the panel chrome; included as future-proofing for any non-tooltip banner work. |
| SkillIconAtlas | The `2DUI_Tooltip_Icons` (sno `2119840`) TextureAtlas — a 61-frame atlas of the inline skill-tag icons the engine composites into tooltip BODY prose (Druid mark, Demonform goat, Demonology / Hellfire / Abyss / Archfiend skill marks, etc.) wherever a `{c_important}` keyword token appears in an [`Description`](../ParagonGlyphAffixDefinition/Description.md) template. Not chrome in the strict layout sense — surfaced here because it's a sibling tooltip resource the consumer needs alongside the panel layers when rendering glyph affixes (FR-C24 continuation). Decode via the existing [`TryGet`](../Catalog/TryGet.md) path with [`TextureDefinition`](../TextureDefinition.md) to access the 61 individual [`TexFrame`](../TexFrame.md) handles + UVs; the semantic keyword→handle mapping (which frame is "Demonology", etc.) is engine-coded and is the consumer's calibration. |

## Remarks

Each entry is a `TooltipBackgroundRarity_<Rarity>`TiledStyle SNO of the `TiledWindowPieces` variant (FR-C14 R10 / CL-62 — 9 piece handles, `ImageScale`, padding, tile flags); the consumer 9-slice-composites it at whatever rect the layout (see `casc-fr#38` — FR-C26) places.

Coverage. The paragon-relevant set is Common / Magic / Rare / Legendary — populated on every install. The item-side rarities (Unique / Set / Mythic / Season) live in the same engine family (`TooltipBackgroundRarity_Unique` 602974, `_Set` 602973, `_Mythic` 2004596, `_Season` 2417490) and are surfaced on the sibling [`ItemSidePanelByRarityName`](./ItemSidePanelByRarityName.md) dictionary — they're not keyed by [`ParagonRarity`](../ParagonRarity.md) (which is a paragon-node concept), but useful future-proofing for any item-tooltip work the consumer takes on.

Open chrome pieces. The bullet glyph, divider line, and icon bezel referenced by the Optimizer's tooltip captures live in engine-coded controller bindings (the `AttributeBullet`/`CoreStatDivider`/ `TitleDecoLeft`/`TitleDecoRight` widgets in [`ReadUiScene`](../Diablo4Storage/ReadUiScene.md)'s graph for `ParagonBoard` 657304 are bare template stubs — no own image, no own rect, populated at runtime). They're tracked on `casc-fr#38` (FR-C26) — the full tooltip layout / per-state RE — and surface there alongside the rect geometry; this surface stops at the chrome panel.

## See Also

* struct [AssetRef](../AssetRef.md)
* enum [ParagonRarity](../ParagonRarity.md)
* record [ParagonTooltipChrome](../ParagonTooltipChrome.md)
* namespace [WiseOwl.Casc.Diablo4](../../WiseOwl.Casc.Diablo4.md)

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.Diablo4.dll -->
