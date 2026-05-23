using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// The display-axis classification of a paragon node — what the in-game
/// tooltip headline shows (the visual archetype, distinct from
/// <see cref="ParagonRarity"/>). Derived from
/// <see cref="ParagonNodeDefinition.NodeType"/> +
/// <see cref="ParagonNodeDefinition.IsGate"/> +
/// <see cref="ParagonNodeDefinition.HasSocket"/> +
/// <see cref="ParagonNodeDefinition.Rarity"/>.
/// </summary>
/// <remarks>
/// Precedence (a node can satisfy more than one of the underlying
/// fields; the kind picks the most specific archetype): <see cref="Start"/>
/// → <see cref="Gate"/> → <see cref="Socket"/> → then by rarity
/// (<see cref="Normal"/> / <see cref="Magic"/> / <see cref="Rare"/> /
/// <see cref="Legendary"/>).
/// </remarks>
public enum ParagonNodeKind
{
    /// <summary>A plain board node (no socket, no gate, common rarity).</summary>
    Normal,
    /// <summary>A magic-rarity stat node.</summary>
    Magic,
    /// <summary>A rare-rarity stat node (carries the "bonus when threshold met"
    /// mechanic — see <see cref="ParagonNodeDefinition.BonusStatTagSnoIds"/>).</summary>
    Rare,
    /// <summary>A legendary-rarity node.</summary>
    Legendary,
    /// <summary>The board's start node (<c>eNodeType == 5</c>) — the class emblem
    /// that anchors the board chain; carries no stat grants.</summary>
    Start,
    /// <summary>A glyph-socket node (<c>bHasSocket</c>) — grants its stat indirectly
    /// through the seated glyph and the surrounding magic nodes; carries no
    /// direct stat grants.</summary>
    Socket,
    /// <summary>A gate / attachment-marker node (<c>bIsGate</c>) — structural,
    /// not a stat node; carries no stat grants.</summary>
    Gate,
}

/// <summary>
/// How a <see cref="ParagonNodeStat.FlatValue"/> should be rendered for
/// display. <see cref="Flat"/> means "show as a raw number"
/// (<c>+5 Strength</c>); <see cref="Percent"/> means "show with a
/// percent sign" (<c>+7.5% Total Armor</c>); <see cref="Multiplier"/>
/// is reserved for future stats that display as a multiplicative
/// factor (<c>×1.5</c>) — no paragon node uses it today.
/// </summary>
/// <remarks>
/// The library's heuristic derives the unit from the stat token + the
/// underlying <see cref="NodeAttribute.AttributeId"/> (best-effort,
/// matches the in-game tooltip on every node we've validated). It is
/// a <i>hint</i> — the <see cref="ParagonNodeStat.FlatValue"/> is the
/// numeric truth.
/// </remarks>
public enum StatUnit
{
    /// <summary>Render as a raw number (<c>+5 Strength</c>,
    /// <c>+3 All Resistance</c>).</summary>
    Flat,
    /// <summary>Render as a percentage (<c>+7.5% Total Armor</c>,
    /// <c>+10% Damage</c>, <c>+4% Max Life</c>).</summary>
    Percent,
    /// <summary>Render as a multiplicative factor (<c>×1.5</c>). Reserved;
    /// not used by any paragon node observed.</summary>
    Multiplier,
}

