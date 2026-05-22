using System;
using System.Collections.Generic;
using System.Linq;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// The typed paragon-board render projection (FR-C7), built on top of
/// the generic <see cref="UiScene"/> decode of <c>ParagonBoard</c>
/// (SNO 657304). Raw decoded geometry only — the consumer owns the
/// resolution/zoom scale and all imaging/compositing (boundary:
/// <c>docs/casc-diablo4-format.md</c> Appendix C, §10.7).
/// </summary>
/// <remarks>
/// Contract: <c>docs/fr-c7-api-proposal.md</c> §7.1 (Round-11, agreed;
/// amendable until the next NuGet publish). <see cref="Ratios"/> are
/// emitted with <see cref="RenderRatios.Provisional"/> = <see
/// langword="true"/> until they reproduce the §10.8 67.7 px/grid anchor
/// (the over-determined check; the oracle is the verifier, never the
/// source). The raw <see cref="WidgetRect"/>s are unconditional facts
/// (read from the bound instance records) for audit + the CL acceptance
/// row. <see cref="BoardRotationQuadrant"/> is a 90°-multiple index —
/// 45° is unrepresentable by construction (CL-10).
/// </remarks>
public sealed record ParagonRenderLayout(
    RenderRatios Ratios,
    CanvasRef CanvasReference,
    WidgetRect NodeContainer,
    WidgetRect NodeTemplate,
    int BoardRotationQuadrant,
    NodeElement Disc,
    NodeElement Symbol,
    IReadOnlyList<StateElements> States,
    NodeElement CommonNodeRevealedLayer);

/// <summary>
/// The <b>exhaustive</b> paragon render-model (FR-C9): the role-assigned
/// <see cref="Layout"/> (FR-C7/C8 typed projection) plus, for every
/// paragon UI-scene, every widget that binds at least one real atlas
/// texture handle — with the handle, its decoded
/// <see cref="WidgetRect"/>, and alpha. This is the one-shot audit
/// surface: the library guarantees it is <b>complete</b> (no binding
/// shape dropped — proven by the FR-C9 coverage gate); the consumer
/// owns role/state classification (FR-C7 §6 boundary).
/// </summary>
public sealed record ParagonRenderModel(
    ParagonRenderLayout Layout,
    IReadOnlyList<ParagonSceneModel> Scenes,
    ParagonBoardChrome BoardChrome);

/// <summary>One paragon UI-scene's complete atlas-binding model: every
/// widget that binds ≥1 real atlas handle.</summary>
/// <param name="SnoId">The UI-scene SNO (657304 ParagonBoard /
/// 964599 ParagonBoardSelect).</param>
/// <param name="Widgets">Every binding widget, in scene order.</param>
public sealed record ParagonSceneModel(
    int SnoId, IReadOnlyList<ParagonBoundWidget> Widgets);

/// <summary>A scene widget that binds ≥1 real atlas texture handle:
/// its name, class id, and the bound layers (handle + decoded rect +
/// alpha) regardless of binding shape (0x22 field or the 0x58 block).
/// Raw + complete; the consumer assigns role/state.</summary>
public sealed record ParagonBoundWidget(
    string Name, uint ClassId, IReadOnlyList<NodeElement> Layers);

/// <summary>The paragon board chrome render model (§10.16). The main
/// board (scene 657304) composes a 5-piece chrome: a centre
/// background field (<see cref="BackgroundCenter"/>) plus a
/// 4-cardinal-side rim — <see cref="BorderTop"/> /
/// <see cref="BorderBottom"/> share one band texture,
/// <see cref="BorderLeft"/> / <see cref="BorderRight"/> share another.
/// <see cref="BoardSelectChrome"/> carries the board-select panel's
/// preview frame + filigree band from scene 964599. The centre field
/// (<see cref="BackgroundCenter"/>) carries an authored
/// <c>1200×1200</c> reference-unit rect (decoded FR-C16 R7 — its
/// <c>nWidth</c>/<c>nHeight</c> are tag-2-encoded; pre-R7 the 0x22-only
/// parser read no records for this widget and so reported an all-zero
/// rect — an artifact, now corrected). The 4 rim sides bind no
/// <c>nWidth</c>/<c>nHeight</c> at all, so they remain
/// engine-positioned at native pixel size (their rect stays zero,
/// faithfully). The rim-band handles
/// (<see cref="BorderTop"/>'s, etc.) are scene-bound via the
/// standard <c>0x6B1C5D9C</c> texture-handle field but resolve
/// through a non-icon-catalog path CASC does not currently index, so
/// their <see cref="NodeElement.AtlasSno"/> /
/// <see cref="NodeElement.NativeWidth"/> /
/// <see cref="NodeElement.NativeHeight"/> are <c>0</c> (consumer
/// uses a different texture-resolution path or a procedural
/// equivalent). Any rim animation (the engine-animated "fire" the
/// game shows on the rim) is **engine-internal** — scene data has
/// no blend mode, frame order, or timing for it; per CL-28 / CL-30
/// no-fabrication discipline CASC does not surface a fabricated
/// sequence.</summary>
public sealed record ParagonBoardChrome(
    NodeElement BackgroundCenter,
    NodeElement BorderTop,
    NodeElement BorderRight,
    NodeElement BorderBottom,
    NodeElement BorderLeft,
    IReadOnlyList<NodeElement> BoardSelectChrome,
    IReadOnlyList<TiledStyleBinding> TiledStyleBindings);

/// <summary>
/// FR-C16 R14 — the engine's per-node render PROGRAM as a <b>flat,
/// truly-z-ordered list of atomic components</b>. The consumer is a pure
/// interpreter with one rule:
/// <code>
/// foreach (var c in recipe.Components)            // already in paint order
///     if (c.Activation.Evaluate(facts))           // facts = consumer's node state
///         draw(c.ImageHandle, c.Rect, c.Alpha);
/// </code>
/// No mutual-exclusion, substitution, slot-tiebreak, or per-state grouping
/// is left to the consumer — each component carries its <b>exact</b>
/// activation (the per-rarity / per-type / per-selection-state condition,
/// parent ∧ child combined) and its <b>true draw order</b>
/// (<see cref="ParagonNodeComponent.ZOrder"/> = index; the per-rarity disc
/// sits at the base-disc position, below the symbol — not the template's
/// appended scene z). The only consumer-owned input is
/// <c>computeFacts(boardState)</c>.
/// <br/><br/>
/// <b>Why the activation is engine-sourced, not consumer-authored:</b> the
/// scene stores no per-widget condition field (FR-C16 R10, exhaustive); the
/// engine binds visibility to state by widget/asset name in its compiled
/// <c>ParagonBoardUI</c> controller (named data-source binding —
/// <c>ParagonNodeIsPurchased</c> is registry-confirmed, R12/R13). CASC
/// decodes that naming convention into the typed
/// <see cref="ParagonNodeComponent.Activation"/>; each carries its
/// <see cref="NodeActivation.Source"/> provenance per
/// <c>feedback_widget-name-not-role</c>.
/// </summary>
public sealed record ParagonNodeRecipe(IReadOnlyList<ParagonNodeComponent> Components);

/// <summary>
/// FR-C16 R14 — one atomic drawable component of a <see cref="ParagonNodeRecipe"/>:
/// a single atlas frame drawn at its rect/alpha when its
/// <see cref="Activation"/> holds. The flattening of the former
/// layer/disc/composite nesting — every per-rarity disc, per-state ornate,
/// interior fill, glow, arrow, and symbol slot is its own component with
/// the exact combined condition.
/// </summary>
/// <param name="ZOrder">The component's true paint order — its index in
/// <see cref="ParagonNodeRecipe.Components"/> (lower draws first /
/// underneath). The per-rarity/-type disc is remapped to the base-disc
/// position (below the symbol), NOT the template widget's appended scene
/// position.</param>
/// <param name="Source">The originating widget name (and child index for a
/// template sub-record, e.g. <c>Template_Node_Magic[1]</c>) — verbatim, for
/// traceability; never a normalized role.</param>
/// <param name="ImageHandle">The component's atlas frame handle. <c>0</c> ⇒
/// a runtime-filled slot (e.g. <c>Node_Icon</c> → the node's
/// <c>HIconMask</c>) — drawn, not dropped, so the program is complete.</param>
/// <param name="Rect">The component's authored reference-unit rect inset
/// (<see langword="default"/> ⇒ inherits the cell; negative ⇒ overscan).</param>
/// <param name="Alpha">The component's <c>dwAlpha</c> opacity byte.</param>
/// <param name="Activation">The exact engine-sourced condition gating this
/// component (rarity/type ∧ selection-state combined). Evaluate against the
/// consumer-computed fact set; see <see cref="NodeActivation"/>.</param>
/// <param name="DefaultActive">The layer's authored <c>bActive</c> — its
/// default (resting) visibility. <see langword="true"/> (or unbound) ⇒ shown
/// in the default/unselected state; <see langword="false"/> ⇒ default-off,
/// shown only when the engine toggles it for a runtime state (selected,
/// purchased, located, …). This is the field that disambiguates a swap pair:
/// the <c>bActive=1</c> disc is the unselected variant, the <c>bActive=0</c>
/// disc the selected one.</param>
/// <param name="Tint">The layer's authored <c>rgbaTint</c> (multiply colour),
/// or <see langword="null"/> for no tint. E.g. the glyph-socket base disc is
/// drawn through a grey <c>0xFF8A8A8A</c> tint.</param>
public sealed record ParagonNodeComponent(
    int ZOrder,
    string Source,
    uint ImageHandle,
    WidgetRect Rect,
    byte Alpha,
    NodeActivation Activation,
    bool DefaultActive,
    RgbaTint? Tint);

