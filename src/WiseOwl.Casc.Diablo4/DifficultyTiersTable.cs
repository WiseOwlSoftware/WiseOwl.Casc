using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>DifficultyTiers</c> GameBalance table (SNO
/// <b>1973217</b>, group 20): the per-<b>monster-level</b> scaling curve — the
/// monster/content analogue of the player-side <see cref="LevelScalingTable"/>
/// (FR-C34, CL-101). 150 rows, monster levels <c>1..150</c>, row index =
/// <c>level − 1</c>.
/// </summary>
/// <remarks>
/// <para><b>Two separate curves (§8.2 reconciliation).</b> This is <i>not</i>
/// the difficulty ladder (that lives in <c>StringList 216612</c>) and it is
/// <i>not</i> the same curve as <see cref="LevelScalingTable.HpScalar(int)"/>.
/// The player-side <c>hpScalar</c> reaches ×30.5 at level 70; this table's
/// monster-HP column reaches <b>×101,051</b> at level 70 — a far steeper,
/// independent curve (~3,300× different). Monsters scale their HP off
/// <i>this</i> table, not off <c>LevelScaling</c> rows 71–200.</para>
/// <para><b>Label honesty (AC-3).</b> The row-layout is locked by an
/// <i>independent</i> anchor: <see cref="PerLevelXpValue(int)"/> reproduces the
/// game's per-level XP curve exactly (L40 = <c>8.0</c>, L70 = <c>11.0</c>;
/// <c>+0.1</c>/level from ~L40) — so the stride/offsets are correct and every
/// column is <i>located</i> correctly. But <see cref="MonsterHpScalar(int)"/>
/// (col <c>+4</c>) and <see cref="MonsterDamageScalar(int)"/> (col <c>+8</c>)
/// carry <b>inferred</b> semantics (devlog 0084): both are per-level multipliers
/// (×1.0 at level 1, monotonic), but the "monster HP / damage" meaning
/// <b>cannot be owner-validated</b> — Diablo IV shows monster health as a bar
/// only, with no numeric readout (owner-confirmed). Treat those two names as the
/// best structural inference, not a verified label.</para>
/// <para><b>Byte layout</b> (verified <c>3.1.1.72836</c>; §8.2): a
/// <c>DT_VARIABLEARRAY</c> descriptor at payload <c>+0x50</c>
/// (<c>dataOffset@+0</c> = 88 / <c>byteSize@+4</c> = 19200) → <b>150 rows × 128
/// bytes</b>. Columns: <c>+0</c> <c>int32</c> level; <c>+4</c>/<c>+8</c> the
/// HP/damage multipliers; <c>+36</c> the XP anchor; <c>+40</c> a
/// per-level gold multiplier (candidate). The remaining columns are per-level
/// reward/scaling coefficients (a mix of small <c>int</c> flags and <c>float</c>
/// scalars) exposed unlabeled on <see cref="DifficultyTierRow.Columns"/>.</para>
/// </remarks>
public sealed class DifficultyTiersTable
{
    /// <summary>The default <c>DifficultyTiers</c> GameBalance SNO id.</summary>
    public const int DefaultSnoId = 1973217;

    private const int VlaDescriptorOffset = 0x50;
    private const int RowStride = 128;

    private readonly DifficultyTierRow[] _rows;   // indexed by (level - 1)

    private DifficultyTiersTable(int snoId, DifficultyTierRow[] rows)
    {
        SnoId = snoId;
        _rows = rows;
    }

    /// <summary>The table's SNO id (1973217).</summary>
    public int SnoId { get; }

    /// <summary>Number of monster levels the table defines (150).</summary>
    public int LevelCount => _rows.Length;

    /// <summary>All rows, ordered by level (index 0 = level 1).</summary>
    public IReadOnlyList<DifficultyTierRow> Rows => _rows;

    /// <summary>The row for <paramref name="level"/> (1-based).</summary>
    /// <param name="level">1-based monster level (1..<see cref="LevelCount"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is
    /// outside <c>1..<see cref="LevelCount"/></c>.</exception>
    public DifficultyTierRow Row(int level)
    {
        if (level < 1 || level > _rows.Length)
            throw new ArgumentOutOfRangeException(nameof(level));
        return _rows[level - 1];
    }

    /// <summary>The per-level monster <b>HP multiplier</b> at
    /// <paramref name="level"/> (col <c>+4</c>; <c>1.0</c> at level 1). The
    /// "monster HP" semantic is <b>inferred</b> and unvalidated — see the type
    /// remarks (AC-3).</summary>
    public float MonsterHpScalar(int level) => Row(level).MonsterHpScalar;

    /// <summary>The per-level monster <b>damage multiplier</b> at
    /// <paramref name="level"/> (col <c>+8</c>; <c>1.0</c> at level 1). The
    /// "monster damage" semantic is <b>inferred</b> and unvalidated — see the
    /// type remarks (AC-3).</summary>
    public float MonsterDamageScalar(int level) => Row(level).MonsterDamageScalar;

    /// <summary>The <b>per-level XP value</b> at <paramref name="level"/> (col
    /// <c>+36</c>). This is the anchor column — it reproduces the game's XP
    /// curve exactly (L40 = 8.0, L70 = 11.0), which is what locks the row
    /// layout.</summary>
    public float PerLevelXpValue(int level) => Row(level).PerLevelXpValue;

    /// <summary>The <b>per-level gold value</b> at <paramref name="level"/> (col
    /// <c>+40</c>; candidate — co-located with the XP anchor, not independently
    /// oracled).</summary>
    public float PerLevelGoldValue(int level) => Row(level).PerLevelGoldValue;

    /// <summary>Decode a <c>DifficultyTiers</c> table from its raw SNO blob (the
    /// <c>.gam</c> for SNO 1973217).</summary>
    /// <exception cref="CascFormatException">The blob's row VLA is missing or
    /// malformed.</exception>
    public static DifficultyTiersTable Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        int snoId = r.SnoId;
        if (r.PayloadBase + VlaDescriptorOffset + 8 > r.Length)
            throw new CascFormatException(
                $"DifficultyTiers SNO {snoId} is too short for the row VLA descriptor.");
        int dataOff = r.I32(VlaDescriptorOffset);
        int byteSize = r.I32(VlaDescriptorOffset + 4);
        if (dataOff <= 0 || byteSize <= 0 || byteSize % RowStride != 0 ||
            r.PayloadBase + dataOff + byteSize > r.Length)
            throw new CascFormatException(
                $"DifficultyTiers SNO {snoId} row VLA is malformed " +
                $"(dataOffset={dataOff}, byteSize={byteSize}, stride={RowStride}).");

        int rows = byteSize / RowStride;
        var result = new DifficultyTierRow[rows];
        int cols = RowStride / 4;
        for (int i = 0; i < rows; i++)
        {
            int rowOff = dataOff + i * RowStride;
            var words = new float[cols];
            for (int c = 0; c < cols; c++)
                words[c] = r.F32(rowOff + c * 4);
            int level = r.I32(rowOff);   // col +0 is an int32, not a float
            result[i] = new DifficultyTierRow(level, words);
        }
        return new DifficultyTiersTable(snoId, result);
    }
}

