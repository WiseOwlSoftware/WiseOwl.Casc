using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// FR-C19 — the engine's mouse-over / cursor <b>selection highlight</b>,
/// surfaced as the set of <b>authored recipes</b> that draw it. The selection
/// highlight is orthogonal to, and not part of, the paragon node render recipe
/// (<see cref="ParagonNodeRecipe"/>): a node can be selected whether or not it
/// is purchased, so the consumer composes the node from its recipe and then,
/// if the node is selected, draws the matching selection style on top.
/// <br/><br/>
/// Crucially, the game does <b>not</b> require the consumer to assemble the
/// highlight from loose atlas frames — it authors each selection shape as a
/// <see cref="TiledStyleDefinition">TiledStyle</see> 9-slice recipe (group
/// <see cref="SnoGroup.UiStyle"/> = 103) over the
/// <c>2DUI_SelectionHighlight</c> / <c>2DUITiled_SelectionHighlight</c>
/// atlases. This accessor returns those authored recipes (<see cref="Styles"/>)
/// so the consumer applies the existing TiledStyle machinery
/// (<see cref="Diablo4Storage.ReadTiledStyle"/>) — exactly the path used for
/// every other tiled UI drawing — and performs <b>no</b> composition or
/// shape-classification of its own.
/// <br/><br/>
/// Each style carries its <b>authored</b> name and the <see cref="SelectionShape"/>
/// derived from it (e.g. <c>ControllerSelectionCircle</c> →
/// <see cref="SelectionShape.Circle"/>, <c>SelectionRectangleInset</c> →
/// <see cref="SelectionShape.Rectangle"/>): the consumer selects the style
/// whose shape matches the node's silhouette. The shape comes from the engine's
/// own recipe name, not from an atlas-frame-size guess.
/// </summary>
/// <param name="Styles">The authored selection-highlight TiledStyle recipes,
/// ordered by name. Empty if the selection atlases are absent. Pass each
/// <see cref="SelectionHighlightStyle.TiledStyleSno"/> to
/// <see cref="Diablo4Storage.ReadTiledStyle"/> to obtain the 9-slice
/// composition.</param>
public sealed record SelectionHighlight(IReadOnlyList<SelectionHighlightStyle> Styles)
{
    /// <summary>True when no authored selection style was found.</summary>
    public bool IsEmpty => Styles.Count == 0;

    /// <summary>The selection-highlight atlas names whose composing TiledStyles
    /// make up a selection highlight. The plain atlas holds the per-shape
    /// controller-selection frames; the <c>…Tiled…</c> atlas is the inset
    /// rectangle's tiling sheet.</summary>
    internal static readonly string[] AtlasNames =
        ["2DUI_SelectionHighlight", "2DUITiled_SelectionHighlight"];

    /// <summary>Classify a selection style by its <b>authored</b> TiledStyle
    /// name (the engine's own role label), not by frame geometry.</summary>
    internal static SelectionShape ShapeOf(string name) =>
        name.Contains("Circle", StringComparison.Ordinal) ? SelectionShape.Circle :
        name.Contains("Diamond", StringComparison.Ordinal) ? SelectionShape.Diamond :
        name.Contains("TearDrop", StringComparison.Ordinal) ? SelectionShape.TearDrop :
        name.Contains("Rectangle", StringComparison.Ordinal) ? SelectionShape.Rectangle :
        SelectionShape.Other;
}