/// <summary>
/// FR-C17 — the engine's paragon-board grid-layout metric, in the
/// authored design-canvas reference units. The consumer maps a board
/// cell's grid coordinate to a canvas position with
/// <c>canvasPos = (gridX·Pitch, gridY·Pitch)</c> (relative to the grid
/// origin) and scales the whole canvas
/// <c>CanvasWidth×CanvasHeight → render resolution</c>. This replaces
/// the consumer's empirical pitch (the FR-C7-measured ~67.7px /
/// arbitrary CellPx) with the engine's authored metric.
/// <br/><br/>
/// All values are read from the live UI scene (game data), not
/// hard-coded: <see cref="CanvasWidth"/>/<see cref="CanvasHeight"/> from
/// the root <c>ParagonBoard_main</c> widget, <see cref="CellExtent"/>
/// from the <c>Template_Node_Common</c> node-cell widget. The board's
/// logical grid (dimensions + which cell holds which node) is the
/// per-board <see cref="ParagonBoardDefinition"/> (<c>Width</c> +
/// <c>Cells</c>); this record is the global canvas/cell metric shared
/// across boards.
/// <br/><br/>
/// <b>Pitch = CellExtent</b> (cells are laid out adjacent, no extra
/// inter-cell gap beyond the per-cell art inset). Validated against the
/// owner's in-game measurement: the empirical ~67.7px pitch =
/// <c>CellExtent (100) × render-scale (≈0.677 at the consumer's board
/// width)</c>, so the authored 100-unit cell reproduces the observed
/// spacing exactly. If a future build introduces an explicit inter-cell
/// gap it would surface as <c>Pitch ≠ CellExtent</c> (engine
/// render-code RE — the <c>UIParagonBoardStyle</c> class is a style
/// wrapper with no grid-layout fields, so any gap lives in the
/// engine's grid-layout code, not the scene data).
/// </summary>
/// <param name="CanvasWidth">Design-canvas width in reference units
/// (<c>ParagonBoard_main.nWidth</c>; 1920 on the current build).</param>
/// <param name="CanvasHeight">Design-canvas height
/// (<c>ParagonBoard_main.nHeight</c>; 1200).</param>
/// <param name="CellExtent">Node-cell extent in reference units
/// (<c>Template_Node_Common.nWidth</c> = <c>nHeight</c>; 100).</param>
/// <param name="Pitch">Cell-to-cell step in reference units. Equals
/// <see cref="CellExtent"/> on the current build (adjacent cells).</param>
public sealed record ParagonBoardGrid(
    int CanvasWidth,
    int CanvasHeight,
    int CellExtent,
    int Pitch);

/// <summary>The UI design space the raw rects are authored in (decoded
/// from the root <c>ParagonBoard_main</c> widget; verified
/// 1920×1200).</summary>
public readonly record struct CanvasRef(int Width, int Height);

/// <summary>An authored <c>DT_INT</c> bindable rect, exactly as stored
/// (UI reference units; no pixels). Pitch is derived, not stored.</summary>
public readonly record struct WidgetRect(
    int Left, int Right, int Top, int Bottom, int Width, int Height);

/// <summary>A drawable element of the paragon node composite recipe:
/// its raw texture handle (<c>== TexFrame.ImageHandle</c>;
/// <c>0xFFFFFFFF</c>/0 ⇒ none, never pre-resolved), the atlas SNO that
/// hosts the frame, the frame's native pixel size (the layer's
/// authoritative draw extent when no widget rect is authored — the
/// engine draws at native px size centred on the disc anchor), the
/// authored reference-unit rect (<see langword="default"/> ⇒ inherits
/// <c>NodeTemplate</c> at native px), and raw <c>dwAlpha</c>.</summary>
public readonly record struct NodeElement(
    uint TextureHandle, WidgetRect Rect, byte Alpha,
    int AtlasSno = 0,
    int NativeWidth = 0, int NativeHeight = 0);

/// <summary>A raw bound <c>DT_RGBACOLOR</c> value.</summary>
public readonly record struct RgbaTint(byte R, byte G, byte B, byte A);

/// <summary>
/// Library-derived <b>unitless</b> render ratios (the primary consume
/// path; C-c). All are fractions, never pixels — the consumer applies
/// its own resolution/zoom basis (permanently consumer-owned).
/// </summary>
/// <param name="Provisional"><see langword="true"/> until the ratios
/// reproduce the §10.8 67.7 px/grid anchor; consumers must not treat
/// the values as final while this is set.</param>
/// <param name="PitchRef">Node-centre pitch ÷ canvas reference.</param>
/// <param name="DiscRef">Disc element size ÷ canvas reference.</param>
/// <param name="OrnateOverDisc">Ornate frame ÷ disc.</param>
/// <param name="SymbolOverDisc">Symbol ÷ disc.</param>
/// <param name="GreyRingOverDisc">Grey rim ring ÷ disc.</param>
/// <param name="SocketRingOverDisc">Socket pulse ring ÷ disc.</param>
public readonly record struct RenderRatios(
    bool Provisional,
    double PitchRef, double DiscRef,
    double OrnateOverDisc, double SymbolOverDisc,
    double GreyRingOverDisc, double SocketRingOverDisc);

/// <summary>
/// One row of the §7.2 state contract: the back→front layer list for a
/// (rarity, state) or a kind/overlay. Rows the schema enumerates but
/// no scene widget binds (e.g. <c>overlay.selectionRing</c> — the
/// selected-state red ring lives composited inside each per-rarity
/// selected variant, not as a separate overlay) carry
/// <see cref="Layers"/> empty and <see cref="Unresolved"/> =
/// <see langword="true"/>.
/// </summary>
/// <param name="RarityOverride">0/2/3/4, or −1 for
/// socket/gate/start/overlay.</param>
/// <param name="State">The canonical §7.2 key (e.g. <c>unselected</c>,
/// <c>socket.unselected</c>, <c>overlay.connectorBar</c>).</param>
/// <param name="Layers">Back→front draw layers. Empty when
/// <see cref="Unresolved"/> is <see langword="true"/>.</param>
/// <param name="Tint">The bound per-rarity×state <c>rgbaTint</c>
/// (<see langword="null"/> if none ⇒ fixed-shader, consumer recipe).</param>
/// <param name="LitTint">The second <c>DT_RGBACOLOR</c> (relit colour)
/// on <c>selected</c> keys, if authored.</param>
/// <param name="Animation">Pulse/rotate spec, or
/// <see langword="null"/>.</param>
/// <param name="Unresolved"><see langword="true"/> when the row is
/// enumerated by the schema but no scene widget binds its art (the
/// art lives composited inside another row's bindings, e.g.
/// <c>overlay.selectionRing</c>'s red ring is baked into each
/// per-rarity selected composite). The per-record completeness gate
/// (§10.14) permits empty <see cref="Layers"/> exactly when this is
/// <see langword="true"/>.</param>
public readonly record struct StateElements(
    int RarityOverride, string State,
    IReadOnlyList<NodeElement> Layers,
    RgbaTint? Tint, RgbaTint? LitTint, AnimSpec? Animation,
    bool Unresolved = false);

/// <summary>An authored animation parameter set (raw; the consumer
/// bakes one representative static frame).</summary>
public readonly record struct AnimSpec(
    string Kind, double PeriodSeconds, double MinValue, double MaxValue);

/// <summary>
/// Internal helper: project a decoded <see cref="UiScene"/> to the
/// typed <see cref="ParagonRenderLayout"/>. Kept separate so the
/// generic decode (<see cref="UiScene"/>) stays policy-free.
/// </summary>
internal static class ParagonRenderProjection
{
    private static readonly uint FnLeft   = Diablo4.FieldHash("nLeft");
    private static readonly uint FnRight  = Diablo4.FieldHash("nRight");
    private static readonly uint FnTop    = Diablo4.FieldHash("nTop");
    private static readonly uint FnBottom = Diablo4.FieldHash("nBottom");
    private static readonly uint FnWidth  = Diablo4.FieldHash("nWidth");
    private static readonly uint FnHeight = Diablo4.FieldHash("nHeight");

    private static int Val(UiWidget w, uint fieldHash) => Val(w.Fields, fieldHash);

    private static int Val(IReadOnlyList<UiField> fields, uint fieldHash)
    {
        foreach (var f in fields)
            if (f.FieldHash == fieldHash && f.HasValue) return (int)f.RawValue;
        return 0;
    }

    private static WidgetRect Rect(UiWidget? w) =>
        w is null ? default : RectOf(w.Fields);

    private static WidgetRect RectOf(IReadOnlyList<UiField> fields) =>
        new(Val(fields, FnLeft), Val(fields, FnRight), Val(fields, FnTop),
            Val(fields, FnBottom), Val(fields, FnWidth), Val(fields, FnHeight));

