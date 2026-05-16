using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// Diablo IV's <c>CoreTOCSharedPayloadsMapping.dat</c> (magic
/// <c>0xABBA0003</c>): a payload de-duplication table. When a SNO's own
/// payload section is empty, its pixel/blob bytes physically live under a
/// different ("shared") SNO; this maps the requesting id → the id that
/// actually holds the payload. Texture atlases (e.g. the per-class paragon
/// node icons) rely on this.
/// </summary>
/// <remarks>
/// Layout (clean-room, per the upstream record §8.11 — the behaviour the
/// WoW-Tools D4 root implements): <c>i32 magic (0xABBA0003); i32 count;
/// count × { i32 snoId; i32 sharedSnoId }</c>.
/// </remarks>
public sealed class SharedPayloadMapping
{
    /// <summary>The file's magic number.</summary>
    public const uint Magic = 0xABBA0003;

    private readonly Dictionary<int, int> _idToSource;

    private SharedPayloadMapping(Dictionary<int, int> map) => _idToSource = map;

    /// <summary>Number of aliased SNOs.</summary>
    public int Count => _idToSource.Count;

    /// <summary>True if <paramref name="id"/>'s payload is stored under
    /// another SNO; <paramref name="sourceId"/> is that holder.</summary>
    public bool TryGetSource(int id, out int sourceId) =>
        _idToSource.TryGetValue(id, out sourceId);

    /// <summary>All <c>(requestingSnoId, sourceSnoId)</c> alias pairs.</summary>
    public IEnumerable<(int Sno, int Source)> Pairs()
    {
        foreach (var kv in _idToSource) yield return (kv.Key, kv.Value);
    }

    /// <summary>Parse the mapping file bytes.</summary>
    public static SharedPayloadMapping Parse(ReadOnlySpan<byte> data)
    {
        var map = new Dictionary<int, int>();
        if (data.Length < 8) return new SharedPayloadMapping(map);

        // i32 magic (0xABBA0003), i32 count, then 8-byte entries.
        var count = Bytes.I32LE(data, 4);
        var pos = 8;
        for (var i = 0; i < count && pos + 8 <= data.Length; i++, pos += 8)
        {
            var snoId = Bytes.I32LE(data, pos);
            var sharedSnoId = Bytes.I32LE(data, pos + 4);
            map[snoId] = sharedSnoId;
        }
        return new SharedPayloadMapping(map);
    }

    /// <summary>An empty mapping (used when the file is absent).</summary>
    public static SharedPayloadMapping Empty { get; } = new(new Dictionary<int, int>());
}
