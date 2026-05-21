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
