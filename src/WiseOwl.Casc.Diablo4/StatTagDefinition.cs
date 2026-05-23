using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV stat-threshold tag record (SNO group
/// <see cref="SnoGroup.StatTag"/> = 124). Surfaces the
/// <see cref="ThresholdFormulaText"/> only — evaluation (substituting the
/// <c>ParagonBoardEquipIndex</c> binding and reducing the expression to a
/// numeric threshold) is the consumer's, mirroring the
/// <see cref="AttributeFormulaTable"/> boundary in
/// <c>docs/casc-diablo4-format.md</c> Appendix C.
/// </summary>
/// <remarks>
/// Group 124 names follow the pattern <c>&lt;Stat&gt;{Main|Side}{Tier}</c>
/// (<c>WillpowerMain2</c>, <c>StrengthSide1</c>, …), with class-keyed
/// composite variants (<c>Barb_Strength+Dexterity</c>) and glyph-keyed
/// variants (<c>Glyph_Willpower_Main</c>). Two consumers reference these
/// records:
/// <list type="bullet">
///   <item>a rare <see cref="ParagonNodeDefinition"/> via
///     <see cref="ParagonNodeDefinition.BonusStatTagSnoIds"/> (the "bonus
///     when threshold met" gating); and</item>
///   <item>glyphs via their own activation gates (out of this CL's scope).</item>
/// </list>
/// Byte layout (payload base <c>0x10</c>; verified on build <c>3.0.2.71886</c>):
/// <c>snoId@0</c>; a <c>DT_VARIABLEARRAY[DT_CHAR]</c> descriptor at payload
/// <c>+64</c> whose <c>dataOffset@+8</c> / <c>dataSize@+12</c> reference the
/// ASCII formula text (no null counted in <c>dataSize</c>); a parallel
/// <c>DT_VARIABLEARRAY</c> descriptor at payload <c>+80</c> carrying the
/// pre-parsed token stream (not modeled — the text is the authoritative
/// source; the token stream is the engine's bytecode equivalent).
/// <para>
/// Simple tags (<c>WillpowerMain2</c>, etc.) hold a single formula whose
/// text sits at payload <c>+96</c>; class-composite tags
/// (<c>Barb_Strength+Dexterity</c>) carry the primary formula at a later
/// offset with additional structured sub-records for the per-alternative
/// stats (not yet modeled — the canonical engine field names have not
/// been recovered; surfacing the primary text only is the conservative
/// first-cut). Glyph-keyed tags carry a numeric constant
/// (<c>Glyph_Willpower_Main</c> → <c>"40"</c>) rather than a formula.
/// </para>
/// </remarks>
public sealed class StatTagDefinition
{
    /// <summary>Payload offset of the <c>DT_VARIABLEARRAY[DT_CHAR]</c>
    /// descriptor pointing to the formula text.</summary>
    private const int FormulaTextArrayDescriptor = 64;

    private StatTagDefinition(int snoId, string thresholdFormulaText)
    {
        SnoId = snoId;
        ThresholdFormulaText = thresholdFormulaText;
    }

    /// <summary>The record's own SNO id (group 124).</summary>
    public int SnoId { get; }

    /// <summary>The ASCII formula text whose evaluation yields the stat
    /// threshold the gated bonus requires. For paragon-node-referenced tags
    /// the expression binds <c>ParagonBoardEquipIndex</c>
    /// (<c>"760 + (455 * ParagonBoardEquipIndex)"</c> →
    /// <c>2125</c> when <c>EquipIndex == 3</c>); for glyph-referenced tags
    /// the text is typically a bare numeric constant
    /// (<c>"40"</c>). Empty when the descriptor is missing or
    /// out-of-range.</summary>
    public string ThresholdFormulaText { get; }

    /// <summary>Decode a stat-threshold tag from its raw SNO blob.</summary>
    public static StatTagDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        var snoId = r.SnoId;

        var formulaBytes = r.VariableArray(FormulaTextArrayDescriptor);
        // The descriptor's dataOffset is payload-relative; SnoRecord.Ascii
        // wants the same. We've already sliced into the payload via the
        // VariableArray helper, so just decode the bytes directly without
        // re-computing the offset.
        var text = formulaBytes.Length == 0
            ? string.Empty
            : DecodeNulTerminatedAscii(formulaBytes);

        return new StatTagDefinition(snoId, text);
    }

    /// <summary>Decode an ASCII byte span, stopping at the first NUL (the
    /// engine writes the trailing NUL in some records and omits it in
    /// others — match either).</summary>
    private static string DecodeNulTerminatedAscii(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        if (end < 0) end = bytes.Length;
        return System.Text.Encoding.ASCII.GetString(bytes[..end]);
    }
}
