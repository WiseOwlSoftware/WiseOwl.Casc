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
