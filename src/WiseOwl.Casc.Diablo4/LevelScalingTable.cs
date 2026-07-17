using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>LevelScaling</c> GameBalance table (SNO
/// <b>206158</b>, group 20): the per-level scaling curve that drives base
/// <b>Max Life</b> (and monster scaling). Indexed <c>1..200</c>; character
/// levels occupy <c>1..70</c> (<see cref="MaxCharacterLevel"/> — the cap on
/// build <c>3.1.1.72836</c>), while <c>71..200</c> are monster / content
/// levels — one <see cref="HpScalar(int)"/> column serves both populations.
/// </summary>
/// <remarks>
/// <para><b>Base Life (FR-C29 Phase 2, CL-99).</b> A character's base Max Life
/// is <b>class-independent</b> — every class reads the same value:
/// <see cref="BaseLife(int)"/> <c>= round(<see cref="BaseHitpointsMax"/> ×
/// <see cref="HpScalar(int)"/>(level))</c>, round-half-away-from-zero. The
/// <c>1526</c> a Level-70 tooltip shows is that rounded product — it exists
/// nowhere in the data; the operands (<c>50</c> and the scalar) do
/// (<c>round(50 × 30.526) = 1526</c>; <c>round(50 × 17.200) = 860</c> at
/// L60; <c>round(50 × 1.03) = 52</c> at L2 — the sole exact-<c>.5</c> case,
/// which pins the rounding mode). Cross-validated 15/15 against owner oracles
/// including out-of-sample L11–L14.</para>
/// <para><b>Byte layout</b> (verified <c>3.1.1.72836</c>; <c>docs/casc-diablo4-format.md
/// §8.2</c>): a <c>DT_VARIABLEARRAY</c> descriptor at payload <c>+0x50</c>
/// (<c>dataOffset@+0</c> / <c>byteSize@+4</c>) → <b>200 rows × 212 bytes</b>;
/// row index = <c>level − 1</c>; <c>hpScalar</c> (<see cref="HpScalar(int)"/>)
/// is the <c>float</c> at row column <c>+4</c>. Other columns
/// (<c>monsterDr</c>, <c>powerBase/Delta/Item</c>, <c>xpScalar</c>) exist but
/// are not modeled here — this reader surfaces only the byte-verified
/// <c>hpScalar</c> and the base-Life projection it feeds.</para>
/// </remarks>
public sealed class LevelScalingTable
{
    /// <summary>The maximum character level (<c>heroDetails</c> id 279 on build
    /// <c>3.1.1.72836</c>). Table indices above this are monster / content
    /// levels, not characters.</summary>
    public const int MaxCharacterLevel = 70;

    /// <summary>The class-independent base <c>Hitpoints_Max</c> that the
    /// per-level <see cref="HpScalar(int)"/> scales (CL-99). Cross-validated
    /// 15/15 against owner oracles; <b>fitted, not yet located</b> as a readable
    /// field in the data — baked as a constant per the engine-constants pattern
    /// (a future build re-verifies via <see cref="BaseLife(int)"/>).</summary>
    public const float BaseHitpointsMax = 50f;

    private const int VlaDescriptorOffset = 0x50;
    private const int RowStride = 212;
    private const int HpScalarColumn = 4;

    private readonly float[] _hpScalar;   // indexed by (level - 1)

    private LevelScalingTable(int snoId, float[] hpScalar)
    {
        SnoId = snoId;
        _hpScalar = hpScalar;
    }

    /// <summary>The table's SNO id (206158).</summary>
    public int SnoId { get; }

    /// <summary>Number of rows (levels) the table defines (200 — characters
    /// 1..70, monsters/content 71..200).</summary>
    public int LevelCount => _hpScalar.Length;

    /// <summary>The <c>hpScalar</c> at <paramref name="level"/> (row column
    /// <c>+4</c>) — the multiplier applied to <see cref="BaseHitpointsMax"/>.
    /// <c>1.0</c> at level 1.</summary>
    /// <param name="level">1-based level (1..<see cref="LevelCount"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is
    /// outside <c>1..<see cref="LevelCount"/></c>.</exception>
    public float HpScalar(int level)
    {
        if (level < 1 || level > _hpScalar.Length)
            throw new ArgumentOutOfRangeException(nameof(level));
        return _hpScalar[level - 1];
    }

    /// <summary>The character's <b>base Max Life</b> at
    /// <paramref name="level"/> — <c>round(<see cref="BaseHitpointsMax"/> ×
    /// <see cref="HpScalar(int)"/>(level))</c>, round-half-away-from-zero,
    /// class-independent (FR-C29 Phase 2). Defined for character levels
    /// <c>1..<see cref="MaxCharacterLevel"/></c>.</summary>
    /// <param name="level">1-based character level
    /// (1..<see cref="MaxCharacterLevel"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is
    /// outside the character range.</exception>
    public int BaseLife(int level)
    {
        if (level < 1 || level > MaxCharacterLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        return (int)Math.Round(BaseHitpointsMax * HpScalar(level),
            MidpointRounding.AwayFromZero);
    }

    /// <summary>Decode a <c>LevelScaling</c> table from its raw SNO blob (the
    /// <c>.gam</c> for SNO 206158).</summary>
    /// <exception cref="CascFormatException">The blob's <c>LevelScaling</c> VLA
    /// is missing or malformed.</exception>
    public static LevelScalingTable Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        int snoId = r.SnoId;
        if (r.PayloadBase + VlaDescriptorOffset + 8 > r.Length)
            throw new CascFormatException(
                $"LevelScaling SNO {snoId} is too short for the row VLA descriptor.");
        int dataOff = r.I32(VlaDescriptorOffset);
        int byteSize = r.I32(VlaDescriptorOffset + 4);
        if (dataOff <= 0 || byteSize <= 0 || byteSize % RowStride != 0 ||
            r.PayloadBase + dataOff + byteSize > r.Length)
            throw new CascFormatException(
                $"LevelScaling SNO {snoId} row VLA is malformed " +
                $"(dataOffset={dataOff}, byteSize={byteSize}, stride={RowStride}).");

        int rows = byteSize / RowStride;
        var hpScalar = new float[rows];
        for (int i = 0; i < rows; i++)
            hpScalar[i] = r.F32(dataOff + i * RowStride + HpScalarColumn);
        return new LevelScalingTable(snoId, hpScalar);
    }
}