    private static readonly uint FbActive = Diablo4.FieldHash("bActive");
    // Anchoring field for placement resolution. The node widgets carry both
    // eVerticalAnchoring (0x03D55658) and eHorizontalAnchoring (0x093CBAA8) —
    // both =3 for centred widgets, =0 for absolute. (The latter was previously
    // mislabelled "eGroupType" in KnownFieldNames; the real eGroupType hashes
    // to 0x05862894, caught by the field-hash sanity check.) Both axes agree
    // on the node-recipe widgets, so eVerticalAnchoring carries the decision.
    private static readonly uint FeAnchor = Diablo4.FieldHash("eVerticalAnchoring");
    private static readonly uint FrgbaTint = Diablo4.FieldHash("rgbaTint");
    private static readonly uint FdwAlphaField = Diablo4.FieldHash("dwAlpha");

    // The authored bActive default visibility: bActive == 0 (explicitly bound)
    // ⇒ default-OFF; bActive == 1 or unbound ⇒ default-ON. This is the field
    // that disambiguates a swap pair (the bActive=1 variant is the
    // unselected/default one).
    private static bool DefaultActiveOf(IReadOnlyList<UiField> fields)
    {
        foreach (var f in fields)
            if (f.FieldHash == FbActive && f.HasValue) return f.RawValue != 0;
        return true;
    }

    // The authored rgbaTint (multiply colour), stored ARGB; null if unbound.
    private static RgbaTint? TintOf(IReadOnlyList<UiField> fields)
    {
        foreach (var f in fields)
            if (f.FieldHash == FrgbaTint && f.HasValue)
            {
                uint v = f.RawValue;   // ARGB
                return new RgbaTint((byte)(v >> 16), (byte)(v >> 8), (byte)v, (byte)(v >> 24));
            }
        return null;
    }

    private static byte AlphaOf(IReadOnlyList<UiField> fields)
    {
        foreach (var f in fields)
            if (f.FieldHash == FdwAlphaField && f.HasValue) return (byte)f.RawValue;
        return 0xFF;
    }

    // FR-C16 R14 — resolve a widget's authored insets + size + anchoring to an
    // absolute placement rect in cell-reference space, so the component is
    // directly drawable (no consumer layout logic). eGroupType decodes the
    // anchoring mode: 0 = absolute top-left (the directional arrows/connectors
    // position at nLeft/nTop); 3/other = centered/stretch in the parent cell —
    // an explicit nWidth/nHeight centers at that size (e.g. the 120² Located
    // ring → centred, overscanning the 100 cell), otherwise the element
    // stretches to the cell minus its insets (the inset-7 disc → 86² centred).
    private static WidgetRect ResolvePlacement(in WidgetRect r, int groupType, int cell)
    {
        int w = r.Width > 0 ? r.Width : System.Math.Max(0, cell - r.Left - r.Right);
        int h = r.Height > 0 ? r.Height : System.Math.Max(0, cell - r.Top - r.Bottom);
        int x, y;
        if (groupType == 0)                       // absolute top-left
        {
            x = r.Left; y = r.Top;
        }
        else if (r.Width > 0 || r.Height > 0)     // centred at explicit size
        {
            x = (cell - w) / 2 + (r.Left - r.Right) / 2;
            y = (cell - h) / 2 + (r.Top - r.Bottom) / 2;
        }
        else                                      // inset-stretch
        {
            x = r.Left; y = r.Top;
        }
        return new WidgetRect(x, cell - (x + w), y, cell - (y + h), w, h);
    }

    private static bool RectIsEmpty(in WidgetRect r) =>
        r is { Left: 0, Right: 0, Top: 0, Bottom: 0, Width: 0, Height: 0 };

    // FR-C16 R11/R14 — the engine binds a widget's visibility to a runtime
    // state BY NAME in its compiled UI controller; the scene stores no
    // activation field (FR-C16 R10). This decodes the naming convention into
    // a typed activation, provenance-marked: NameConvention where the name
    // literally spells the state, EngineBehavior where the name is suggestive
    // but a fact is inferred (validate vs the owner oracle). The grey base
    // discs are the COMMON-rarity variant (eRarity 0), peer to the per-rarity
    // templates — gated [RarityCommon, …] so they don't draw over a coloured
    // disc (exactly one rarity fact holds per node).
    private static NodeActivation BaseActivation(string name)
    {
        const NodeActivationSource Nm = NodeActivationSource.NameConvention;
        const NodeActivationSource Eng = NodeActivationSource.EngineBehavior;
        static NodeActivation A(NodeActivationSource s, params NodeFact[] f) => new(f, s);
        return name switch
        {
            // Common-kind base disc (grey): the 0x1D166DC7 / 0xD3051CCA pair
            // is the COMMON unpurchased/PURCHASED disc swap (the red ring on
            // 0xD3051CCA is the purchased variant) — parallel to the per-rarity
            // disc pairs. The widget name "Node_Purchased" is literally its
            // role (owner oracle 2026-05-21).
            "Node_IconBase"             => A(Eng, NodeFact.KindCommon, NodeFact.Unpurchased),
            "Node_Purchased"            => A(Nm,  NodeFact.KindCommon, NodeFact.Purchased),
            "Rarity_Display"            => A(Eng, NodeFact.KindCommon, NodeFact.Unpurchased),
            "Purchased_Rarity_Display"  => A(Eng, NodeFact.KindCommon, NodeFact.Purchased),
            // Symbol slot (runtime HIconMask).
            "Node_Icon"                 => NodeActivation.Always,
            // State overlays / glows.
            "Common_Node_Revealed"      => A(Nm, NodeFact.Revealed),
            // The GlyphNodeGlow ring is a glyph-SOCKET element (its name) —
            // gated to the socket kind, not drawn on every node's
            // purchased/revealed state (the "red ring only on a socket").
            "GlyphNodeGlow_Revealed"    => A(Eng, NodeFact.KindSocket, NodeFact.Revealed),
            "GlyphNodeGlow_Purchased"   => A(Eng, NodeFact.KindSocket, NodeFact.Purchased),
            "NodeAvailableGlow"         => A(Nm, NodeFact.Available),
            "Node_Purchasable"          => A(Nm, NodeFact.Purchasable),
            "Node_EquipGlow"            => A(Nm, NodeFact.Equipped),
            "Node_SearchResultHighlight"=> A(Nm, NodeFact.SearchMatch),
            "Node_Located"              => A(Nm, NodeFact.Located),
            "Node_Tutorial_Highlight"   => A(Nm, NodeFact.Tutorial),
            // Glyph-socket node composition (base disc, beads, glow) — drawn
            // for the socket KIND (the socket node always shows its base),
            // not merely when a glyph is currently socketed.
            // (Usage_Slot_* is handled in NodeRecipe as the socket type-disc
            // carrier — remapped into the base-disc band — not here.)
            "Node_Glyph" or "Node_Glyph_Usage_Stack" => A(Eng, NodeFact.KindSocket),
            // Purchased-node add-on: arrows point to PURCHASABLE neighbours,
            // connectors bridge to already-PURCHASED neighbours; both only on a
            // purchased node (owner oracle 2026-05-21).
            "Arrow_Top"      => A(Eng, NodeFact.Purchased, NodeFact.NeighbourPurchasableTop),
            "Arrow_Right"    => A(Eng, NodeFact.Purchased, NodeFact.NeighbourPurchasableRight),
            "Arrow_Bottom"   => A(Eng, NodeFact.Purchased, NodeFact.NeighbourPurchasableBottom),
            "Arrow_Left"     => A(Eng, NodeFact.Purchased, NodeFact.NeighbourPurchasableLeft),
            "Connector_Top"    => A(Eng, NodeFact.Purchased, NodeFact.NeighbourPurchasedTop),
            "Connector_Right"  => A(Eng, NodeFact.Purchased, NodeFact.NeighbourPurchasedRight),
            "Connector_Bottom" => A(Eng, NodeFact.Purchased, NodeFact.NeighbourPurchasedBottom),
            "Connector_Left"   => A(Eng, NodeFact.Purchased, NodeFact.NeighbourPurchasedLeft),
            // Always-on / unrecovered (per-cell darken bg, hit-target, aura tile).
            _ => A(Eng, NodeFact.Always),
        };
    }

    // The per-rarity / per-type fact a Template_Node_* widget owns.
    private static NodeFact? TemplateFact(string name) => name switch
    {
        "Template_Node_Magic"      => NodeFact.KindMagic,
        "Template_Node_Rare"       => NodeFact.KindRare,
        "Template_Node_Legendary"  => NodeFact.KindLegendary,
        "Template_Node_Socketable" => NodeFact.KindSocket,
        "Template_Node_Starter"    => NodeFact.KindStart,
        "Template_Node_Quest"      => NodeFact.KindGate,
        _ => null,
    };

    // Decoded gate (Template_Node_Quest) composite roles. These are atlas
    // frame handles — they have no string/hash preimage, so they're named
    // constants by their decoded role. The unpurchased/purchased split is
    // data-confirmed: GateOrnateUnpurchased binds bActive=1 (the resting
    // default); the other is the purchased (red-ring) variant — the same
    // unpurchased→purchased disc swap as the rarity discs.
    private const uint GateOrnateUnpurchased = 0xC2DF4786u;
    private const uint GateOrnatePurchased   = 0x0E6B6249u;
    private const uint GateLocator           = 0x6D68F45Fu;

