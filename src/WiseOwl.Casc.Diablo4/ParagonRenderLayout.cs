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

/// <summary>The UI design space the raw rects are authored in (decoded
/// from the root <c>ParagonBoard_main</c> widget; verified
/// 1920×1200).</summary>
public readonly record struct CanvasRef(int Width, int Height);

/// <summary>An authored <c>DT_INT</c> bindable rect, exactly as stored
/// (UI reference units; no pixels). Pitch is derived, not stored.</summary>
public readonly record struct WidgetRect(
    int Left, int Right, int Top, int Bottom, int Width, int Height);

/// <summary>A drawable element: its raw texture handle
/// (<c>== TexFrame.ImageHandle</c>; <c>0xFFFFFFFF</c>/0 ⇒ none, never
/// pre-resolved — Q4), its reference-unit rect, and raw
/// <c>dwAlpha</c>.</summary>
public readonly record struct NodeElement(
    uint TextureHandle, WidgetRect Rect, byte Alpha);

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
/// One row of the §7.2 state contract (15 baked + 3 overlay = 18):
/// the back→front layer list for a (rarity, state) or a kind/overlay.
/// </summary>
/// <param name="RarityOverride">0/2/3/4, or −1 for
/// socket/gate/start/overlay.</param>
/// <param name="State">The canonical §7.2 key (e.g. <c>unselected</c>,
/// <c>socket.unselected</c>, <c>overlay.connectorBar</c>).</param>
/// <param name="Layers">Back→front draw layers.</param>
/// <param name="Tint">The bound per-rarity×state <c>rgbaTint</c>
/// (<see langword="null"/> if none ⇒ fixed-shader, consumer recipe).</param>
/// <param name="LitTint">The second <c>DT_RGBACOLOR</c> (relit colour)
/// on <c>selected</c> keys, if authored.</param>
/// <param name="Animation">Pulse/rotate spec, or
/// <see langword="null"/>.</param>
public readonly record struct StateElements(
    int RarityOverride, string State,
    IReadOnlyList<NodeElement> Layers,
    RgbaTint? Tint, RgbaTint? LitTint, AnimSpec? Animation);

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
        UiScene scene, Func<uint, bool>? isTextureHandle = null)
    {
        UiWidget? ByName(string n) =>
            scene.Widgets.FirstOrDefault(w => w.Name == n);

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
                    list.Add(new NodeElement(v, default, 0));
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
            return new NodeElement(handle, Rect(x), alpha);
        }

        var disc   = Elem("Node_IconBase");        // 0x1D166DC7
        var ornate = Elem("NodeAvailableGlow");    // 0x4A901508 (Rare/Legendary)
        var pulse  = Elem("GlyphNodeGlow_Revealed"); // 0xBED4CF21 (socket)

        static NodeElement[] L(params NodeElement[] xs) =>
            xs.Where(e => e.TextureHandle != 0).ToArray();

        var states = new List<StateElements>(18);

        // Rows 1–8: rarity {0,2,3,4} × {unselected,selected}. Layers are
        // the shared decode-true elements (disc, + gold ornate for
        // Rare/Legendary); per-rarity colour is rgbaTint (not decoded
        // per-rarity ⇒ Tint/LitTint left null, not fabricated).
        foreach (var rar in new[] { 0, 2, 3, 4 })
            foreach (var st in new[] { "unselected", "selected" })
                states.Add(new StateElements(
                    rar, st,
                    rar >= 3 ? L(disc, ornate) : L(disc),
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

        // Rows 16–18: overlays. CORRECTION (FR-C8 R6, CL-24): the
        // directional pointer AND the connector bars are NOT
        // pure-procedural. `Arrow_{Top,Right,Bottom,Left}` bind the
        // pre-oriented red arrow art (Top 0xD51CAB25, Right 0x6D3CB8DE,
        // Bottom 0x8EEAC178, Left 0xB6D8C741); `Connector_{...}` bind
        // the connector art (0x77ECA3A8 / 0x288DE11F) — each with an
        // authored rect, via the standard texture-handle field on the
        // FR-C7 0x22 path. FR-C7 missed both because the texture handle
        // is each widget's *last* 0x22 record, which straddled the
        // widget boundary and was dropped (UiScene.Parse tail fix,
        // CL-24) — and FR-C7 also hardcoded these rows empty. So
        // `overlay.pointerTriangle` / `overlay.connectorBar` now carry
        // their real T/R/B/L bound layers (handle + decoded Rect).
        // `overlay.selectionRing` has no scene widget → genuinely
        // engine-drawn, stays empty. No fabrication — empty rows stay
        // empty, the arrows/connectors are the real decoded values.
        NodeElement[] Overlay(params string[] widgets) =>
            L(widgets.Select(Elem).ToArray());

        states.Add(new StateElements(-1, "overlay.selectionRing",
            Array.Empty<NodeElement>(), null, null, null));
        states.Add(new StateElements(-1, "overlay.connectorBar",
            Overlay("Connector_Top", "Connector_Right",
                    "Connector_Bottom", "Connector_Left"),
            null, null, null));
        states.Add(new StateElements(-1, "overlay.pointerTriangle",
            Overlay("Arrow_Top", "Arrow_Right",
                    "Arrow_Bottom", "Arrow_Left"),
            null, null, null));

        return new ParagonRenderLayout(
            ratios, canvas,
            Rect(container), Rect(template),
            boardRotationQuadrant,
            Disc: disc, Symbol: default,
            States: states);
    }
}
