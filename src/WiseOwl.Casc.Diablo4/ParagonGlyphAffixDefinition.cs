using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>ParagonGlyphAffixDefinition</c> (<c>.gaf</c>, SNO
/// group 112). Raw fields only — magnitude interpretation (operation
/// semantics, level scaling, thresholds) stays with the consumer.
/// </summary>
/// <remarks>
/// Byte layout per the canonical reference (<c>docs/casc-format.md</c>,
/// migrated/verified from upstream <c>d4-binary-formats.md §5</c>,
/// <c>ParagonGlyphAffixDefinition — VERIFIED</c>, formatHash 353797140):
/// payload base <c>0x10</c>; <c>snoId@0</c>;
/// <c>eAffectedNodeRarity@24</c> (DT_ENUM, Maxroll-compact
/// 1=Normal/2=Magic/3=Rare); <c>eBonusOperation@48</c> (DT_ENUM);
/// <c>flStartingBonusScalar@76</c> (DT_FLOAT, == Maxroll <c>base</c>);
/// <c>flAddedBonusScalarPerLevel@80</c> (DT_FLOAT, == Maxroll
/// <c>perLevel</c>). Op-5 (<c>Power_*</c>) carries no base/per — its
/// magnitude is in the threshold chain (consumer concern).
/// </remarks>
public sealed class ParagonGlyphAffixDefinition
{
    private ParagonGlyphAffixDefinition(
        int snoId, int affectedRarity, int operation, float @base, float perLevel)
    {
        SnoId = snoId;
        AffectedRarity = affectedRarity;
        Operation = operation;
        Base = @base;
        PerLevel = perLevel;
    }

    /// <summary>The affix's own SNO id.</summary>
    public int SnoId { get; }

    /// <summary><c>eAffectedNodeRarity</c> (<c>+24</c>) — Maxroll-compact
    /// 1=Normal, 2=Magic, 3=Rare.</summary>
    public int AffectedRarity { get; }

    /// <summary><c>eBonusOperation</c> (<c>+48</c>) — 1/2/4/5 (Attribute /
    /// NodeAmplification / Power / AttributeConversion).</summary>
    public int Operation { get; }

    /// <summary><c>flStartingBonusScalar</c> (<c>+76</c>) — the base
    /// magnitude (level-invariant; == Maxroll <c>base</c>).</summary>
    public float Base { get; }

    /// <summary><c>flAddedBonusScalarPerLevel</c> (<c>+80</c>) — per-level
    /// magnitude increment (== Maxroll <c>perLevel</c>).</summary>
    public float PerLevel { get; }

    /// <summary>Decode a ParagonGlyphAffix from its raw SNO blob.</summary>
    public static ParagonGlyphAffixDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        return new ParagonGlyphAffixDefinition(
            snoId: r.SnoId,
            affectedRarity: r.I32(24),
            operation: r.I32(48),
            @base: r.F32(76),
            perLevel: r.F32(80));
    }
}