    private static NodeFact? GateRoleFact(uint handle) => handle switch
    {
        GateOrnateUnpurchased => NodeFact.Unpurchased,
        GateOrnatePurchased   => NodeFact.Purchased,
        GateLocator           => NodeFact.Located,
        _ => null,
    };

    public static ParagonRenderLayout Project(
        UiScene scene,
        Func<uint, bool>? isTextureHandle = null,
        Func<uint, (int AtlasSno, int W, int H)>? frameLookup = null)
    {
        UiWidget? ByName(string n) =>
            scene.Widgets.FirstOrDefault(w => w.Name == n);

        (int AtlasSno, int NativeW, int NativeH) Frame(uint handle) =>
            handle == 0 || handle == 0xFFFFFFFFu
                ? (0, 0, 0)
                : (frameLookup?.Invoke(handle) ?? (0, 0, 0));

        // FR-C8: the start/gate composite layers are bound on
        // Template_Node_Starter / Template_Node_Quest via the 0x58-block
        // shape (UiWidget.ExtraLayerValues) — not the 0x22 path. Keep the
        // ordered values that are real texture handles (validated against
        // the texture catalog when a validator is supplied by
        // Diablo4Storage; otherwise a conservative magnitude guard — the
        // 0x58 blocks also carry small int params like 20). Never
        // fabricated: every emitted handle is literally in the scene and
        // (when validated) resolves to an atlas frame.
        bool IsHandle(uint v) => v is not 0u and not 0xFFFFFFFFu &&
            (isTextureHandle?.Invoke(v) ?? v > 0xFFFFu);

        NodeElement[] LayersOf(string widget)
        {
            var x = ByName(widget);
            if (x is null) return Array.Empty<NodeElement>();
            var seen = new HashSet<uint>();
            var list = new List<NodeElement>();
            foreach (var v in x.ExtraLayerValues)
                if (IsHandle(v) && seen.Add(v))
                {
                    var (sno, fw, fh) = Frame(v);
                    list.Add(new NodeElement(
                        v, default, 0, sno, fw, fh));
                }
            return list.ToArray();
        }

        var root      = ByName("ParagonBoard_main");
        var container = ByName("ParagonNodes");          // own rect is runtime-bound
        var template  = ByName("Template_Node_Common");  // the per-node element (~100²)

        var canvas = root is null
            ? default
            : new CanvasRef(Val(root, FnWidth), Val(root, FnHeight));

        // Board rotation: decoded from ParagonNodes_BoardRotationLayer,
        // never assumed. 45° is unrepresentable — only 90° quadrants.
        // No non-zero rotation field is bound on the Warlock-Start
        // calibration view (CL-10 axis-aligned ⇒ quadrant 0); a
        // genuine per-board quadrant field is surfaced here once a
        // board that uses one is decoded (tracked: §10.12).
        const int boardRotationQuadrant = 0;

        // Decode-true render ratios. Node-centre pitch = the
        // `Template_Node_Common` box (uniform square tiling); the disc
        // draws inside it with `Node_IconBase`'s symmetric insets.
        // Normalised against the canvas HEIGHT (D4 UI height-scales at
        // super-wide; §10.7). Over-determined check PASSES (§10.8): a
        // uniform 100-ref box predicts a square uniform lattice at one
        // scale; the consumer's dual-validated anchor — autocorr
        // 67.59(X)/67.81(Y) (square) and the gate→start span 67.96 —
        // all ÷ the decode-true 100-ref pitch converge to ≈0.677 px/ref
        // (≤0.4 px), reproducing the §10.8 67.7 anchor. Hence
        // Provisional = false.
        var nodeBox = ByName("Template_Node_Common");
        var iconBase = ByName("Node_IconBase");
        double pitchUnits = nodeBox is null ? 0 : Val(nodeBox, FnWidth); // 100
        int insetX = iconBase is null ? 0 : Val(iconBase, FnLeft) + Val(iconBase, FnRight);
        double discUnits = pitchUnits > 0 ? pitchUnits - insetX : 0;     // 86
        double canvasH = canvas.Height > 0 ? canvas.Height : 0;          // 1200

        // Element draw size in ref units. The disc/ornate/symbol/pulse
        // elements have no own nWidth — they fill the node box minus
        // their symmetric insets (decode-true, §10.11). A missing
        // widget ⇒ not bound in this scene (e.g. the grey rim ring is
        // app-drawn — 0, not fabricated).
        double ElemSize(string n)
        {
            var x = ByName(n);
            if (x is null || pitchUnits <= 0) return 0;
            int w = Val(x, FnWidth);
            if (w > 0) return w;
            return pitchUnits - (Val(x, FnLeft) + Val(x, FnRight)); // fills parent
        }

        double ornateSz = ElemSize("NodeAvailableGlow");    // gold ornate (fills box → 100)
        double symbolSz = ElemSize("Node_Icon");            // per-class symbol
        double socketSz = ElemSize("GlyphNodeGlow_Revealed"); // socket pulse ring
        double greySz   = ElemSize("Node_GreyRing");        // app-drawn/absent ⇒ 0

        bool anchored = pitchUnits > 0 && canvasH > 0;
        double Over(double v) => discUnits > 0 && v > 0 ? v / discUnits : 0;
        var ratios = new RenderRatios(
            Provisional: !anchored,
            PitchRef: anchored ? pitchUnits / canvasH : 0,               // 100/1200
            DiscRef: anchored && discUnits > 0 ? discUnits / canvasH : 0,// 86/1200
            OrnateOverDisc: Over(ornateSz),                              // 100/86
            SymbolOverDisc: Over(symbolSz),                              // 100/86
            GreyRingOverDisc: Over(greySz),                              // 0 (app-drawn)
            SocketRingOverDisc: Over(socketSz));                         // 100/86

        // Per-state texture binding (§10.11, decode-true): node
        // textures bind via the texture-handle DT type 0x6B1C5D9C on
        // specifically-named widgets. Disc/ornate/pulse are the only
        // elements bound in ParagonBoard; rarity differs by rgbaTint
        // (shader, §2.3); the overlay.* layers are app-drawn (absent
        // from scene data — FR §2.5).
        const uint TexHandleType = 0x6B1C5D9Cu;

        NodeElement Elem(string widgetName)
        {
            var x = ByName(widgetName);
            if (x is null) return default;
            uint handle = 0;
            byte alpha = 0;
            foreach (var f in x.Fields)
            {
                if (f.HasValue && f.TypeHash == TexHandleType &&
                    f.RawValue is not 0 and not 0xFFFFFFFF && handle == 0)
                    handle = f.RawValue;
                if (f.HasValue && f.FieldHash == Diablo4.FieldHash("dwAlpha"))
                    alpha = (byte)f.RawValue;
            }
            var (sno, fw, fh) = Frame(handle);
            return new NodeElement(handle, Rect(x), alpha, sno, fw, fh);
        }

        var disc   = Elem("Node_IconBase");          // 0x1D166DC7
        var pulse  = Elem("GlyphNodeGlow_Revealed"); // 0xBED4CF21 (socket bead ring — unselected/revealed)
        var pulseSocketed = Elem("GlyphNodeGlow_Purchased"); // 0xBED4CF21 (same handle, scene-bound under the socketed-state widget — FR-C12 R2)
        // FR-C12 R2: Node_Located and Node_EquipGlow bind their atlas
        // handles via the 0x58-block shape (ExtraLayerValues), NOT via
        // a 0x22 texture-handle field — Elem() returns default for
        // them. LayersOf() enumerates the 0x58 block.
        var locatedLayers   = LayersOf("Node_Located");    // 0x87A89F86
        var equipGlowLayers = LayersOf("Node_EquipGlow");  // 0xFC806F42

        // FR-C12 R2 owner correction (atlas-frame visual oracle): the
        // on-board per-node SOCKET composite uses three atlas handles
        // bound on Usage_Slot_2 (the right-side equipped-glyph panel
        // widget) — the engine reuses the same atlas frames for both
        // contexts. Per owner inspection of the atlas frames in
        // 2DUI_Paragon_transparentElements, plus CASC's own frame
        // extraction (FR-C12 R2 socket-composite-stack.png), the
        // back-to-front ordering is:
        //   0xF6443089 (135² ornate outer socket disk — black frame
        //                with red gem inset, center opening)
        //   0xBED4CF21 (135² red glowing bead ring — the "pulsing"
        //                animation layer; already surfaced via
        //                GlyphNodeGlow_Revealed's field binding)
        //   0x23F487F3 (136² inner spike-frame with center depression
        //                where the per-node HIconMask glyph icon sits)
        // The narrow CL-33 §1 probe filtered widget names by
        // Glyph/Socket/Ring/Pulse and missed the outer disk + inner
        // spike-frame because their binding widget (Usage_Slot_2)
        // doesn't match those tokens — CL-31→32 lesson applied to the
        // socket axis.
        const uint SocketOuterDisk = 0xF6443089u;
        const uint SocketInnerWell = 0x23F487F3u;

        // Resolve these handles from any scene 657304 widget that binds
        // them (Usage_Slot_2's 0x58 block) — the layer is a pure atlas
        // reference; no per-node rect is authored (the on-board socket
        // composites at the disc anchor, like the rarity-base layer).
        NodeElement LayerByHandle(uint handle)
        {
            if (handle == 0) return default;
            foreach (var w in scene.Widgets)
            {
                foreach (var f in w.Fields)
                    if (f.HasValue && f.RawValue == handle)
                    {
                        var (sno, fw, fh) = Frame(handle);
                        return new NodeElement(handle, default, 0xFF, sno, fw, fh);
                    }
                foreach (var v in w.ExtraLayerValues)
                    if (v == handle)
                    {
                        var (sno, fw, fh) = Frame(handle);
                        return new NodeElement(handle, default, 0xFF, sno, fw, fh);
                    }
            }
            return default;
        }
        var socketOuterDisk = LayerByHandle(SocketOuterDisk);
        var socketInnerWell = LayerByHandle(SocketInnerWell);

        static NodeElement[] L(params NodeElement[] xs) =>
            xs.Where(e => e.TextureHandle != 0).ToArray();

        // FR-C10 node composite recipe (§10.15). Each rarity composites
        // grey-base + (optional rarity-specific interior fill) +
        // (selected variant: a pre-composited full-disc frame with the
        // red ring at the disc perimeter, bound in the scene). Every
        // selected state references a scene-bound composite — no
        // standalone engine-internal ring is added to a per-rarity row.
        // Per-rarity handle roles, build-stable on 3.0.2.71886, sourced
        // from atlas-frame visual inspection + owner visual oracle for
        // Magic (FR-C10 R1) and the FR-C10 R2 root-cause analysis.
        const uint MagicInteriorFill   = 0xFEC31E48u; // 135² blue interior — owner-confirmed
        const uint MagicSelComposite   = 0x72C29402u; // 154² blue disc + perimeter ring composite (Template_Node_Magic 0x58 block)
        const uint MagicBaseComposite  = 0x621CB6FFu; // 153² (FR-C12 R2) Template_Node_Magic 0x58 first layer — magic-unselected disc-composite previously missed
        const uint RareInteriorFill    = 0xF8373491u; // 135² rare interior
        const uint RareOrnateUnsel     = 0xB71BD068u; // 154² yellow ornate frame
        const uint RareOrnateSel       = 0x03EDABABu; // 153² yellow ornate + perimeter ring composite
        const uint LegInteriorFill     = 0x006ED182u; // 136² legendary interior
        const uint LegOrnateUnsel      = 0x232DF7F9u; // 189² orange spike ornate frame
        const uint LegOrnateSel        = 0xBD27FB7Cu; // 189² orange spike ornate + perimeter ring composite
        const uint LegClassOverlay     = 0xCC3E3B25u; // 135² (FR-C12 R2) Template_Node_Legendary 0x58 layer in 2DUI_ParagonNodesIcons_Rogue — class-specific paragon node overlay (the first class-specific atlas surfaced in §10.15)

        NodeElement Layer(uint handle)
        {
            if (handle == 0) return default;
            var (sno, fw, fh) = Frame(handle);
            return new NodeElement(handle, default, 0xFF, sno, fw, fh);
        }

        // Decode-true: only surface a per-rarity handle when its
        // Template_Node_* 0x58 block actually contains it. The library
        // never fabricates a layer; a future season that drops or
        // renames a handle leaves the row honestly shorter.
        var magicBlockHandles = new HashSet<uint>(
            LayersOf("Template_Node_Magic").Select(e => e.TextureHandle));
        var rareBlockHandles  = new HashSet<uint>(
            LayersOf("Template_Node_Rare").Select(e => e.TextureHandle));
        var legBlockHandles   = new HashSet<uint>(
            LayersOf("Template_Node_Legendary").Select(e => e.TextureHandle));

        // Common's selected composite lives on a separate widget
        // (`Node_Purchased`, the "allocated/spent" indicator) rather
        // than the per-rarity Template_Node_* block — scene-bound, not
        // engine-internal.
        var purchased = Elem("Node_Purchased"); // 0xD3051CCA — 153² dark disc + perimeter ring

        NodeElement[] RarityComposite(int rar, bool selected)
        {
            var layers = new List<NodeElement>();
            if (disc.TextureHandle != 0) layers.Add(disc);
            switch (rar)
            {
                case 0: // Common — base disc; selected swaps to Node_Purchased's composite (dark disc + perimeter ring).
                    if (selected && purchased.TextureHandle != 0)
                        layers.Add(purchased);
                    break;
                case 2: // Magic — base + blue interior; selected adds Template_Node_Magic's 0x72C29402 composite (blue disc + perimeter ring).
                    if (magicBlockHandles.Contains(MagicBaseComposite))
                        layers.Add(Layer(MagicBaseComposite));   // FR-C12 R2 — 153² magic base composite previously missed
                    if (magicBlockHandles.Contains(MagicInteriorFill))
                        layers.Add(Layer(MagicInteriorFill));
                    if (selected && magicBlockHandles.Contains(MagicSelComposite))
                        layers.Add(Layer(MagicSelComposite));
                    break;
                case 3: // Rare — base + interior + ornate (swapped on selected for the ornate + perimeter-ring composite).
                    if (rareBlockHandles.Contains(RareInteriorFill))
                        layers.Add(Layer(RareInteriorFill));
                    var rareOrnate = selected ? RareOrnateSel : RareOrnateUnsel;
                    if (rareBlockHandles.Contains(rareOrnate))
                        layers.Add(Layer(rareOrnate));
                    break;
                case 4: // Legendary — base + interior + larger spike ornate (swapped on selected).
                    if (legBlockHandles.Contains(LegInteriorFill))
                        layers.Add(Layer(LegInteriorFill));
                    var legOrnate = selected ? LegOrnateSel : LegOrnateUnsel;
                    if (legBlockHandles.Contains(legOrnate))
                        layers.Add(Layer(legOrnate));
                    if (legBlockHandles.Contains(LegClassOverlay))
                        layers.Add(Layer(LegClassOverlay));     // FR-C12 R2 — 135² class-specific layer in 2DUI_ParagonNodesIcons_Rogue (first class-specific atlas surfaced in §10.15)
                    break;
            }
            return layers.ToArray();
        }

        var states = new List<StateElements>(21);

        // Rows 1–8: rarity {0,2,3,4} × {unselected,selected}. Recipe
        // per §10.15: grey-base + (rarity-specific interior fill if
        // bound) + (selected-state composite — Template_Node_<rarity>'s
        // 0x58-block selected-variant for Magic/Rare/Legendary, or
        // Node_Purchased's binding for Common — each carrying the red
        // perimeter ring in its disc art). Per-rarity tint stays null —
        // the per-rarity colour comes from the bound interior-fill
        // atlas frame, not a shader tint on a shared disc.
        foreach (var rar in new[] { 0, 2, 3, 4 })
            foreach (var sel in new[] { false, true })
                states.Add(new StateElements(
                    rar, sel ? "selected" : "unselected",
                    L(RarityComposite(rar, sel)),
                    Tint: null, LitTint: null, Animation: null));

        // Rows 9–11: socket (FR-C12 R3 — game-recipe corrected). The
        // socket-class node has its OWN ornate outer disk and does NOT
        // composite the shared per-rarity grey-base 0x1D166DC7 — the
        // engine's state dispatch for socket cells never references
        // Node_IconBase (owner visual oracle on the rebuilt app: the
        // grey base would project ~9.5px beyond the ornate disk's
        // silhouette as a thin grey ring, which the game NEVER renders
        // on a socket in any state). FR-C12 R2 INCORRECTLY prepended
        // disc on the assumption it was universal; CL-35 drops it from
        // the socket rows.
        //
        // The on-board socket composite is exactly three layers
        // (back→front): F6443089 (ornate outer disk) → BED4CF21 (red
        // glowing bead ring — the pulsing animation layer) → 23F487F3
        // (inner spike-frame with center depression — the per-node
        // HIconMask glyph icon seats here). All three are scene-bound
        // (outer disk + inner well on Usage_Slot_2's 0x58-block; bead
        // ring on GlyphNodeGlow_Revealed for unselected/selected and
        // GlyphNodeGlow_Purchased for socketed — the engine reuses the
        // same atlas frames for the side-panel equipped-glyph display
        // and the on-board per-node render). The three-layer recipe
        // is identical across all three states; per-state activation
        // policy (pulse animation on/off, placed-glyph-icon overlay at
        // the inner-well centre depression on .socketed) is consumer-
        // side per FR-C7 §6 (CL-36 owner visual-oracle confirmation:
        // .selected = bead-ring static @ opacity 1.0; .socketed =
        // .selected + glyph icon overlay; .unselected = bead-ring
        // pulse 0.15↔1.0 sine, 4 s period — all consumer-side).
        states.Add(new StateElements(-1, "socket.unselected",
            L(socketOuterDisk, pulse, socketInnerWell),
            null, null, Animation: null));
        states.Add(new StateElements(-1, "socket.selected",
            L(socketOuterDisk, pulse, socketInnerWell),
            null, null, null));
        states.Add(new StateElements(-1, "socket.socketed",
            L(socketOuterDisk, pulseSocketed, socketInnerWell),
            null, null, null));

        // Rows 12–15: gate / start. CORRECTION (FR-C8, CL-23): the
        // FR-C7-era "no distinct gate/start texture is bound" was wrong —
        // it followed from the §10.3 0x22 scan missing the 0x58-block
        // bindings. The composites ARE in ParagonBoard 657304:
        // Template_Node_Starter → filigree 0xA0F996FE + grey hexagon
        // 0xF8312CA8; Template_Node_Quest → filigree 0xA0F996FE + ornate
        // squares 0xC2DF4786 / 0x0E6B6249. The per-node SYMBOL on top is
        // the ParagonNode HIconMask (already exposed; correctly NOT in
        // the scene). Layers carry the decoded ordered scene handles
        // (back→front); per-layer rect/scale/tint and the exact
        // unselected↔selected ornate-square split are not decoded → left
        // default (honest, not fabricated — consumer owns the shader
        // brightness pass per FR-C7 §6, and the symbol via HIconMask).
        var startLayers = LayersOf("Template_Node_Starter");
        var gateLayers  = LayersOf("Template_Node_Quest");
        states.Add(new StateElements(-1, "gate.unselected",
            gateLayers.Length > 0 ? gateLayers : L(disc), null, null, null));
        states.Add(new StateElements(-1, "gate.selected",
            gateLayers.Length > 0 ? gateLayers : L(disc), null, null, null));
        states.Add(new StateElements(-1, "start.unselected",
            startLayers.Length > 0 ? startLayers : L(disc), null, null, null));
        states.Add(new StateElements(-1, "start.selected",
            startLayers.Length > 0 ? startLayers : L(disc), null, null, null));

        // Rows 16–18: the node-overlay states (§10.13). connectorBar
        // and pointerTriangle bind their scene widgets via the standard
        // 0x6B1C5D9C-typed texture-handle field on the 0x22 path
        // (handle + decoded Rect):
        //   - connectorBar   → Connector_{T,R,B,L} (0x77ECA3A8 /
        //                     0x288DE11F)
        //   - pointerTriangle→ Arrow_{T,R,B,L} (0xD51CAB25, 0x6D3CB8DE,
        //                     0x8EEAC178, 0xB6D8C741)
        // overlay.selectionRing has no scene-widget binding because the
        // selected-state red ring is not a separate overlay — it is
        // baked into each per-rarity selected composite (Template_Node_*'s
        // 0x58-block selected-variant; Node_Purchased's binding for
        // Common — see §10.15). The row stays in the §7.2 enumeration
        // for schema completeness and is marked Unresolved=true so the
        // per-record completeness gate (§10.14) recognises this as
        // intentional, not a projection drop.
        NodeElement[] Overlay(params string[] widgets) =>
            L(widgets.Select(Elem).ToArray());

        states.Add(new StateElements(-1, "overlay.selectionRing",
            Array.Empty<NodeElement>(), null, null, null,
            Unresolved: true));
        states.Add(new StateElements(-1, "overlay.connectorBar",
            Overlay("Connector_Top", "Connector_Right",
                    "Connector_Bottom", "Connector_Left"),
            null, null, null));
        states.Add(new StateElements(-1, "overlay.pointerTriangle",
            Overlay("Arrow_Top", "Arrow_Right",
                    "Arrow_Bottom", "Arrow_Left"),
            null, null, null));
        // Row 19: the selectable/available glow (FR-C8 R9, CL-25). The
        // yellow pulsing perimeter outline drawn on every UNSELECTED
        // node that is selectable (cardinally adjacent to a selected
        // node), ANY rarity — `NodeAvailableGlow` (handle 0x4A901508 +
        // authored Rect, single perimeter frame). This is what FR-C7
        // mis-labelled as the r3/r4 "ornate"; it is a selectable-STATE
        // overlay, not a rarity decoration — now its own row, distinct
        // from the genuine Rare/Legendary ornate (rareL/legL above).
        // Rows 20–21 (FR-C12 R2): two state overlays surfaced by the
        // broad scene-657304 probe that the prior narrow widget-name
        // filter (Glyph/Socket/Ring/Pulse) missed.
        // - overlay.locatedHighlight: Node_Located (0x87A89F86, 135²) —
        //   bound on a Node_*-named widget without socket/ring tokens.
        //   In-game role TBD (likely a "node located / search-result-
        //   adjacent" highlight applied on top of the per-rarity
        //   composite); surfaced honestly so the consumer can audit and
        //   the row-completeness gate keeps it covered.
        // - overlay.equipGlow: Node_EquipGlow (0xFC806F42, 91×90) — a
        //   smaller node-state overlay; in-game role likely the
        //   equipped-glyph indicator drawn over socketed nodes. Same
        //   honest decode discipline.
        states.Add(new StateElements(-1, "overlay.locatedHighlight",
            locatedLayers, null, null, null));
        states.Add(new StateElements(-1, "overlay.equipGlow",
            equipGlowLayers, null, null, null));

        states.Add(new StateElements(-1, "overlay.availableGlow",
            Overlay("NodeAvailableGlow"), null, null, null));

        // FR-C11 R3 §2 / CL-33 + FR-C15 R2 / CL-39 (role retraction):
        // scene-bound binding on the `Common_Node_Revealed` widget
        // (handle `0xC1473C21` via the standard 0x6B1C5D9C texture-
        // handle field, authored rect L=R=T=B=3 inside the 100-pitch
        // `NodeTemplate` → 94×94 cell footprint; the widget records
        // `dwAlpha=0xFF` so the atlas frame's own alpha drives the
        // composite). The BINDING is correct (scene-bound; auditable
        // from the FR-C9 exhaustive widget model). CL-33 originally
        // proposed this binding as the "per-node cell background
        // tile" (the persistent darker rounded square the lighter
        // field shows through) — that role-claim was retracted in
        // CL-39 after the consumer plumbed the binding end-to-end and
        // visual inspection of `0xC1473C21`'s atlas frame revealed a
        // horizontal ember-strip / cell-reveal glow pattern, NOT a
        // clean rounded square. The actual visual role is more likely
        // a transient cell-reveal effect (consistent with the widget
        // name `_Revealed`) than the persistent per-node tile. The
        // typed field is named after the BINDING (`CommonNodeRevealedLayer`)
        // not the role; consumer + owner determine the rendering role
        // via the visual oracle. `Common_Node_BG_Black` is the sibling
        // hidden-state widget (same handle, same rect) per CL-33 — not
        // separately surfaced; reachable via the FR-C9 exhaustive view
        // if needed.
        var commonNodeRevealedLayer = Elem("Common_Node_Revealed");

        return new ParagonRenderLayout(
            ratios, canvas,
            Rect(container), Rect(template),
            boardRotationQuadrant,
            Disc: disc, Symbol: default,
            States: states,
            CommonNodeRevealedLayer: commonNodeRevealedLayer);
    }