/// <summary>
/// One stat grant on a paragon node — the display-ready projection of a
/// <see cref="NodeAttribute"/>. The library does the formula evaluation
/// and unit inference for the node-info surface (FR-C21 carve-out from
/// the Appendix C boundary).
/// </summary>
/// <param name="AttributeId">The raw <see cref="NodeAttribute.AttributeId"/>
/// — the engine's <c>eAttribute</c>. <b>Note:</b> this is a power-budget
/// category, <b>not</b> the canonical stat key (three distinct
/// <see cref="ParagonNodeInfo.Sno"/>s can share id <c>481</c>; see the
/// Optimizer correction relayed in <c>docs/devlog/0063-*.md</c>).
/// Surfaced raw so the consumer can inspect the budget category — for
/// stat-identity aggregation, key on
/// <see cref="ParagonNodeInfo.Sno"/>.</param>
/// <param name="StatName">Human-readable stat name, derived clean-room
/// from the <c>Generic_&lt;Rarity&gt;_&lt;Token&gt;</c> node-name convention
/// (light CamelCase split + known-abbreviation expansion;
/// <c>Generic_Magic_DamageToElite</c> → <c>"Damage to Elite"</c>).
/// Non-Generic class-specific node names (e.g. <c>Warlock_Rare_006</c>)
/// have no encoded stat token and fall back to <c>"Attribute &lt;id&gt;"</c>.</param>
/// <param name="Variant">The raw <see cref="NodeAttribute.NParam"/> —
/// informational. Always <c>0</c> on every paragon stat node sampled
/// so far (the per-element resistance/damage variants the engine
/// distinguishes via a single <see cref="NodeAttribute.NParam"/> in
/// other domains are split into distinct nodes here, so each carries
/// its own <see cref="ParagonNodeInfo.Sno"/> with the variant
/// expressed in the node name).</param>
/// <param name="VariantName">Reserved (always <see langword="null"/>
/// today). Iterate via a follow-on FR if a future stat surfaces a
/// non-zero <paramref name="Variant"/> with a meaningful resolution.</param>
/// <param name="FlatValue">The displayed magnitude — the formula text
/// from <see cref="AttributeFormulaTable"/> evaluated via
/// <see cref="ParagonMagnitudeFormula.Evaluate"/> (or the inline
/// constant on a Normal-rarity node). <see langword="null"/> when the
/// formula references an unknown budget-multiplier intrinsic (a future
/// build added one the calibration table hasn't picked up yet) — the
/// consumer can still inspect <paramref name="Formula"/>.</param>
/// <param name="Unit">How to render <paramref name="FlatValue"/> —
/// see <see cref="StatUnit"/>.</param>
/// <param name="Formula">Informational reference to the named
/// <see cref="AttributeFormulaTable"/> entry the magnitude evaluation
/// resolved through (<see cref="AssetKind.AttributeFormulas"/> with
/// the formula name in <see cref="AssetRef.Tags"/>);
/// <see langword="null"/> when the node ships an inline formula
/// (<see cref="NodeAttribute.IsInline"/>) — the inline text is on
/// <paramref name="InlineFormula"/>.</param>
/// <param name="InlineFormula">The node's own inline formula text when
/// <see cref="NodeAttribute.IsInline"/> is true; <see langword="null"/>
/// when the node references a shared GameBalance formula via
/// <paramref name="Formula"/>.</param>
public sealed record ParagonNodeStat(
    int AttributeId,
    string StatName,
    int Variant,
    string? VariantName,
    double? FlatValue,
    StatUnit Unit,
    AssetRef? Formula,
    string? InlineFormula);

