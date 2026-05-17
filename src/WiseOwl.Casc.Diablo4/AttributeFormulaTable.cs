using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>One <c>arRanges</c> row of an <see cref="AttributeFormula"/>:
/// a power-range start, its two range values, and the formula source text.
/// Raw decoded fields — the library never evaluates the text.</summary>
/// <param name="ItemPowerRangeStart"><c>nItemPowerRangeStart</c> (<c>+0</c>).</param>
/// <param name="RangeValue1">First range value (<c>+4</c>, float).</param>
/// <param name="RangeValue2">Second range value (<c>+8</c>, float).</param>
/// <param name="FormulaText">The <c>DT_STRING_FORMULA</c> source text
/// (e.g. <c>"5"</c>, <c>"2 * ParagonPowerBudgetMultiplierNodeMagicOffensive()"</c>).
/// </param>
public readonly record struct FormulaRange(
    int ItemPowerRangeStart, float RangeValue1, float RangeValue2, string FormulaText);

/// <summary>One named GameBalance attribute formula entry
/// (<c>AttributeFormulaEntry</c>, stride 280).</summary>
/// <param name="Name"><c>szName</c> (inline <c>DT_CHARARRAY[256]</c>),
/// e.g. <c>ParagonNodeCoreStat_Normal</c>.</param>
/// <param name="NameGbid"><c>GbidHash(Name)</c> — the entry's identity (the
/// in-record <c>gbid</c> field is <c>0xFFFFFFFF</c>; identity is the name
/// hash, matching a node's <c>FormulaGbid</c>).</param>
/// <param name="Ranges">The <c>arRanges</c> rows (≥1; paragon stat nodes
/// have exactly one).</param>
public sealed record AttributeFormula(
    string Name, uint NameGbid, IReadOnlyList<FormulaRange> Ranges)
{
    /// <summary>The primary formula text (<c>arRanges[0].FormulaText</c>) —
    /// the value the consumer evaluates. Empty if the entry has no ranges.</summary>
    public string PrimaryText => Ranges.Count > 0 ? Ranges[0].FormulaText : string.Empty;
}

/// <summary>
/// A decoded Diablo IV GameBalance <c>AttributeFormulas</c> table
/// (<c>.gam</c>, SNO group 20; canonically SNO id <b>201912</b>): the
/// game's <c>name → formula text</c> table that paragon node/glyph
/// magnitudes reference by GBID. Raw text + indices only — evaluation and
/// the calibrated engine intrinsics are permanently the consumer's.
/// </summary>
/// <remarks>
/// Byte layout per the canonical reference (<c>docs/casc-diablo4-format.md §8</c>,
/// migrated/verified from upstream <c>d4-binary-formats.md §7.3-VERIFIED</c>).
/// Payload base <c>0x10</c>; <c>eGameBalanceType</c> at payload <c>+8</c>
/// must be <b>22</b> (AttributeFormulas) — other GameBalance table types
/// have different element structs and are out of scope here. The walk:
/// <c>ptData</c> polymorphic variable-array at payload <c>+16</c>
/// (<c>dataOffset@+8</c>); element region at <c>dataOffset</c>; an 8-byte
/// type tag precedes the table struct (<c>tableBase = dataOffset + 8</c>);
/// <c>tEntries</c> variable-array at <c>tableBase+16</c> (entry stride
/// <b>280</b>); each entry: inline <c>szName[256]@+0</c>,
/// <c>gbid@+256</c>, <c>arRanges</c> variable-array <c>@+264</c> (range
/// stride <b>48</b>); each range: <c>nItemPowerRangeStart@+0</c>, two
/// floats <c>@+4/+8</c>, <c>tFormula</c> <c>DT_STRING_FORMULA@+16</c>
/// (<c>FormulaOffset@+8</c>, <c>FormulaSize@+12</c>; text is ASCII at
/// payload <c>FormulaOffset</c>).
/// </remarks>
public sealed class AttributeFormulaTable
{
    private const int EntryStride = 280;
    private const int RangeStride = 48;

    /// <summary>The <c>eGameBalanceType</c> value this reader handles.</summary>
    public const int AttributeFormulasType = 22;

    private readonly Dictionary<string, string> _byName;
    private readonly Dictionary<uint, string> _nameByGbid;
    private readonly AttributeFormula[] _entries;

    private AttributeFormulaTable(
        int snoId, AttributeFormula[] entries,
        Dictionary<string, string> byName, Dictionary<uint, string> nameByGbid)
    {
        SnoId = snoId;
        _entries = entries;
        _byName = byName;
        _nameByGbid = nameByGbid;
    }

