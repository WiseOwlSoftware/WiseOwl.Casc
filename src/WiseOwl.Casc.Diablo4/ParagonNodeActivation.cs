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
    /// <summary>The node is the currently selected/highlighted node.</summary>
    Selected,
    /// <summary>The node is not the selected node.</summary>
    Unselected,
    /// <summary>The node has been purchased/allocated.</summary>
    Purchased,
    /// <summary>The node is reachable and can be purchased now.</summary>
    Purchasable,
    /// <summary>The node has been revealed (its tile is uncovered).</summary>
    Revealed,
    /// <summary>The node is the located/targeted ("you are here") node.</summary>
    Located,
    /// <summary>The node is a glyph socket with a glyph socketed.</summary>
    Socketed,
    /// <summary>The node carries the equipped-item glow.</summary>
    Equipped,
    /// <summary>The node matches the active search query.</summary>
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
    /// <summary>The node's rarity is Magic.</summary>
    RarityMagic,
    /// <summary>The node's rarity is Rare.</summary>
    RarityRare,
    /// <summary>The node's rarity is Legendary.</summary>
    RarityLegendary,
    /// <summary>The node is a glyph-socket node.</summary>
    TypeSocket,
    /// <summary>The node is the board exit/gate node.</summary>
    TypeGate,
    /// <summary>The node is the board start node.</summary>
    TypeStart,
    /// <summary>The node is locked (not yet reachable) — engine texture
    /// state <c>ParagonNode_Texture_Locked</c>.</summary>
    Locked,
    /// <summary>The node is unlocked (reachable but not purchased) — engine
    /// state <c>ParagonNode_Legendary_Unlocked</c>.</summary>
    Unlocked,
    /// <summary>Engine interaction state: the widget is pressed.</summary>
    Pressed,
    /// <summary>Engine interaction state: the cursor is over the widget.</summary>
    MouseOver,
    /// <summary>Engine interaction state: the widget is disabled.</summary>
    Disabled,
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
    /// <c>Template_Node_Magic</c> → <see cref="NodeFact.RarityMagic"/>).</summary>
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