/// <summary>
/// FR-C21 — the display-ready projection of one paragon node. The
/// library evaluates magnitudes + infers units + resolves names so the
/// consumer can render the in-game tooltip without re-walking the raw
/// byte fields or owning the formula evaluator (per the Appendix C
/// carve-out for this surface).
/// </summary>
/// <remarks>
/// <para>
/// <b>Aggregation key.</b> The canonical key for a stat (e.g. "+7.5%
/// Total Armor") is <see cref="Sno"/>, not
/// <c>(AttributeId, NParam)</c>: three nodes
/// (<c>Generic_Magic_Armor</c>, <c>Generic_Magic_ArmorPercent</c>,
/// <c>Generic_Magic_DamageReductionFromElite</c>) decode to
/// <i>identical</i> attribute fields (<c>AttributeId 481</c>,
/// <c>NParam 0</c>, same formula GBID, same parallel-array GBID) but
/// display three distinct stats. The Optimizer signed off on this
/// correction (FR-C21 / <c>casc-fr#33</c>, 2026-05-23).
/// </para>
/// <para>
/// <b>Boundary.</b> The library returns ready-to-display values for
/// this surface only (FR-C21 carve-out from Appendix C). Other formula
/// domains (power-script output, glyph rank/radius scaling,
/// item/affix value resolution) remain the consumer's.
/// </para>
/// </remarks>
/// <param name="Sno">The node's SNO id (group <see cref="SnoGroup.ParagonNode"/>)
/// — the canonical stat-identity key.</param>
/// <param name="Name">The node's CoreTOC name (e.g.
/// <c>Generic_Magic_Armor</c>, <c>Warlock_Rare_006</c>). The most
/// patch-durable identity across builds.</param>
/// <param name="Kind">The visual archetype — see
/// <see cref="ParagonNodeKind"/>.</param>
/// <param name="Rarity">The raw <see cref="ParagonRarity"/>
/// (<c>eRarityOverride</c>). Distinct from <see cref="Kind"/>: a rare
/// node has <see cref="Kind"/>=<see cref="ParagonNodeKind.Rare"/> AND
/// <see cref="Rarity"/>=<see cref="ParagonRarity.Rare"/>; a Start
/// node has <see cref="Kind"/>=<see cref="ParagonNodeKind.Start"/>
/// but <see cref="Rarity"/>=<see cref="ParagonRarity.Common"/>.</param>
/// <param name="Icon">The atlas (<see cref="AssetKind.TextureAtlas"/>)
/// containing the <see cref="ParagonNodeDefinition.HIcon"/> frame, when
/// the node authors one (most nodes leave <c>HIcon = 0</c> and rely on
/// <see cref="IconMask"/>). Resolve the per-frame UVs via
/// <see cref="Catalog.TryResolveFrame"/> against the raw handle.</param>
/// <param name="IconMask">The atlas containing the
/// <see cref="ParagonNodeDefinition.HIconMask"/> frame — the symbol icon
/// shown on the node's disc.</param>
/// <param name="PassivePower">The <see cref="AssetKind.Power"/> SNO
/// the node grants (when <see cref="ParagonNodeDefinition.SnoPassivePower"/>
/// is set), pre-resolved as an asset reference;
/// <see langword="null"/> when the node grants no passive power.</param>
/// <param name="PassivePowerName">The localized name of
/// <paramref name="PassivePower"/> from the sibling <c>Power_&lt;Name&gt;</c>
/// StringList (<c>§6.7</c>); <see langword="null"/> when there is no
/// passive power, or when the sibling string list is missing.</param>
/// <param name="Stats">The node's stat grants — display-ready
/// magnitudes, units, and names (see <see cref="ParagonNodeStat"/>).
/// Empty for <see cref="ParagonNodeKind.Start"/>,
/// <see cref="ParagonNodeKind.Socket"/>, and
/// <see cref="ParagonNodeKind.Gate"/>.</param>
/// <param name="HasSocket">Raw
/// <see cref="ParagonNodeDefinition.HasSocket"/> for back-compat;
/// equivalent to <see cref="Kind"/>=<see cref="ParagonNodeKind.Socket"/>.</param>
/// <param name="IsGate">Raw
/// <see cref="ParagonNodeDefinition.IsGate"/> for back-compat;
/// equivalent to <see cref="Kind"/>=<see cref="ParagonNodeKind.Gate"/>.</param>
public sealed record ParagonNodeInfo(
    int Sno,
    string Name,
    ParagonNodeKind Kind,
    ParagonRarity Rarity,
    AssetRef? Icon,
    AssetRef? IconMask,
    AssetRef? PassivePower,
    string? PassivePowerName,
    IReadOnlyList<ParagonNodeStat> Stats,
    bool HasSocket,
    bool IsGate);
