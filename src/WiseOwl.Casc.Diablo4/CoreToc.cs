using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>One resolved <c>CoreTOC.dat</c> entry: a SNO record's group,
/// numeric id, and name.</summary>
/// <param name="Group">SNO group.</param>
/// <param name="Id">SNO numeric id (unique within a group).</param>
/// <param name="Name">SNO name, e.g. <c>Paragon_Warlock_00</c>.</param>
public readonly record struct SnoEntry(SnoGroup Group, int Id, string Name);

/// <summary>
/// Parser for Diablo IV's master SNO directory, <c>CoreTOC.dat</c>. Maps
/// every <c>(group, id)</c> to a name and exposes the per-group payload
/// format hash needed to dispatch SNO record parsing.
/// </summary>
/// <remarks>
/// <para>
/// Implements the layout in the upstream record
/// (<c>d4-binary-formats.md §1</c>), re-derived here as a clean-room
/// parser. All integers are little-endian; string pointers are resolved to
/// absolute offsets. The whole file (~40&#160;MB) is parsed from a single
/// buffer using spans.
/// </para>
/// <para><b>New-format header.</b> The first <see cref="int"/> is read as
/// the group count; if it equals the sentinel <c>0xBCDE6611</c> the file is
/// the current "new format" and the real count follows. The stock
/// <c>WoW-Tools CascLib</c> NuGet (1.0.23) overflows on this header — this
/// parser handles it directly.</para>
/// </remarks>
public sealed class CoreToc
{
    /// <summary>New-format sentinel occupying the first <see cref="int"/>.</summary>
    public const uint NewFormatSentinel = 0xBCDE6611;

    private readonly Dictionary<long, string> _namesByKey;

    private CoreToc(
        int groupCount,
        IReadOnlyList<uint> groupFormatHashes,
        IReadOnlyList<SnoEntry> entries)
    {
        GroupCount = groupCount;
        GroupFormatHashes = groupFormatHashes;
        Entries = entries;
        _namesByKey = entries.ToDictionary(Key, e => e.Name);
    }

    /// <summary>Number of SNO groups (the <c>N</c> in the spec).</summary>
    public int GroupCount { get; }

    /// <summary>Per-group payload format hash (index = group id). Used to
    /// dispatch typed SNO record parsing.</summary>
    public IReadOnlyList<uint> GroupFormatHashes { get; }

    /// <summary>All entries, in file order.</summary>
    public IReadOnlyList<SnoEntry> Entries { get; }

    private static long Key(SnoEntry e) => ((long)e.Group << 32) | (uint)e.Id;

    /// <summary>Parse a <c>CoreTOC.dat</c> from disk.</summary>
    public static CoreToc Load(string path) => Parse(File.ReadAllBytes(path));

    /// <summary>Parse <c>CoreTOC.dat</c> from an in-memory buffer.</summary>
    public static CoreToc Parse(ReadOnlySpan<byte> data)
    {
        var pos = 0;
        var first = Bytes.U32LE(data, pos); pos += 4;

        // If the first int32 is the sentinel, the real count follows;
        // otherwise the first int32 *was* the count (legacy format).
        var newFormat = first == NewFormatSentinel;
        int n;
        if (newFormat) { n = (int)Bytes.U32LE(data, pos); pos += 4; }
        else { n = (int)first; }

        // Four parallel int32[N] arrays: counts, offsets, unkCounts,
        // formatHashes — then a trailing int32 Unk1 (unused).
        var counts = ReadU32Array(data, ref pos, n);
        var offsets = ReadU32Array(data, ref pos, n);
        _ = ReadU32Array(data, ref pos, n);                 // unkCounts (unused)
        var formatHashes = ReadU32Array(data, ref pos, n);

        // EntryOffsets are relative to the END of the header, NOT absolute
        // (verified empirically upstream). New-format header is
        // 12 + 16*N bytes (sentinel + N + four N-arrays + Unk1).
        var headerSize = newFormat ? 12 + 16 * n : 8 + 16 * n;

        var entries = new List<SnoEntry>();
        for (var group = 0; group < n; group++)
        {
            var count = (int)counts[group];
            if (count == 0) continue;

            var groupStart = (int)offsets[group] + headerSize;
            var groupEnd = groupStart + count * 12;

            var p = groupStart;
            for (var i = 0; i < count; i++)
            {
                var snoGroup = Bytes.I32LE(data, p);
                var snoId = Bytes.I32LE(data, p + 4);
                var pName = Bytes.I32LE(data, p + 8);
                p += 12;
                // Name string: NUL-terminated ASCII at groupEnd + pName.
                var name = Bytes.AsciiZ(data, groupEnd + pName);
                entries.Add(new SnoEntry((SnoGroup)snoGroup, snoId, name));
            }
        }

        return new CoreToc(n, formatHashes, entries);
    }

    /// <summary>All entries belonging to a given SNO group.</summary>
    public IEnumerable<SnoEntry> EntriesInGroup(SnoGroup group) =>
        Entries.Where(e => e.Group == group);

    /// <summary>Resolve a SNO name, or <see langword="null"/> if unknown.</summary>
    public string? GetName(SnoGroup group, int id) =>
        _namesByKey.TryGetValue(((long)group << 32) | (uint)id, out var v) ? v : null;

    /// <summary>Try to resolve a SNO name.</summary>
    public bool TryGetName(SnoGroup group, int id, out string name)
    {
        var ok = _namesByKey.TryGetValue(((long)group << 32) | (uint)id, out var v);
        name = v ?? string.Empty;
        return ok;
    }

    /// <summary>The payload format hash for a SNO group (<c>0</c> if out of
    /// range). Used to select the typed record reader.</summary>
    public uint FormatHashFor(SnoGroup group)
    {
        var i = (int)group;
        return i >= 0 && i < GroupFormatHashes.Count ? GroupFormatHashes[i] : 0u;
    }

    private static uint[] ReadU32Array(ReadOnlySpan<byte> data, ref int pos, int count)
    {
        var arr = new uint[count];
        for (var i = 0; i < count; i++) { arr[i] = Bytes.U32LE(data, pos); pos += 4; }
        return arr;
    }
}
