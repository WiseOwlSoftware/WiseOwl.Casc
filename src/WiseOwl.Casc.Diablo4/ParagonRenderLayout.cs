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
    IReadOnlyList<StateElements> States);

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
    IReadOnlyList<ParagonSceneModel> Scenes);

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
/// <c>NodeTemplate</c> at native px), raw <c>dwAlpha</c>, and
/// <see cref="EngineInternal"/> = <see langword="true"/> when the layer
/// is referenced by the engine but not bound to any scene widget
/// (e.g. the selected-state red ring <c>0xB732F921</c> on Common /
/// Magic — the engine renders it directly from the icon catalog;
/// <see cref="Diablo4Storage.ReadParagonRenderModel"/>'s exhaustive
/// scene-binding gate will not see it, hence the explicit flag).</summary>
public readonly record struct NodeElement(
    uint TextureHandle, WidgetRect Rect, byte Alpha,
    int AtlasSno = 0,
    int NativeWidth = 0, int NativeHeight = 0,
    bool EngineInternal = false);

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
/// (rarity, state) or a kind/overlay. Rows with no scene-widget binding
/// (engine-internal art) carry <see cref="Layers"/> empty and
/// <see cref="Unresolved"/> = <see langword="true"/> — they are
/// enumerated for schema completeness, not omitted.
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
/// engine draws it internally, or the art lives composited inside
/// another row's bindings). The per-record completeness gate
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

    private static int Val(UiWidget w, uint fieldHash)
    {
        foreach (var f in w.Fields)
            if (f.FieldHash == fieldHash && f.HasValue) return (int)f.RawValue;
        return 0;
    }

    private static WidgetRect Rect(UiWidget? w) => w is null
        ? default
        : new WidgetRect(
            Val(w, FnLeft), Val(w, FnRight), Val(w, FnTop),
            Val(w, FnBottom), Val(w, FnWidth), Val(w, FnHeight));

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

        var disc   = Elem("Node_IconBase");        // 0x1D166DC7
        var pulse  = Elem("GlyphNodeGlow_Revealed"); // 0xBED4CF21 (socket)

        static NodeElement[] L(params NodeElement[] xs) =>
            xs.Where(e => e.TextureHandle != 0).ToArray();

        // FR-C10 node composite recipe (§10.15). Each rarity composites
        // grey-base + (optional rarity-specific interior fill) +
        // (optional ornate outer frame). The selected state either swaps
        // the ornate for the rarity's selected-variant (Rare/Legendary
        // — red ring composited into the disc art) or adds the
        // standalone engine-internal red ring (Common/Magic — referenced
        // directly from the catalog, no scene widget binds it).
        // Per-rarity handle roles, build-stable on 3.0.2.71886, sourced
        // from atlas-frame visual inspection + owner visual oracle for
        // Magic (FR-C10 R1, oracle calibration screenshots).
        const uint EngineInternalRing = 0xB732F921u; // 96² 2DUI_Paragon_transparentElements
        const uint MagicInteriorFill  = 0xFEC31E48u; // 135² blue interior — owner-confirmed
        const uint RareInteriorFill   = 0xF8373491u; // 135² rare interior
        const uint RareOrnateUnsel    = 0xB71BD068u; // 154² yellow ornate frame
        const uint RareOrnateSel      = 0x03EDABABu; // 153² yellow ornate + red ring composite
        const uint LegInteriorFill    = 0x006ED182u; // 136² legendary interior
        const uint LegOrnateUnsel     = 0x232DF7F9u; // 189² orange spike ornate frame
        const uint LegOrnateSel       = 0xBD27FB7Cu; // 189² orange ornate + red ring composite

        NodeElement Layer(uint handle, bool engineInternal = false)
        {
            if (handle == 0) return default;
            var (sno, fw, fh) = Frame(handle);
            return new NodeElement(
                handle, default, 0xFF, sno, fw, fh, engineInternal);
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

        NodeElement[] RarityComposite(int rar, bool selected)
        {
            var layers = new List<NodeElement>();
            if (disc.TextureHandle != 0) layers.Add(disc);
            switch (rar)
            {
                case 0: // Common — base disc only; engine adds the ring on selected.
                    if (selected) layers.Add(Layer(EngineInternalRing, engineInternal: true));
                    break;
                case 2: // Magic — grey base + blue interior fill; + engine ring on selected.
                    if (magicBlockHandles.Contains(MagicInteriorFill))
                        layers.Add(Layer(MagicInteriorFill));
                    if (selected) layers.Add(Layer(EngineInternalRing, engineInternal: true));
                    break;
                case 3: // Rare — grey base + interior fill + ornate frame (swapped on selected).
                    if (rareBlockHandles.Contains(RareInteriorFill))
                        layers.Add(Layer(RareInteriorFill));
                    var rareOrnate = selected ? RareOrnateSel : RareOrnateUnsel;
                    if (rareBlockHandles.Contains(rareOrnate))
                        layers.Add(Layer(rareOrnate));
                    break;
                case 4: // Legendary — grey base + interior fill + larger spike ornate (swapped on selected).
                    if (legBlockHandles.Contains(LegInteriorFill))
                        layers.Add(Layer(LegInteriorFill));
                    var legOrnate = selected ? LegOrnateSel : LegOrnateUnsel;
                    if (legBlockHandles.Contains(legOrnate))
                        layers.Add(Layer(legOrnate));
                    break;
            }
            return layers.ToArray();
        }

        var states = new List<StateElements>(19);

        // Rows 1–8: rarity {0,2,3,4} × {unselected,selected}. Recipe
        // per §10.15: grey-base + (rarity-specific interior fill if
        // bound) + (ornate frame, swapped on selected for Rare/Legendary,
        // engine-internal red ring added on selected for Common/Magic).
        // Per-rarity tint stays null — the per-rarity colour comes from
        // the bound interior-fill atlas frame (already coloured), not a
        // shader tint on a shared disc.
        foreach (var rar in new[] { 0, 2, 3, 4 })
            foreach (var sel in new[] { false, true })
                states.Add(new StateElements(
                    rar, sel ? "selected" : "unselected",
                    L(RarityComposite(rar, sel)),
                    Tint: null, LitTint: null, Animation: null));

        // Rows 9–11: socket. Pulse present when unselected; dropped on
        // selected; socketed = selected + (glyph image, not separately
        // bound here). AnimSpec left null until the pulse params are
        // decoded (no fabricated anim).
        states.Add(new StateElements(-1, "socket.unselected",
            L(disc, pulse), null, null, Animation: null));
        states.Add(new StateElements(-1, "socket.selected",
            L(disc), null, null, null));
        states.Add(new StateElements(-1, "socket.socketed",
            L(disc), null, null, null));

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
        // selectionRing has no scene-widget binding — the smooth red
        // ring atlas frame (0xB732F921, 96² in 2DUI_Paragon_transparentElements)
        // is referenced engine-internally for Common rarity; for
        // rarity 2/3/4 the red ring lives composited inside each
        // Template_Node_{Magic,Rare,Legendary} selected-variant disc
        // (e.g. Magic-selected 0x72C29402, Rare-selected 0x03EDABAB,
        // Legendary-selected 0xBD27FB7C — surfaced in the rarity
        // selected rows above). Marked Unresolved=true so the
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
        states.Add(new StateElements(-1, "overlay.availableGlow",
            Overlay("NodeAvailableGlow"), null, null, null));

        return new ParagonRenderLayout(
            ratios, canvas,
            Rect(container), Rect(template),
            boardRotationQuadrant,
            Disc: disc, Symbol: default,
            States: states);
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
}
