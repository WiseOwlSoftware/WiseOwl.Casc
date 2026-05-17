using System;
using System.Collections.Generic;

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
    private readonly int[] _affixSnoIds;

    private ParagonGlyphDefinition(int snoId, int[] affixSnoIds)
    {
        SnoId = snoId;
        _affixSnoIds = affixSnoIds;
    }

    /// <summary>The glyph's own SNO id.</summary>
    public int SnoId { get; }

    /// <summary>The glyph's ParagonGlyphAffix SNO ids (0..3, in slot order).</summary>
    public IReadOnlyList<int> AffixSnoIds => _affixSnoIds;

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
