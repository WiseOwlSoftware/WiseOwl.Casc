using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>ParagonGlyphDefinition</c> (<c>.gph</c>, SNO
/// group 111). Raw fields only.
/// </summary>
/// <remarks>
/// Byte layout per the canonical reference (<c>docs/casc-diablo4-format.md §7.3</c>,
/// migrated/verified from upstream <c>d4-binary-formats.md §5</c> — the
/// corrected glyph layout): payload base <c>0x10</c>; <c>snoId@0</c>; the
/// glyph's affixes are a fixed set of up to three <c>DT_SNO</c> ids at
/// payload <c>+104 / +108 / +112</c> (the 3 glyph power components —
/// scaling mod / threshold / legendary). <c>0</c> and <c>0xFFFFFFFF</c>
/// slots are treated as "no affix" and omitted.
/// </remarks>
public sealed class ParagonGlyphDefinition
{
    private static readonly int[] NoClasses = [];

    private readonly int[] _affixSnoIds;
    private int[] _usableByClassSnoIds = NoClasses;

    private ParagonGlyphDefinition(int snoId, int[] affixSnoIds)
    {
        SnoId = snoId;
        _affixSnoIds = affixSnoIds;
    }

    /// <summary>The glyph's own SNO id.</summary>
    public int SnoId { get; }

    /// <summary>FR-C24 (CL-79) — the glyph's localized display title
    /// (e.g. <c>"Attrition"</c>, <c>"Guzzler"</c>, <c>"Abyssal"</c>)
    /// resolved via the §6.7 sibling-StringList convention
    /// (<c>Item_ParagonGlyph_&lt;GlyphSnoName&gt;</c>, label
    /// <c>Name</c>) with the universal <c>"Glyph: "</c> prefix
    /// stripped. Empty when the sibling table is missing or the
    /// glyph was decoded via the byte-only
    /// <see cref="Parse(ReadOnlySpan{byte})"/> (no
    /// <see cref="CoreToc"/> to resolve the sibling name).
    /// Populated by
    /// <see cref="Diablo4Storage.ReadParagonGlyph(int, string)"/>.</summary>
    public string LocalizedTitle { get; private set; } = string.Empty;

    /// <summary>FR-C24 (CL-79) — the glyph's nominal
    /// <see cref="ParagonRarity"/>. Every glyph in the live build
    /// (3.0.2.71886) is authored as <see cref="ParagonRarity.Rare"/>
    /// (the SNO name follows <c>Rare_&lt;NN&gt;_&lt;Stat&gt;_&lt;Slot&gt;</c>);
    /// the field is exposed forward-looking should the engine add
    /// Magic / Legendary glyphs in a future build. The value comes
    /// from the SNO name's leading-token convention — the durable
    /// opaque-id principle (Appendix C) decodes naming-convention
    /// fields library-side and surfaces them typed.</summary>
    public ParagonRarity Rarity { get; private set; } = ParagonRarity.Common;

    /// <summary>FR-C24 (CL-83) — the glyph's base socket-effect
    /// radius at Level 1. Engine constant on build
    /// <c>3.0.2.71886</c> — empirically <c>3</c> across all 21
    /// Warlock glyphs in the Optimizer's oracle table; the
    /// <c>.gph</c> record itself has no <c>nStartingSize</c> field
    /// (payload ends at the affix-array descriptor; no per-glyph
    /// variance authored). Surfaced as a read-only constant so a
    /// future season's per-glyph variation triggers an Appendix D
    /// re-verify on this property rather than a silent drift in
    /// consumer code that hard-coded <c>3</c>.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Instance property to preserve consumer-side " +
            "`glyph.BaseRadius` access pattern when a future season " +
            "may ship per-glyph variance (engine-constant today).")]
    public int BaseRadius => 3;

    /// <summary>FR-C24 (CL-83) — the player levels at which the
    /// glyph's socket-effect radius grows by one. Engine constant
    /// on build <c>3.0.2.71886</c> — empirically <c>[25, 50]</c>
    /// across all 21 Warlock glyphs (radius is
    /// <see cref="BaseRadius"/> at L1..L24, <see cref="BaseRadius"/>+1
    /// at L25..L49, <see cref="BaseRadius"/>+2 at L50+). Same
    /// rationale + re-verify trigger as <see cref="BaseRadius"/> —
    /// the <c>.gph</c> record carries no <c>arSizeUpgradeLevels</c>
    /// field.</summary>
    public IReadOnlyList<int> RadiusUpgradeLevels { get; }
        = ParagonGlyphEngineConstants.RadiusUpgradeLevels;

    /// <summary>FR-C24 (CL-83) — the maximum player level a glyph
    /// can reach (the level cap that gates <see cref="RadiusUpgradeLevels"/>
    /// and the per-level affix scaling). Engine constant on build
    /// <c>3.0.2.71886</c> — empirically <c>150</c>; the
    /// <c>ParagonGlyphExperienceTable</c> (sno <c>810212</c>) ships
    /// 201 XP-curve entries but the in-game cap is held at 150.
    /// Same re-verify trigger as <see cref="BaseRadius"/> — if a
    /// future season raises the cap, this property's value needs
    /// updating + Appendix D re-verify.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "Instance property to preserve consumer-side " +
            "`glyph.MaxLevel` access pattern when a future season " +
            "may ship per-glyph variance (engine-constant today).")]
    public int MaxLevel => 150;

    /// <summary>The glyph's ParagonGlyphAffix SNO ids (0..3, in slot order).</summary>
    public IReadOnlyList<int> AffixSnoIds => _affixSnoIds;

    /// <summary>
    /// The set of classes that may socket this glyph, as
    /// <see cref="SnoGroup.PlayerClass"/> SNO ids — the shared class key
    /// (== <see cref="CharacterClass.SnoId"/> /
    /// <see cref="ParagonBoardDefinition.ClassSnoId"/>; FR-D3). Empty if
    /// decoded via the byte-only <see cref="Parse(ReadOnlySpan{byte})"/>
    /// (no <see cref="CoreToc"/> to resolve the class ordering), or for a
    /// malformed/placeholder glyph record (honest empty sentinel — never a
    /// silently-wrong class).
    /// </summary>
    /// <remarks>
    /// Decoded clean-room (<c>docs/casc-diablo4-format.md §7.3</c>,
    /// Appendix A CL-18). The record carries a per-class boolean fixed
    /// array <c>fUsableByClass</c> at payload <c>+0x24</c>; the slot index
    /// for a class is that class's <b>eClass rank</b> — the position of the
    /// class when the §6.5 PlayerClass roster is ordered ascending by the
    /// class's <c>eClass</c> ordinal (PlayerClass record payload <c>+16</c>).
    /// Per the durable opaque-id principle (Appendix C) this ordering is a
    /// data mapping decoded once, library-side, exposed typed — never a
    /// consumer bit-order guess. Populated by
    /// <see cref="Diablo4Storage.ReadParagonGlyph(int)"/>.
    /// </remarks>
    public IReadOnlyList<int> UsableByClassSnoIds => _usableByClassSnoIds;

    /// <summary>Attach the resolved class membership (internal — set by
    /// <see cref="Diablo4Storage.ReadParagonGlyph(int)"/>).</summary>
    internal void SetUsableByClassSnoIds(int[] classSnoIds) =>
        _usableByClassSnoIds = classSnoIds;

    /// <summary>Attach the localized title + rarity-token resolution
    /// (internal — set by
    /// <see cref="Diablo4Storage.ReadParagonGlyph(int)"/>).</summary>
    internal void SetLocalizedFields(string title, ParagonRarity rarity)
    {
        LocalizedTitle = title;
        Rarity = rarity;
    }

    /// <summary>Decode a ParagonGlyph from its raw SNO blob.</summary>
    public static ParagonGlyphDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        var snoId = r.SnoId;

        // Some group-111 SNOs are short/placeholder records (e.g. bad-data
        // entries) with no affix slots — read defensively.
        var affixes = new List<int>(3);
        foreach (var off in stackalloc[] { 104, 108, 112 })
        {
            if (r.PayloadBase + off + 4 > r.Length) break;
            var v = r.U32(off);
            if (v is not 0 and not 0xFFFFFFFF) affixes.Add((int)v);
        }
        return new ParagonGlyphDefinition(snoId, affixes.ToArray());
    }
}
