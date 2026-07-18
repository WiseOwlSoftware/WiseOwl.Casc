using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>MonsterLevelCurves</c> GameBalance table (SNO
/// <b>1610053</b>, group 20): the per-<b>raid-tier</b> monster-level scaling
/// curves (FR-C36, CL-110). Six tiers (<c>Raid_Tier_0</c> … <c>Raid_Tier_5</c>),
/// each a short curve mapping a monster/area level to a <b>scaled effective
/// value</b> — how a raid tier re-levels the encounter.
/// </summary>
/// <remarks>
/// <para><b>Correction.</b> A prior CASC finding recorded this SNO as "an empty
/// name-fragment registry, no per-tier curve." That was wrong — the curves are
/// here. The record's <c>+0x50</c> array holds <b>6 × 320-byte tier records</b>
/// (<c>Raid_Tier_0..5</c> named inline), and each carries a
/// <c>DT_VARIABLEARRAY</c> at record offset <c>+312</c> pointing to its curve
/// rows in the record tail.</para>
/// <para><b>Curve shape</b> (verified <c>3.1.1.72836</c>). Each tier's curve is a
/// list of <b>12-byte rows</b> = two <c>int32</c> + one <c>float32</c>. The two
/// ints are equal in the live data (a level), and the float is the scaled value,
/// which climbs from a small base to <c>100</c> across the tier's level span —
/// e.g. Tier 0 spans levels 55→95 with scaled values reaching 100; higher tiers
/// start at a higher base level (Tier 1 at 65, … Tier 5 at 105) and have fewer
/// rows. The exact remap semantics (which int is input vs. band bound, whether
/// the float is an effective level or a multiplier) are a <b>structural
/// inference</b>, not owner-validated — so the row is exposed with descriptive
/// names <i>and</i> the raw values (<see cref="MonsterLevelCurvePoint"/>) so no
/// data is hidden and no unverifiable formula is asserted
/// (comprehensive-data-exposure + calibrate-claims-to-evidence).</para>
/// </remarks>
public sealed class MonsterLevelCurvesTable
{
    /// <summary>The default <c>MonsterLevelCurves</c> GameBalance SNO id.</summary>
    public const int DefaultSnoId = 1610053;

    private const int TierListDescriptorOffset = 0x50;  // dataOff@+0x50 / byteSize@+0x54
    private const int TierRecordStride = 320;
    private const int TierNameMaxLength = 64;
    private const int CurveDescriptorOffset = 312;      // within a tier record: dataOff / byteSize
    private const int CurveRowStride = 12;              // int32 + int32 + float32

    private readonly MonsterLevelCurve[] _tiers;

    private MonsterLevelCurvesTable(int snoId, MonsterLevelCurve[] tiers)
    {
        SnoId = snoId;
        _tiers = tiers;
    }

    /// <summary>The table's SNO id (1610053).</summary>
    public int SnoId { get; }

    /// <summary>The raid-tier curves, ordered <c>Raid_Tier_0</c> … <c>Raid_Tier_5</c>
    /// (index = tier number).</summary>
    public IReadOnlyList<MonsterLevelCurve> Tiers => _tiers;

    /// <summary>Decode a <c>MonsterLevelCurves</c> table from its raw SNO blob
    /// (the <c>.gam</c> for SNO 1610053).</summary>
    /// <exception cref="CascFormatException">The tier-list VLA is missing or
    /// malformed.</exception>
    public static MonsterLevelCurvesTable Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        int snoId = r.SnoId;
        if (r.PayloadBase + TierListDescriptorOffset + 8 > r.Length)
            throw new CascFormatException(
                $"MonsterLevelCurves SNO {snoId} is too short for the tier-list VLA descriptor.");
        int dataOff = r.I32(TierListDescriptorOffset);
        int byteSize = r.I32(TierListDescriptorOffset + 4);
        if (dataOff <= 0 || byteSize <= 0 || byteSize % TierRecordStride != 0 ||
            r.PayloadBase + dataOff + byteSize > r.Length)
            throw new CascFormatException(
                $"MonsterLevelCurves SNO {snoId} tier-list VLA is malformed " +
                $"(dataOffset={dataOff}, byteSize={byteSize}, stride={TierRecordStride}).");

        int tierCount = byteSize / TierRecordStride;
        var tiers = new MonsterLevelCurve[tierCount];
        for (int t = 0; t < tierCount; t++)
        {
            int recOff = dataOff + t * TierRecordStride;
            string name = ReadInlineName(r, recOff);
            var points = ReadCurve(r, recOff);
            tiers[t] = new MonsterLevelCurve(t, name, points);
        }
        return new MonsterLevelCurvesTable(snoId, tiers);
    }

    private static string ReadInlineName(SnoRecord r, int recOff)
    {
        int max = Math.Min(TierNameMaxLength, r.Length - r.PayloadBase - recOff);
        if (max <= 0) return string.Empty;
        string raw = r.Ascii(recOff, max);
        int end = 0;
        while (end < raw.Length && raw[end] is >= (char)0x20 and < (char)0x7F) end++;
        return raw[..end];
    }

    private static MonsterLevelCurvePoint[] ReadCurve(SnoRecord r, int recOff)
    {
        int descOff = recOff + CurveDescriptorOffset;
        if (r.PayloadBase + descOff + 8 > r.Length) return [];
        int dataOff = r.I32(descOff);
        int byteSize = r.I32(descOff + 4);
        if (dataOff <= 0 || byteSize <= 0 || byteSize % CurveRowStride != 0 ||
            r.PayloadBase + dataOff + byteSize > r.Length)
            return [];

        int rows = byteSize / CurveRowStride;
        var points = new MonsterLevelCurvePoint[rows];
        for (int i = 0; i < rows; i++)
        {
            int rowOff = dataOff + i * CurveRowStride;
            points[i] = new MonsterLevelCurvePoint(
                r.I32(rowOff), r.I32(rowOff + 4), r.F32(rowOff + 8));
        }
        return points;
    }
}

