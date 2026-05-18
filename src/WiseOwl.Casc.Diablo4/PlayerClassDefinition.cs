using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>PlayerClassDefinition</c> (<c>.prd</c>, SNO group
/// <see cref="SnoGroup.PlayerClass"/> = 74) — the playable character class
/// record. Raw fields only (the class roster + localized names are
/// <see cref="Diablo4Storage.ReadCharacterClasses"/>; this is the typed
/// record reader, C6).
/// </summary>
/// <remarks>
/// Byte layout (clean-room, <c>docs/casc-diablo4-format.md §11.1</c>,
/// Appendix A CL-21): payload base <c>0x10</c>; <c>snoId</c> at payload
/// <c>0</c>; <c>eClass</c> (the game's internal class enum ordinal) at
/// payload <c>16</c>. The <c>eClass</c> ordinal is sparse but stable
/// (Sorcerer 0, Barbarian 1, Rogue 3, Druid 5, Necromancer 6, Spiritborn
/// 7, Paladin 9, Warlock 10 on build 3.0.2.71886) and is the ordering
/// behind the glyph <c>fUsableByClass</c> rank (§7.3 / FR-D3).
/// </remarks>
public sealed class PlayerClassDefinition
{
    private PlayerClassDefinition(int snoId, int eClass)
    {
        SnoId = snoId;
        EClass = eClass;
    }

    /// <summary>The class's own SNO id (== the CoreTOC id; the stable
    /// per-class key shared with <see cref="CharacterClass.SnoId"/> and
    /// <see cref="ParagonBoardDefinition.ClassSnoId"/>).</summary>
    public int SnoId { get; }

    /// <summary>The game's internal class enum ordinal (<c>eClass</c>,
    /// payload <c>+16</c>). Sparse but stable; ranking the real-class
    /// roster by this value yields the glyph class-array slot order
    /// (§7.3).</summary>
    public int EClass { get; }

    /// <summary>Decode a PlayerClass from its raw SNO blob.</summary>
    public static PlayerClassDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        return new PlayerClassDefinition(r.SnoId, r.I32(16));
    }
}