/// <summary>
/// One <see cref="DifficultyTiersTable"/> row — a monster level's scaling
/// coefficients (FR-C34, CL-101). Carries the verified/inferred typed columns
/// plus the full raw column vector for the unlabeled coefficients.
/// </summary>
public sealed class DifficultyTierRow
{
    private readonly float[] _columns;

    internal DifficultyTierRow(int level, float[] columns)
    {
        Level = level;
        _columns = columns;
    }

    /// <summary>The 1-based monster level (col <c>+0</c>, an <c>int32</c>). Row
    /// <i>i</i> ↔ level <i>i</i>+1.</summary>
    public int Level { get; }

    /// <summary>Per-level monster HP multiplier (col <c>+4</c>). Semantic
    /// <b>inferred</b> — see <see cref="DifficultyTiersTable"/> remarks.</summary>
    public float MonsterHpScalar => _columns[1];

    /// <summary>Per-level monster damage multiplier (col <c>+8</c>). Semantic
    /// <b>inferred</b>.</summary>
    public float MonsterDamageScalar => _columns[2];

    /// <summary>Per-level XP value (col <c>+36</c>) — the anchor column (L40 =
    /// 8.0, L70 = 11.0).</summary>
    public float PerLevelXpValue => _columns[9];

    /// <summary>Per-level gold value (col <c>+40</c>; candidate).</summary>
    public float PerLevelGoldValue => _columns[10];

    /// <summary>Every column of the 128-byte row reinterpreted as
    /// <see cref="float"/> (32 entries; index <i>c</i> = byte column
    /// <c>+4·c</c>). <b>Caveats:</b> <c>Columns[0]</c> is the level as an
    /// <c>int32</c> (use <see cref="Level"/>), and columns 3/4/5/6 (bytes
    /// <c>+12..+24</c>) are small <c>int</c> flags, not floats — reinterpret
    /// those as <c>int</c> via <see cref="BitConverter.SingleToInt32Bits(float)"/>.
    /// The labeled columns (1/2/9/10) are the typed accessors above; the rest
    /// are unlabeled per-level reward/scaling coefficients, exposed raw so no
    /// data is hidden (comprehensive-data-exposure) without asserting names that
    /// can't be verified.</summary>
    public IReadOnlyList<float> Columns => _columns;
}
