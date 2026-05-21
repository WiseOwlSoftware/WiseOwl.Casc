using System;
using System.Collections.Generic;
using System.Linq;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// FR-C16 R11 — the closed vocabulary of <b>runtime node facts</b> a
/// consumer computes and feeds to a layer's <see cref="NodeActivation"/>.
/// Each value is a single boolean condition the engine's UI controller
/// keys a widget's visibility off. The consumer owns ONLY the computation
/// of these facts (genuine app state — which node is selected, purchased,
/// reachable, …); the mapping fact→layer is the engine-sourced
/// <see cref="NodeActivation"/>, never consumer-authored.
/// </summary>
/// <remarks>
/// Provenance (see <see cref="NodeActivationSource"/>): the paragon UI
/// scene does <b>not</b> store an activation expression per widget — there
/// is no condition/visibility/predicate field, no binding expression in
/// the value records, and no condition-SNO reference (FR-C16 R10, scene
/// 657304, exhaustively verified). The engine binds a widget's
/// <c>bActive</c> to a runtime state <b>by the widget's name</b> in its C++
/// UI controller; the data-side representation of the association is the
/// <b>naming convention</b> (per-state field suffixes such as
/// <c>hImageFramePressed</c>/<c>MouseOver</c>/<c>Disable</c>, and per-state
/// widget/asset names such as <c>Node_Purchased</c>/<c>Node_Purchasable</c>/
/// <c>Template_Node_Magic</c>). CASC decodes that convention into the typed
/// activations below so the consumer evaluates rather than invents.
/// <br/><br/>
/// <b>EXE-validated (FR-C16 R12).</b> This vocabulary is corroborated by the
/// named data-source / predicate symbols in <c>Diablo IV.exe</c> — the
/// engine uses a <c>DataBinding</c>/<c>SetObjectBinding</c> system whose
/// boolean sources include <c>ParagonNodeIsPurchased</c>
/// (= <see cref="Purchased"/>), <c>IsSelected</c> (= <see cref="Selected"/>),
/// <c>IsLocked</c>/<c>ParagonNode_Texture_Locked</c> (= <see cref="Locked"/>),
/// <c>IsEquipped</c> (= <see cref="Equipped"/>), and
/// <c>ParagonGlyphAffixIsActive</c>. The per-widget wiring lives in the
/// <c>ParagonBoardUI</c> controller (compiled code, not a SNO field); the
/// source <i>names</i> are EXE-recoverable, the wiring needs disassembly.
/// </remarks>
public enum NodeFact
{
    /// <summary>The layer always draws (no gating fact).</summary>
    Always = 0,
    // ----- Availability / progression (engine Node_* state enum:
    // Node_{Available,Disabled,Purchasable,Purchased}, + the legendary
    // Locked/Unlocked progression) -----------------------------------------
    /// <summary>The node has been purchased/allocated (engine
    /// <c>Node_Purchased</c> / <c>ParagonNodeIsPurchased</c>). The base disc
    /// swaps to the purchased variant (red ring, brighter) and the purchased
    /// add-ons (cardinal arrows + connectors) draw.</summary>
    Purchased,
    /// <summary>The node has not been purchased — the resting/default base
    /// disc (no red ring). The positive complement of <see cref="Purchased"/>
    /// (the engine's default state; the consumer sets this when the node is
    /// not purchased).</summary>
    Unpurchased,
    /// <summary>The node can be purchased now (engine <c>Node_Purchasable</c>).
    /// Distinct from <see cref="Available"/> — both are separate engine
    /// widgets/states.</summary>
    Purchasable,
    /// <summary>The node is available/reachable (engine <c>Node_Available</c> /
    /// the <c>NodeAvailableGlow</c> widget). The engine keeps this and
    /// <see cref="Purchasable"/> as separate states/widgets, so CASC surfaces
    /// both; the precise distinction is not yet known (likely
    /// reachable-vs-affordable). A consumer MAY treat the two identically
    /// until a behavioural difference is established.</summary>
    Available,
    /// <summary>The node is interaction-disabled (engine
    /// <c>Node_Disabled</c> / <c>hImageFrameDisable</c>).</summary>
    Disabled,
    /// <summary>The node has been revealed (engine <c>Common_Node_Revealed</c>
    /// / <c>Board_Attach_Reveal</c>).</summary>
    Revealed,
    // ----- Selection / overlays (orthogonal to availability) --------------
    /// <summary>The node is the currently selected node (engine
    /// <c>Node_Selected</c> / <c>IsSelected</c>).</summary>
    Selected,
    /// <summary>The node is not the selected node — the positive complement
    /// of <see cref="Selected"/> (the engine's default/"normal" state; the
    /// consumer sets this when the node is not selected).</summary>
    Unselected,
    /// <summary>The node is the located/targeted ("you are here") node
    /// (engine <c>Node_Located</c>).</summary>
    Located,
    /// <summary>The node is a glyph socket with a glyph socketed (engine
    /// <c>ui_paragon_glyphNode_socketed_ring</c>).</summary>
    Socketed,
    /// <summary>The node is a persistent (always-active) legendary node
    /// (engine <c>ui_paragon_legendaryNode_persistent</c>).</summary>
    Persistent,
    /// <summary>The node carries the equipped-item glow (engine
    /// <c>Node_EquipGlow</c> / <c>ItemIsEquipped</c>).</summary>
    Equipped,
    /// <summary>The node matches the active search query (engine
    /// <c>Node_SearchResultHighlight</c>).</summary>
    SearchMatch,
    /// <summary>The node is the tutorial-highlighted node.</summary>
    Tutorial,
    /// <summary>The cardinal-north neighbour is purchasable.</summary>
    NeighbourPurchasableTop,
    /// <summary>The cardinal-east neighbour is purchasable.</summary>
    NeighbourPurchasableRight,
    /// <summary>The cardinal-south neighbour is purchasable.</summary>
    NeighbourPurchasableBottom,
    /// <summary>The cardinal-west neighbour is purchasable.</summary>
    NeighbourPurchasableLeft,
    /// <summary>The cardinal-north neighbour is already purchased (connector
    /// target — distinct from a purchasable neighbour, which an arrow points
    /// to).</summary>
    NeighbourPurchasedTop,
    /// <summary>The cardinal-east neighbour is already purchased.</summary>
    NeighbourPurchasedRight,
    /// <summary>The cardinal-south neighbour is already purchased.</summary>
    NeighbourPurchasedBottom,
    /// <summary>The cardinal-west neighbour is already purchased.</summary>
    NeighbourPurchasedLeft,
    // ----- Node-kind dimension (FR-C16 R14) -----------------------------
    // The base disc is selected by ONE mutually-exclusive "node kind"
    // classification — the engine enumerates it as a single list
    // (Play_UI_Menu_Paragon_Purchase_Node_{Common,Magic,Rare,Legendary,
    // Socket,Gate}). The rarities (Common/Magic/Rare/Legendary = eRarity
    // 0/2/3/4) and the structural types (Socket/Gate/Start) are peers in it,
    // NOT two dimensions: exactly one Kind* fact holds per node, so the base
    // discs (grey Node_IconBase for Common, Template_Node_<kind> for the
    // rest) are mutually exclusive without any negation.
    /// <summary>Node kind Common (the grey base disc; engine <c>eRarity</c> 0).</summary>
    KindCommon,
    /// <summary>Node kind Magic (<c>eRarity</c> 2).</summary>
    KindMagic,
    /// <summary>Node kind Rare (<c>eRarity</c> 3).</summary>
    KindRare,
    /// <summary>Node kind Legendary (<c>eRarity</c> 4).</summary>
    KindLegendary,
    /// <summary>Node kind glyph-socket (<c>Template_Node_Socketable</c>).</summary>
    KindSocket,
    /// <summary>Node kind board exit/gate (<c>Template_Node_Quest</c>).</summary>
    KindGate,
    /// <summary>Node kind board start/entry (<c>Template_Node_Starter</c>).</summary>
    KindStart,
    /// <summary>The node is locked (not yet reachable) — engine texture
    /// state <c>ParagonNode_Texture_Locked</c>.</summary>
    Locked,
    /// <summary>The node is unlocked (reachable but not purchased) — engine
    /// state <c>ParagonNode_Legendary_Unlocked</c>.</summary>
    Unlocked,
    /// <summary>Engine widget interaction state: pressed
    /// (<c>hImageFramePressed</c>).</summary>
    Pressed,
    /// <summary>Engine widget interaction state: cursor over
    /// (<c>hImageFrameMouseOver</c>).</summary>
    MouseOver,
    /// <summary>The layer never draws under any computed fact (an
    /// authored-inactive widget with no recovered predicate).</summary>
    Never,
}

