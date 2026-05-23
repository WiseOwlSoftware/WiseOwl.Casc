using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// FR-C23 (Option A) — the engine's authored paragon-node tooltip
/// chrome surfaced as a typed asset-reference recipe. The consumer
/// looks up the entry for a node's <see cref="ParagonRarity"/> and
/// composites the panel via the existing
/// <c>Diablo4Storage.ReadTiledStyle</c> /
/// <c>Catalog.TryGet&lt;TiledStyleDefinition&gt;</c> path (the
/// returned <see cref="AssetRef"/>s are the same shape as everything
/// else <see cref="Catalog"/> hands out).
/// </summary>
/// <remarks>
/// <para>
/// Each entry is a <c>TooltipBackgroundRarity_&lt;Rarity&gt;</c>
/// <see cref="AssetKind.TiledStyle"/> SNO of the
/// <c>TiledWindowPieces</c> variant (FR-C14 R10 / CL-62 — 9 piece
/// handles, <c>ImageScale</c>, padding, tile flags); the consumer
/// 9-slice-composites it at whatever rect the layout
/// (see <c>casc-fr#38</c> — FR-C26) places.
/// </para>
/// <para>
/// <b>Coverage.</b> The paragon-relevant set is
/// <see cref="ParagonRarity.Common"/> / <see cref="ParagonRarity.Magic"/> /
/// <see cref="ParagonRarity.Rare"/> / <see cref="ParagonRarity.Legendary"/>
/// — populated on every install. The item-side rarities
/// (Unique / Set / Mythic / Season) live in the same engine family
/// (<c>TooltipBackgroundRarity_Unique</c> 602974, <c>_Set</c> 602973,
/// <c>_Mythic</c> 2004596, <c>_Season</c> 2417490) and are surfaced
/// on the sibling
/// <see cref="ItemSidePanelByRarityName"/> dictionary — they're not
/// keyed by <see cref="ParagonRarity"/> (which is a paragon-node
/// concept), but useful future-proofing for any item-tooltip work
/// the consumer takes on.
/// </para>
/// <para>
/// <b>Open chrome pieces.</b> The bullet glyph, divider line, and
/// icon bezel referenced by the Optimizer's tooltip captures live in
/// engine-coded controller bindings (the
/// <c>AttributeBullet</c>/<c>CoreStatDivider</c>/
/// <c>TitleDecoLeft</c>/<c>TitleDecoRight</c> widgets in
/// <see cref="Diablo4Storage.ReadUiScene"/>'s graph for
/// <c>ParagonBoard</c> 657304 are bare template stubs — no own image,
/// no own rect, populated at runtime). They're tracked on
/// <c>casc-fr#38</c> (FR-C26) — the full tooltip layout / per-state
/// RE — and surface there alongside the rect geometry; this surface
/// stops at the chrome panel.
/// </para>
/// </remarks>
/// <param name="PanelByRarity">Map from the four paragon
/// <see cref="ParagonRarity"/> values to the
/// <c>TooltipBackgroundRarity_&lt;Rarity&gt;</c>
/// <see cref="AssetKind.TiledStyle"/> SNO. Iteration order is
/// <see cref="ParagonRarity"/> ascending (Common → Magic → Rare →
/// Legendary). Always populated for every paragon rarity on a
/// live install; the consumer can rely on indexing without a
/// `TryGet` guard.</param>
/// <param name="ItemSidePanelByRarityName">Future-proofing handle on
/// the four item-side rarity panels (Unique / Set / Mythic /
/// Season). Keyed by the engine's string rarity token (the suffix
/// from the <c>TooltipBackgroundRarity_*</c> SNO name —
/// <c>"Unique"</c>, <c>"Set"</c>, <c>"Mythic"</c>, <c>"Season"</c>);
/// not keyed by <see cref="ParagonRarity"/> because none of these
/// rarities apply to paragon nodes. Available for any item-tooltip
/// work the consumer takes on.</param>
public sealed record ParagonTooltipChrome(
    IReadOnlyDictionary<ParagonRarity, AssetRef> PanelByRarity,
    IReadOnlyDictionary<string, AssetRef> ItemSidePanelByRarityName);
