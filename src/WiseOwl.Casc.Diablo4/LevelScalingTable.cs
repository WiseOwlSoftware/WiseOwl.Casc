using System;
using System.Collections.Generic;

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
/// is the <c>float</c> at row column <c>+4</c>. The remaining columns are now
/// exposed <b>raw</b> on <see cref="LevelScalingRow.Columns"/> (CL-102) — the
/// Maxroll dump names some (<c>monsterDr</c>, <c>powerBase/Delta/Item</c>,
/// <c>xpScalar</c>) but those names are <b>not</b> asserted: only <c>hpScalar</c>
/// is oracle-anchored; the rest cannot be verified from the blob (no anchor, no
/// in-game readout), so they ship unlabeled with their per-level behavior
/// characterised in §8.2.</para>
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

    private readonly LevelScalingRow[] _rows;   // indexed by (level - 1)

    private LevelScalingTable(int snoId, LevelScalingRow[] rows)
    {
        SnoId = snoId;
        _rows = rows;
    }

    /// <summary>The table's SNO id (206158).</summary>
    public int SnoId { get; }

    /// <summary>Number of rows (levels) the table defines (200 — characters
    /// 1..70, monsters/content 71..200).</summary>
    public int LevelCount => _rows.Length;

    /// <summary>All rows, ordered by level (index 0 = level 1).</summary>
    public IReadOnlyList<LevelScalingRow> Rows => _rows;

    /// <summary>The row for <paramref name="level"/> (1-based) — the labeled
    /// <see cref="LevelScalingRow.HpScalar"/> plus the full raw column vector
    /// for the unlabeled columns (CL-102).</summary>
    /// <param name="level">1-based level (1..<see cref="LevelCount"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is
    /// outside <c>1..<see cref="LevelCount"/></c>.</exception>
    public LevelScalingRow Row(int level)
    {
        if (level < 1 || level > _rows.Length)
            throw new ArgumentOutOfRangeException(nameof(level));
        return _rows[level - 1];
    }

    /// <summary>The <c>hpScalar</c> at <paramref name="level"/> (row column
    /// <c>+4</c>) — the multiplier applied to <see cref="BaseHitpointsMax"/>.
    /// <c>1.0</c> at level 1.</summary>
    /// <param name="level">1-based level (1..<see cref="LevelCount"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is
    /// outside <c>1..<see cref="LevelCount"/></c>.</exception>
    public float HpScalar(int level) => Row(level).HpScalar;

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
        int cols = RowStride / 4;
        var result = new LevelScalingRow[rows];
        for (int i = 0; i < rows; i++)
        {
            int rowOff = dataOff + i * RowStride;
            var words = new float[cols];
            for (int c = 0; c < cols; c++)
                words[c] = r.F32(rowOff + c * 4);
            result[i] = new LevelScalingRow(i + 1, words);   // level = row index + 1
        }
        return new LevelScalingTable(snoId, result);
    }
}

/// <summary>
/// One <see cref="LevelScalingTable"/> row — a level's scaling coefficients
/// (CL-102). Carries the verified <see cref="HpScalar"/> plus the full raw
/// column vector for the remaining, unlabeled columns.
/// </summary>
/// <remarks>
/// <b>Unlabeled columns are exposed raw, not renamed.</b> The 212-byte row has
/// ~10 non-zero columns, but only <c>+4</c> (<see cref="HpScalar"/>) is
/// oracle-anchored (it drives base Max Life, §8.2). The Maxroll dump names the
/// others <c>monsterDr</c> / <c>powerBase</c> / <c>powerDelta</c> /
/// <c>powerItem</c> / <c>xpScalar</c>, but — unlike <c>DifficultyTiers</c>'s XP
/// column (§8.3), which has an independent anchor — <b>none of these can be
/// verified from the blob</b> (no anchor, no in-game oracle), so this library
/// does <b>not</b> assert those names. Their per-level behavior is characterised
/// in §8.2; they are exposed by byte offset on <see cref="Columns"/> so consumers
/// have every column without a label that can't be stood behind.
/// </remarks>
public sealed class LevelScalingRow
{
    private readonly float[] _columns;

    internal LevelScalingRow(int level, float[] columns)
    {
        Level = level;
        _columns = columns;
    }

    /// <summary>The 1-based level this row describes (row index + 1). Character
    /// levels are <c>1..70</c>; <c>71..200</c> are content levels.</summary>
    public int Level { get; }

    /// <summary>The verified <c>hpScalar</c> (col <c>+4</c>) — the multiplier on
    /// <see cref="LevelScalingTable.BaseHitpointsMax"/> that yields base Max Life
    /// (§8.2). <c>1.0</c> at level 1.</summary>
    public float HpScalar => _columns[1];

    /// <summary>Every column of the 212-byte row reinterpreted as
    /// <see cref="float"/> (53 entries; index <i>c</i> = byte column
    /// <c>+4·c</c>). Only <c>Columns[1]</c> (<see cref="HpScalar"/>) is a labeled,
    /// verified column; the rest are unlabeled per-level coefficients (see the
    /// type remarks — the Maxroll names are <b>not</b> asserted). Column 0 reads
    /// <c>0</c> in this table (the level is <i>not</i> stored per row — it is
    /// implied by row order; use <see cref="Level"/>); many trailing columns are
    /// <c>0</c> too.</summary>
    public IReadOnlyList<float> Columns => _columns;
}