    /// <summary>
    /// FR-C9: the exhaustive per-scene atlas-binding model. Every widget
    /// that binds ≥1 real atlas handle — from <b>either</b> shape (a
    /// 0x22 field value or a 0x58 block value, both surfaced losslessly
    /// by <see cref="UiScene"/>) — with the handle, the widget's decoded
    /// rect, and alpha. Shape-agnostic and complete: a future binding
    /// shape that <see cref="UiScene"/> still surfaces appears here too;
    /// the FR-C9 coverage gate proves none is dropped.
    /// </summary>
    public static ParagonSceneModel SceneModel(
        UiScene scene,
        Func<uint, bool> isTextureHandle,
        Func<uint, (int AtlasSno, int W, int H)>? frameLookup = null)
    {
        uint AlphaOf(UiWidget w)
        {
            foreach (var f in w.Fields)
                if (f.HasValue && f.FieldHash == Diablo4.FieldHash("dwAlpha"))
                    return (byte)f.RawValue;
            return 0;
        }

        (int Sno, int W, int H) Frame(uint h) =>
            frameLookup?.Invoke(h) ?? (0, 0, 0);

        var widgets = new List<ParagonBoundWidget>();
        foreach (var w in scene.Widgets)
        {
            var rect = Rect(w);
            var alpha = (byte)AlphaOf(w);
            var seen = new HashSet<uint>();
            var layers = new List<NodeElement>();
            foreach (var f in w.Fields)
                if (f.HasValue && isTextureHandle(f.RawValue) && seen.Add(f.RawValue))
                {
                    var (sno, fw, fh) = Frame(f.RawValue);
                    layers.Add(new NodeElement(f.RawValue, rect, alpha, sno, fw, fh));
                }
            foreach (var v in w.ExtraLayerValues)
                if (isTextureHandle(v) && seen.Add(v))
                {
                    var (sno, fw, fh) = Frame(v);
                    layers.Add(new NodeElement(v, rect, alpha, sno, fw, fh));
                }
            if (layers.Count > 0)
                widgets.Add(new ParagonBoundWidget(w.Name, w.ClassId, layers));
        }
        return new ParagonSceneModel(scene.SnoId, widgets);
    }

