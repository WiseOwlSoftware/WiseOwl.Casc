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
}
