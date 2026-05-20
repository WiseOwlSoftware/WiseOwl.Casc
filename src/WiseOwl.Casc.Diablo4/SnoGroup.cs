namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// Diablo IV SNO ("Sno", a typed game-object id namespace) group ids. Only
/// the groups this module currently consumes are named; the full set has up
/// to ~154 groups, all enumerable from <see cref="CoreToc"/>.
/// </summary>
/// <remarks>
/// Values verified against current Diablo IV (the upstream
/// reverse-engineering record, <c>d4-binary-formats.md §2</c>):
/// community-circulated values such as <c>ParagonBoard = 73</c> are
/// <b>wrong</b> for the current build. <c>Attribute</c> and <c>Formula</c>
/// are deliberately absent — they are not SNO groups in Diablo IV.
/// </remarks>
public enum SnoGroup
{
    /// <summary>GameBalance tables (<c>.gam</c>) — formula/constant tables.</summary>
    GameBalance = 20,

    /// <summary>Power definitions (<c>.pow</c>) — skills/passives.</summary>
    Power = 29,

    /// <summary>Item definitions (<c>.itm</c>).</summary>
    Item = 73,

    /// <summary>Player class definitions (<c>.prd</c>).</summary>
    PlayerClass = 74,

    /// <summary>Item-type definitions.</summary>
    ItemType = 98,

    /// <summary>Affix definitions (<c>.aff</c>).</summary>
    Affix = 104,

    /// <summary>StringList tables (<c>.stl</c>) — localized <c>label → text</c>
    /// buckets. Not per-SNO path-addressable; delivered through the per-locale
    /// consolidated bundle read by <see cref="Diablo4Storage.GetStrings"/> /
    /// <see cref="StringListCatalog"/>. The group id is still meaningful for
    /// CoreTOC name↔id resolution (e.g. a record's sibling string table).</summary>
    StringList = 42,

    /// <summary>UI / 2D textures (<c>.tex</c>). Paragon node/glyph art lives
    /// in shared atlases here; the pixel payload is addressable by SNO id but
    /// the <see cref="TextureDefinition"/> meta is only in the combined-meta
    /// bundle (see <see cref="CombinedTextureMeta"/>).</summary>
    Texture = 44,

    /// <summary>Paragon node definitions (<c>.pgn</c>).</summary>
    ParagonNode = 106,

    /// <summary>Paragon board definitions (<c>.pbd</c>).</summary>
    ParagonBoard = 108,

    /// <summary>Paragon glyph definitions (<c>.gph</c>).</summary>
    ParagonGlyph = 111,

    /// <summary>Paragon glyph-affix definitions (<c>.gaf</c>).</summary>
    ParagonGlyphAffix = 112,

    /// <summary>UI Style — <see cref="TiledStyleDefinition"/> records.
    /// Carry the engine's tile-rendering composition for UI overlays
    /// (piece handles for the 3-slice / 9-slice composition, image
    /// scale, padding). Identified from FR-C14 R8 by cracking
    /// <see cref="Diablo4.FieldHash"/>(<c>"snoTiledStyle"</c>) =
    /// 0x07DB38D3 and tracing the bound SNO ids to this group
    /// (format hash 0x80504E18). Sample entries: 843662 "InnerShadow",
    /// 603760 "BagBackground", 1309282 "Frame_AbilityPoints",
    /// 872641 "Tutorial_Highlight", 1841254 "Expansion_Frame_Ultimate".</summary>
    UiStyle = 103,
}