    /// <summary>
    /// FR-C11: the typed paragon board-chrome projection (§10.16).
    /// Surfaces the scene-bound chrome widgets for scenes 657304
    /// (<c>ParagonBoard</c>) + 964599 (<c>ParagonBoardSelect</c>):
    /// the dark textured board background, the board-select panel's
    /// preview frame, and the filigree band. The animated rim
    /// "fire border" is engine-internal (its candidate atlas frames
    /// exist in <c>2DUI_Paragon</c> but no scene widget binds them) —
    /// not surfaced here per the CL-28 / CL-30 no-fabrication
    /// discipline. Boundary unchanged: library decodes scene-bound
    /// chrome + a no-drop gate; consumer composites at runtime.
    /// </summary>
    public static ParagonBoardChrome BoardChrome(
        UiScene mainBoard, UiScene boardSelect,
        Func<uint, bool> isTextureHandle,
        Func<uint, (int AtlasSno, int W, int H)>? frameLookup = null,
        Func<int, TiledStyleDefinition?>? tiledStyleReader = null)
    {
        const uint TexHandleType = 0x6B1C5D9Cu;
        const uint TiledStyleField = 0x07DB38D3u; // FieldHash("snoTiledStyle")
        (int AtlasSno, int W, int H) Frame(uint h) =>
            h == 0 || h == 0xFFFFFFFFu ? (0, 0, 0)
            : frameLookup?.Invoke(h) ?? (0, 0, 0);

        // Catalog-filtered binding: only surface handles
        // <paramref name="isTextureHandle"/> accepts (the icon catalog
        // index). Used for Center + the board-select panel where every
        // bound texture is multi-frame-atlas resolvable.
        NodeElement CatalogBinding(UiWidget? w)
        {
            if (w is null) return default;
            uint handle = 0;
            foreach (var f in w.Fields)
                if (f.HasValue && f.TypeHash == TexHandleType &&
                    isTextureHandle(f.RawValue) && handle == 0)
                    handle = f.RawValue;
            foreach (var v in w.ExtraLayerValues)
                if (isTextureHandle(v) && handle == 0)
                    handle = v;
            if (handle == 0) return default;
            var (sno, fw, fh) = Frame(handle);
            return new NodeElement(handle, Rect(w), 0xFF, sno, fw, fh);
        }

        // Scene-bound binding (unfiltered): surface the first bound
        // texture-handle from the widget's standard 0x6B1C5D9C field,
        // regardless of whether the icon catalog resolves it. The
        // 4 board-rim widgets bind via this field but their target
        // handles route through a non-icon-catalog path CASC does not
        // currently index — <see cref="NodeElement.AtlasSno"/> and the
        // native size come back zero, the handle itself stays
        // authoritative.
        NodeElement SceneBinding(UiWidget? w)
        {
            if (w is null) return default;
            uint handle = 0;
            foreach (var f in w.Fields)
                if (f.HasValue && f.TypeHash == TexHandleType &&
                    f.RawValue is not 0 and not 0xFFFFFFFFu &&
                    handle == 0)
                    handle = f.RawValue;
            if (handle == 0) return default;
            var (sno, fw, fh) = Frame(handle);
            return new NodeElement(handle, Rect(w), 0xFF, sno, fw, fh);
        }

        IReadOnlyList<NodeElement> WidgetAllLayers(UiWidget? w)
        {
            if (w is null) return Array.Empty<NodeElement>();
            var seen = new HashSet<uint>();
            var rect = Rect(w);
            var list = new List<NodeElement>();
            foreach (var f in w.Fields)
                if (f.HasValue && f.TypeHash == TexHandleType &&
                    isTextureHandle(f.RawValue) && seen.Add(f.RawValue))
                {
                    var (sno, fw, fh) = Frame(f.RawValue);
                    list.Add(new NodeElement(f.RawValue, rect, 0xFF, sno, fw, fh));
                }
            foreach (var v in w.ExtraLayerValues)
                if (isTextureHandle(v) && seen.Add(v))
                {
                    var (sno, fw, fh) = Frame(v);
                    list.Add(new NodeElement(v, rect, 0xFF, sno, fw, fh));
                }
            return list;
        }

        UiWidget? ByName(UiScene s, string n) =>
            s.Widgets.FirstOrDefault(w => w.Name == n);

        // Main board (657304): a 5-piece chrome composite — a centre
        // background field plus a 4-cardinal-side rim. Center binds
        // 0x2954DF0C (1200² icon-catalog atlas frame). The rim sides
        // bind handles that resolve via a non-icon-catalog texture
        // path (Top/Bottom share 0x900C7D87; Left/Right share
        // 0x225F2DA8); CASC surfaces the handles as-is and leaves
        // AtlasSno/native px at zero so the consumer knows to use a
        // non-icon-catalog resolution path or a procedural equivalent.
        var center = CatalogBinding(
            ByName(mainBoard, "Template_Board_Background_Center"));
        var top    = SceneBinding(ByName(mainBoard, "Template_Board_Background_Top"));
        var right  = SceneBinding(ByName(mainBoard, "Template_Board_Background_Right"));
        var bottom = SceneBinding(ByName(mainBoard, "Template_Board_Background_Bottom"));
        var left   = SceneBinding(ByName(mainBoard, "Template_Board_Background_Left"));

        // Board-select panel chrome — scene 964599's preview-frame
        // backing (Board_BG) and the filigree band
        // (Board_Icon_Filigrees). Board_Icon_Template binds the same
        // preview-frame handles as Board_BG (drawn per board), so it
        // is intentionally not duplicated here.
        var selectLayers = new List<NodeElement>();
        foreach (var le in WidgetAllLayers(ByName(boardSelect, "Board_BG")))
            selectLayers.Add(le);
        foreach (var le in WidgetAllLayers(
            ByName(boardSelect, "Board_Icon_Filigrees")))
            selectLayers.Add(le);

        // FR-C14 R9 — scan both scenes for widgets that bind
        // snoTiledStyle (FieldHash 0x07DB38D3) to a non-zero, non-sentinel
        // SNO id, and surface them as TiledStyleBinding records. The
        // underlying TiledStyle SNO is pre-read via tiledStyleReader
        // when available, so consumers can inspect ImageScale +
        // PrimaryHandle alongside the binding without an extra lookup.
        var tiledBindings = new List<TiledStyleBinding>();
        ScanTiledStyles(mainBoard);
        ScanTiledStyles(boardSelect);

        void ScanTiledStyles(UiScene scene)
        {
            foreach (var w in scene.Widgets)
            {
                foreach (var f in w.Fields)
                {
                    if (f.FieldHash != TiledStyleField) continue;
                    if (!f.HasValue) continue;
                    if (f.RawValue == 0u || f.RawValue == 0xFFFFFFFFu) continue;
                    var snoId = (int)f.RawValue;
                    var style = tiledStyleReader?.Invoke(snoId);
                    tiledBindings.Add(new TiledStyleBinding(
                        w.Name ?? string.Empty, w.ClassId, snoId, style));
                }
            }
        }

        return new ParagonBoardChrome(
            center, top, right, bottom, left, selectLayers, tiledBindings);
    }