/// <summary>
/// One <see cref="MonsterLevelCurvesTable"/> raid-tier curve (FR-C36, CL-110) —
/// the level-scaling curve for a single <c>Raid_Tier_N</c>.
/// </summary>
public sealed class MonsterLevelCurve
{
    private readonly MonsterLevelCurvePoint[] _points;

    internal MonsterLevelCurve(int tierIndex, string name, MonsterLevelCurvePoint[] points)
    {
        TierIndex = tierIndex;
        Name = name;
        _points = points;
    }

    /// <summary>The tier number (0..5) — <c>Raid_Tier_<see cref="TierIndex"/></c>.</summary>
    public int TierIndex { get; }

    /// <summary>The tier's inline record name (e.g. <c>"Raid_Tier_0"</c>).</summary>
    public string Name { get; }

    /// <summary>The curve rows, in file order (ascending level). Empty (never
    /// <see langword="null"/>) if the tier's curve descriptor is absent/malformed.</summary>
    public IReadOnlyList<MonsterLevelCurvePoint> Points => _points;
}

/// <summary>
/// One row of a <see cref="MonsterLevelCurve"/> — a 12-byte record (two
/// <c>int32</c> + one <c>float32</c>). <see cref="Level"/>/<see cref="LevelHigh"/>
/// are the (equal, in live data) monster level and <see cref="ScaledValue"/> the
/// scaled effective value; the precise remap semantic is a structural inference
/// (see <see cref="MonsterLevelCurvesTable"/> remarks) — the raw fields are all
/// exposed.
/// </summary>
/// <param name="Level">First <c>int32</c> — the monster/area level (row +0).</param>
/// <param name="LevelHigh">Second <c>int32</c> — equal to <see cref="Level"/> in
/// the live data; the upper bound of the level band if it is one (row +4).</param>
/// <param name="ScaledValue">The <c>float32</c> scaled value (row +8) — climbs to
/// <c>100</c> across the tier's level span.</param>
public readonly record struct MonsterLevelCurvePoint(int Level, int LevelHigh, float ScaledValue);