/// <summary>FR-C19 — one authored selection-highlight recipe: a TiledStyle
/// (group <see cref="SnoGroup.UiStyle"/>) that 9-slices a selection atlas.</summary>
/// <param name="TiledStyleSno">The TiledStyle SNO id — pass to
/// <see cref="Diablo4Storage.ReadTiledStyle"/> for the 9-slice composition
/// (piece handles + padding + scale).</param>
/// <param name="Name">The authored TiledStyle name (e.g.
/// <c>ControllerSelectionCircle</c>, <c>SelectionRectangleInset</c>).</param>
/// <param name="Shape">The node silhouette this style frames, derived from
/// <paramref name="Name"/>. The consumer picks the style matching the node's
/// shape.</param>
/// <param name="SourceImageHandle">The TiledStyle's source image handle (the
/// 9-slice sheet); resolves to <paramref name="AtlasSno"/>.</param>
/// <param name="AtlasSno">The selection atlas (<see cref="SnoGroup.Texture"/>)
/// this style composes.</param>
public readonly record struct SelectionHighlightStyle(
    int TiledStyleSno,
    string Name,
    SelectionShape Shape,
    uint SourceImageHandle,
    int AtlasSno);

/// <summary>
/// FR-C19 — the paragon <b>node mouse-over (hover) selection highlight</b>, as the
/// engine authors it: the <c>ContextualHighlight_Square</c> recipe (a 4-piece
/// <c>TiledWindowPieces</c> TiledStyle — the named "square contextual highlight")
/// paired with its drawable corner art (the 4 corner frames of the
/// <c>2DUITiled_SelectionHighlight</c> atlas, surfaced via
/// <c>SelectionRectangleInset</c>'s window-pieces).
/// <br/><br/>
/// <b>Drawing recipe (authored, owner-validated):</b> a hollow square border
/// sized to the node's bounding square. Draw <b>only the 4 corners</b> — each
/// piece is a full quadrant (corner + both half-edges), so the four meet to
/// surround the node; place <see cref="TopLeft"/> in the top-left quadrant,
/// <see cref="TopRight"/> top-right, etc. <b>No edge or centre pieces, no fill</b>
/// (the node shows through). Resolve each corner handle via the texture path
/// (<see cref="Diablo4Storage.TryGetIconFrame"/> → decode) for the orange-glow +
/// white-edge art.
/// </summary>
/// <param name="RecipeSno">The <c>ContextualHighlight_Square</c> TiledStyle SNO id
/// (group <see cref="SnoGroup.UiStyle"/>) — the authored recipe this represents.
/// <c>0</c> if absent.</param>
/// <param name="RecipeName">The authored recipe name (<c>ContextualHighlight_Square</c>).</param>
/// <param name="TopLeft">Top-left corner frame handle.</param>
/// <param name="TopRight">Top-right corner frame handle.</param>
/// <param name="BottomRight">Bottom-right corner frame handle.</param>
/// <param name="BottomLeft">Bottom-left corner frame handle.</param>
public readonly record struct NodeSelectionHighlight(
    int RecipeSno,
    string RecipeName,
    uint TopLeft,
    uint TopRight,
    uint BottomRight,
    uint BottomLeft)
{
    /// <summary>The 4 corner handles (TL, TR, BR, BL) — the complete hollow
    /// border; draw each in the matching quadrant of the node square.</summary>
    public IReadOnlyList<uint> Corners => [TopLeft, TopRight, BottomRight, BottomLeft];

    /// <summary>True when the corner art could not be resolved.</summary>
    public bool IsEmpty => TopLeft == 0 && TopRight == 0 && BottomRight == 0 && BottomLeft == 0;
}

/// <summary>FR-C19 — node silhouette a <see cref="SelectionHighlightStyle"/>
/// frames, classified from the authored TiledStyle name.</summary>
public enum SelectionShape
{
    /// <summary>Square / rectangular node frame (the common paragon node case;
    /// authored as the inset rectangle and the controller rectangle).</summary>
    Rectangle,
    /// <summary>Round disc node frame.</summary>
    Circle,
    /// <summary>Diamond (gate) node frame.</summary>
    Diamond,
    /// <summary>Teardrop node frame.</summary>
    TearDrop,
    /// <summary>A named selection style whose shape is not one of the above
    /// (e.g. <c>ControllerSelectionAPS</c>); use <see cref="SelectionHighlightStyle.Name"/>.</summary>
    Other,
}