    private static readonly uint FhImageFrame = Diablo4.FieldHash("hImageFrame");
    private static readonly uint FdwAlpha = Diablo4.FieldHash("dwAlpha");

    /// <summary>
    /// FR-C16 R14 — project the per-node render program from the main board
    /// scene (657304) as a flat, truly-z-ordered list of atomic
    /// <see cref="ParagonNodeComponent"/>s (see
    /// <see cref="ParagonNodeRecipe"/> for the consumer's draw-all-active
    /// rule). The node-composition widget run is the contiguous subtree from
    /// the first <c>Common_Node*</c>/<c>Node_*</c> widget through the last
    /// <c>Template_Node_*</c> (the blob serializes widgets in depth-first
    /// child order with no parent field — FR-C16 R3).
    /// <br/><br/>
    /// Each <c>Template_Node_&lt;rarity/type&gt;</c> parent is flattened: its
    /// anonymous child sub-records each become a component whose activation
    /// is the parent rarity/type fact <b>AND</b> the child's selection state,
    /// and whose z is remapped to the base-disc position (below the symbol),
    /// since the per-rarity disc <i>substitutes</i> <c>Node_IconBase</c>
    /// rather than drawing at the template's appended scene index. The grey
    /// <c>Node_IconBase</c>/<c>Node_Purchased</c> discs are the Common-rarity
    /// variant, gated <c>[RarityCommon, …]</c> so they don't paint over a
    /// coloured disc.
    /// </summary>
    public static ParagonNodeRecipe NodeRecipe(
        UiScene mainBoard, Func<uint, bool>? isTextureHandle = null)
    {
        // A drawable handle must RESOLVE to an atlas frame (FR-C16 R5), not
        // merely exceed a magnitude — excludes the small-negative overscan
        // insets the 0x58 blocks interleave.
        bool IsHandle(uint v) => v is not 0u and not 0xFFFFFFFFu &&
            (isTextureHandle is not null
                ? isTextureHandle(v)
                : v is >= 0x10000u and < 0xFFFFFF00u);

        var ws = mainBoard.Widgets;
        int first = -1, last = -1, iconBaseIdx = -1;
        for (int k = 0; k < ws.Count; k++)
        {
            var nm = ws[k].Name ?? string.Empty;
            if ((nm.StartsWith("Common_Node", StringComparison.Ordinal) ||
                 nm.StartsWith("Node_", StringComparison.Ordinal)) && first < 0) first = k;
            if (nm.StartsWith("Template_Node_", StringComparison.Ordinal)) last = k;
            if (nm == "Node_IconBase") iconBaseIdx = k;
        }
        if (first < 0 || last < first)
            return new ParagonNodeRecipe(Array.Empty<ParagonNodeComponent>());

        // Cell reference extent (for anchoring resolution) — Template_Node_Common.
        int cellExtent = 100;
        var commonCell = ws.FirstOrDefault(w => w.Name == "Template_Node_Common");
        if (commonCell is not null) { int cw = Val(commonCell.Fields, FnWidth); if (cw > 0) cellExtent = cw; }

        // The base-disc slot z (where a rarity/type disc substitutes the grey
        // base): Node_IconBase's scene index. Template disc components remap
        // here so they sort below the symbol (Node_Icon), not at z≈121.
        double baseZ = iconBaseIdx >= 0 ? iconBaseIdx : first;

        // FR-C12 #22: the canonical base-disc inset (Node_IconBase, =7 → 86² in
        // the 100 cell). A template base child whose rect is unspecified
        // (all-zero) — e.g. the Starter base 0xF8312CA8 — renders at this disc
        // size, NOT stretched full-cell (the FR-C18 oversize class generalised
        // beyond the rarity disc-pair). Explicitly-sized children (e.g. the
        // Starter filigree's authored 140² overscan, the gate ornate's inset-3)
        // keep their own rect.
        int discInset = iconBaseIdx >= 0 ? Val(ws[iconBaseIdx].Fields, FnLeft) : 7;
        var baseDiscRect = new WidgetRect(discInset, discInset, discInset, discInset, 0, 0);

        var eng = NodeActivationSource.EngineBehavior;
        NodeActivation Never() => new(new[] { NodeFact.Never }, eng);
        NodeActivation Kind(NodeFact pf, params NodeFact[] extra) =>
            new(extra.Length == 0 ? new[] { pf } : new[] { pf }.Concat(extra).ToArray(), eng);

        // The resting state: any kind, unpurchased, unselected, no other
        // runtime state. A layer authored default-OFF (bActive=0) must NOT be
        // active here — if its name/position activation would fire at rest,
        // that's a contradiction (it's an engine-toggled overlay whose trigger
        // isn't decoded) → Never. This keeps a bActive=0 widget like
        // Rarity_Display from drawing over the unpurchased base disc.
        var resting = new HashSet<NodeFact>
        {
            NodeFact.Unpurchased, NodeFact.Unselected, NodeFact.KindCommon, NodeFact.KindMagic,
            NodeFact.KindRare, NodeFact.KindLegendary, NodeFact.KindSocket, NodeFact.KindGate, NodeFact.KindStart,
        };
        NodeActivation Finalize(NodeActivation a, IReadOnlyList<UiField> fields) =>
            DefaultActiveOf(fields) || !a.Evaluate(resting) ? a : Never();

        var staged = new List<(double Sort, ParagonNodeComponent Comp)>();
        // Emit one component per drawable layer (a widget's own hImageFrame, or
        // a handle-bearing child sub-record), carrying its authored bActive
        // (default visibility), rgbaTint, anchoring-resolved rect, and alpha.
        void Emit(double sort, string src, uint handle, IReadOnlyList<UiField> fields,
                  WidgetRect rawRect, NodeActivation act)
        {
            var placed = ResolvePlacement(rawRect, Val(fields, FeAnchor), cellExtent);
            staged.Add((sort, new ParagonNodeComponent(
                0, src, handle, placed, AlphaOf(fields), act,
                DefaultActiveOf(fields), TintOf(fields))));
        }

        for (int k = first; k <= last; k++)
        {
            var wd = ws[k];
            var name = wd.Name ?? string.Empty;
            var handleChildren = wd.Children
                .Where(c => IsHandle((uint)Val(c.Fields, FhImageFrame))).ToList();

            if (TemplateFact(name) is NodeFact pf)
            {
                // Template widget → one component per handle-bearing child,
                // remapped into the base-disc band (below the symbol).
                bool rarity = pf is NodeFact.KindMagic or NodeFact.KindRare or NodeFact.KindLegendary;

                // Disc-pair co-sizing (FR-C18): the unselected (child 0) /
                // selected (child 1) discs share one authored inset; a side
                // with an empty rect inherits the other.
                WidgetRect shared = default;
                if (rarity && handleChildren.Count >= 2)
                {
                    var r0 = RectOf(handleChildren[0].Fields);
                    var r1 = RectOf(handleChildren[1].Fields);
                    shared = !RectIsEmpty(r1) ? r1 : !RectIsEmpty(r0) ? r0 : default;
                }

                for (int i = 0; i < handleChildren.Count; i++)
                {
                    var cf = handleChildren[i].Fields;
                    uint h = (uint)Val(cf, FhImageFrame);
                    var rect = RectOf(cf);

                    // An unspecified (all-zero) rect inherits the disc size: the
                    // rarity pair's co-sized inset where present, else the
                    // canonical base-disc inset (86²) — never full-cell.
                    if (RectIsEmpty(rect)) rect = !RectIsEmpty(shared) ? shared : baseDiscRect;

                    // Activation grounded in the authored layout (the swap pair
                    // child 0/1 → unselected/selected disc; a known gate
                    // ornate/locator role; otherwise always-for-this-kind), then
                    // Finalize against bActive: a default-off layer that would
                    // fire at rest becomes Never.
                    NodeActivation act =
                        rarity && i == 0 ? Kind(pf, NodeFact.Unpurchased)
                      : rarity && i == 1 ? Kind(pf, NodeFact.Purchased)
                      : !rarity && GateRoleFact(h) is NodeFact gr ? Kind(pf, gr)
                      : Kind(pf);

                    Emit(baseZ + 0.001 * (i + 1), $"{name}[{i}]", h, cf, rect, Finalize(act, cf));
                }
            }
            else if (name.StartsWith("Usage_Slot", StringComparison.Ordinal))
            {
                // The SOCKET node's on-board type-disc has no Template_Node_*
                // widget — Template_Node_Socketable is empty. The engine draws
                // it from the Usage_Slot_* (equipped-glyph side-panel) widgets,
                // reusing their disc frames on-board (FR-C12 / CL-34). So treat
                // Usage_Slot_* as the KindSocket type-disc carrier, exactly like
                // a rarity Template_Node_* widget: emit only its handle-bearing
                // DISC children, remapped into the base-disc band (below the
                // symbol/arrows/connectors) so they compose like any base disc.
                // The widget's OWN hImageFrame (0x3084D186, 12² ) is the
                // side-panel usage-pip bead — NOT part of the on-board node —
                // so it is not emitted.
                for (int j = 0; j < handleChildren.Count; j++)
                {
                    var cf = handleChildren[j].Fields;
                    uint h = (uint)Val(cf, FhImageFrame);
                    Emit(baseZ + 0.001 * (j + 1), $"{name}[{j}]", h, cf, RectOf(cf),
                        Finalize(Kind(NodeFact.KindSocket), cf));
                }
            }
            else
            {
                // Non-template widget: emit its own hImageFrame layer (the name
                // encodes its state — Node_Purchased→selected, Arrow_*→neighbour,
                // …) plus any handle-bearing child layers. A default-off
                // (bActive=0) child with no named state is Never (engine-toggled,
                // undecoded).
                uint own = (uint)Val(wd.Fields, FhImageFrame);
                Emit(k, name, own, wd.Fields, RectOf(wd.Fields), Finalize(BaseActivation(name), wd.Fields));

                for (int j = 0; j < handleChildren.Count; j++)
                {
                    var cf = handleChildren[j].Fields;
                    uint h = (uint)Val(cf, FhImageFrame);
                    Emit(k + 0.001 * (j + 1), $"{name}[{j}]", h, cf, RectOf(cf), Finalize(BaseActivation(name), cf));
                }
            }
        }

        // True paint order = list order: stable-sort by the remapped z, then
        // assign ZOrder = final index.
        var ordered = staged.OrderBy(s => s.Sort).ToList();
        var comps = new List<ParagonNodeComponent>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
            comps.Add(ordered[i].Comp with { ZOrder = i });
        return new ParagonNodeRecipe(comps);
    }

