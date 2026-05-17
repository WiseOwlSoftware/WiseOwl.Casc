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

    public static ParagonRenderLayout Project(UiScene scene)
    {
        UiWidget? ByName(string n) =>
            scene.Widgets.FirstOrDefault(w => w.Name == n);

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

        // Ratios stay Provisional until the §10.8 67.7 anchor
        // reproduces — no pitch/scale number is asserted here.
        var ratios = new RenderRatios(
            Provisional: true,
            PitchRef: 0, DiscRef: 0,
            OrnateOverDisc: 0, SymbolOverDisc: 0,
            GreyRingOverDisc: 0, SocketRingOverDisc: 0);

        // Disc/Symbol elements and the 18-row State matrix require the
        // per-state widget→texture assembly (§10.12); rows are added
        // only when decode-proven (no fabricated states). Until then
        // States is empty and the consumer relies on ReadUiScene +
        // their interim calibrated constants (the contract's staged
        // design: Provisional ratios + audit rects now, States next).
        var states = Array.Empty<StateElements>();

        return new ParagonRenderLayout(
            ratios, canvas,
            Rect(container), Rect(template),
            boardRotationQuadrant,
            Disc: default, Symbol: default,
            States: states);
    }
}