    /// <summary>The table's SNO id (201912 for the paragon table).</summary>
    public int SnoId { get; }

    /// <summary>Every parsed formula entry, in file order.</summary>
    public IReadOnlyList<AttributeFormula> Entries => _entries;

    /// <summary><c>name → primary formula text</c>
    /// (<c>arRanges[0].FormulaText</c>).</summary>
    public IReadOnlyDictionary<string, string> ByName => _byName;

    /// <summary>Resolve a formula's primary source text by name.</summary>
    public bool TryGetFormulaText(string name, out string text)
    {
        var ok = _byName.TryGetValue(name, out var v);
        text = v ?? string.Empty;
        return ok;
    }

    /// <summary>Resolve a formula name from a node's <c>FormulaGbid</c>
    /// (keyed on <c>GbidHash(szName)</c> — the entry's identity).</summary>
    public bool TryGetNameByGbid(uint gbid, out string name)
    {
        var ok = _nameByGbid.TryGetValue(gbid, out var v);
        name = v ?? string.Empty;
        return ok;
    }

    /// <summary>Decode an AttributeFormulas table from its raw SNO blob
    /// (the <c>.gam</c> for SNO 201912).</summary>
    /// <exception cref="CascFormatException">The blob is not an
    /// AttributeFormulas table (<c>eGameBalanceType != 22</c>).</exception>
    public static AttributeFormulaTable Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        var snoId = r.SnoId;                          // payload + 0
        var gbType = r.I32(8);                         // eGameBalanceType
        if (gbType != AttributeFormulasType)
            throw new CascFormatException(
                $"GameBalance SNO {snoId} is type {gbType}, not " +
                $"AttributeFormulas ({AttributeFormulasType}). General " +
                "GameBalance tables are out of scope (see feature-backlog C6).");

        // ptData polymorphic variable-array @ payload+16: dataOffset @ +8.
        var ptDataOffset = (int)r.U32(16 + 8);
        // 8-byte polymorphic type tag precedes the table struct.
        var tableBase = ptDataOffset + 8;
        // AttributeFormulaEntry_Table.tEntries variable-array @ tableBase+16:
        //   dataOffset @ +8, dataSize @ +12.
        var entriesOffset = (int)r.U32(tableBase + 16 + 8);
        var entriesSize = (int)r.U32(tableBase + 16 + 12);
        var entryCount = entriesSize > 0 ? entriesSize / EntryStride : 0;

        var entries = new AttributeFormula[entryCount];
        var byName = new Dictionary<string, string>(entryCount, StringComparer.Ordinal);
        var nameByGbid = new Dictionary<uint, string>(entryCount);

        for (var i = 0; i < entryCount; i++)
        {
            var e = entriesOffset + i * EntryStride;   // payload-relative
            var name = r.Ascii(e + 0, 256);            // inline szName[256]
            // arRanges variable-array @ entry+264: dataOffset @ +8, size @ +12.
            var rangesOffset = (int)r.U32(e + 264 + 8);
            var rangesSize = (int)r.U32(e + 264 + 12);
            var rangeCount = rangesSize > 0 ? rangesSize / RangeStride : 0;

            var ranges = new FormulaRange[rangeCount];
            for (var j = 0; j < rangeCount; j++)
            {
                var rg = rangesOffset + j * RangeStride;
                // tFormula DT_STRING_FORMULA @ range+16: FormulaOffset @ +8,
                // FormulaSize @ +12; text is ASCII at payload FormulaOffset.
                var fOff = r.I32(rg + 16 + 8);
                var fSize = r.I32(rg + 16 + 12);
                var text = fSize > 0 ? r.Ascii(fOff, fSize).Trim() : string.Empty;
                ranges[j] = new FormulaRange(
                    r.I32(rg + 0), r.F32(rg + 4), r.F32(rg + 8), text);
            }

            var gbid = Diablo4.GbidHash(name);
            entries[i] = new AttributeFormula(name, gbid, ranges);
            if (name.Length > 0)
            {
                byName[name] = ranges.Length > 0 ? ranges[0].FormulaText : string.Empty;
#if NETSTANDARD2_0
                if (!nameByGbid.ContainsKey(gbid)) nameByGbid[gbid] = name;
#else
                nameByGbid.TryAdd(gbid, name);
#endif
            }
        }

        return new AttributeFormulaTable(snoId, entries, byName, nameByGbid);
    }
}