    /// <summary>
    /// FR-C17 — project the board grid-layout metric from the main board
    /// scene (657304): canvas extent from <c>ParagonBoard_main</c>, cell
    /// extent from <c>Template_Node_Common</c>. <see cref="ParagonBoardGrid.Pitch"/>
    /// is set equal to the cell extent (adjacent-cell layout — see the
    /// record remarks for the owner-measurement validation). Falls back
    /// to the current-build constants (1920×1200, cell 100) if a widget
    /// is missing.
    /// </summary>
    public static ParagonBoardGrid BoardGrid(UiScene mainBoard)
    {
        UiWidget? ByName(string n) => mainBoard.Widgets.FirstOrDefault(w => w.Name == n);
        var root = ByName("ParagonBoard_main");
        var cell = ByName("Template_Node_Common");
        int canvasW = root is null ? 1920 : Val(root, FnWidth);
        int canvasH = root is null ? 1200 : Val(root, FnHeight);
        int cellExt = cell is null ? 100 : Val(cell, FnWidth);
        if (canvasW <= 0) canvasW = 1920;
        if (canvasH <= 0) canvasH = 1200;
        if (cellExt <= 0) cellExt = 100;
        return new ParagonBoardGrid(canvasW, canvasH, cellExt, cellExt);
    }
}