/// <summary>
/// FR-C16 R11 — where a <see cref="NodeActivation"/>'s condition was
/// recovered from. The honesty marker for the
/// <c>feedback_widget-name-not-role</c> discipline: a <c>SceneField</c>
/// activation is read verbatim from the data; a <c>NameConvention</c> one
/// is decoded from the engine's own state-suffixed identifier; an
/// <c>EngineBehavior</c> one is CASC's documented inference where the name
/// is suggestive but not literal.
/// </summary>
public enum NodeActivationSource
{
    /// <summary>The widget/field name literally spells the state (e.g.
    /// <c>Node_Purchased</c> → <see cref="NodeFact.Purchased"/>,
    /// <c>Template_Node_Magic</c> → <see cref="NodeFact.KindMagic"/>).</summary>
    NameConvention,
    /// <summary>Read verbatim from a scene field (none exist for activation
    /// in scene 657304 — reserved for a future build that authors one).</summary>
    SceneField,
    /// <summary>CASC's documented engine-behavior inference where the name
    /// is suggestive but not literal (e.g. <c>NodeAvailableGlow</c> →
    /// <see cref="NodeFact.Purchasable"/>). Validate against the owner
    /// oracle before relying on it.</summary>
    EngineBehavior,
}

/// <summary>
/// FR-C16 R11 — the typed activation condition of a recipe layer: the
/// engine-sourced predicate over <see cref="NodeFact"/> that gates whether
/// the layer draws. The consumer supplies the set of currently-true facts
/// and calls <see cref="Evaluate"/>; it authors no predicate of its own.
/// </summary>
/// <param name="AllOf">The facts that must <b>all</b> hold for the layer to
/// draw (logical AND). Empty ⇒ <see cref="NodeFact.Always"/>. A list
/// containing <see cref="NodeFact.Never"/> never activates.</param>
/// <param name="Source">Where the condition was recovered from — the
/// honesty marker (see <see cref="NodeActivationSource"/>).</param>
public sealed record NodeActivation(
    IReadOnlyList<NodeFact> AllOf,
    NodeActivationSource Source)
{
    /// <summary>An always-on activation (no gating fact).</summary>
    public static NodeActivation Always { get; } =
        new(Array.Empty<NodeFact>(), NodeActivationSource.NameConvention);

    /// <summary>Build a single-fact activation.</summary>
    public static NodeActivation Of(NodeFact fact, NodeActivationSource source) =>
        new(new[] { fact }, source);

    /// <summary>
    /// Evaluate the activation against the consumer-computed set of
    /// currently-true facts: the layer draws iff every fact in
    /// <see cref="AllOf"/> is present (and none is
    /// <see cref="NodeFact.Never"/>).
    /// </summary>
    /// <param name="trueFacts">The facts the consumer has computed as true
    /// for the node being drawn.</param>
    public bool Evaluate(IReadOnlySet<NodeFact> trueFacts)
    {
        ArgumentNullException.ThrowIfNull(trueFacts);
        foreach (var f in AllOf)
        {
            if (f is NodeFact.Never) return false;
            if (f is NodeFact.Always) continue;
            if (!trueFacts.Contains(f)) return false;
        }
        return true;
    }
}
