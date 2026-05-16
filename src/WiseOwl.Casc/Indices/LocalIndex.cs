using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Indices;

/// <summary>Where a stored blob lives in the local archive set: which
/// <c>data.NNN</c> file, at what byte offset, and how long.</summary>
/// <param name="ArchiveIndex">The <c>data.NNN</c> archive number.</param>
/// <param name="Offset">Byte offset of the blob's archive envelope.</param>
/// <param name="Size">Envelope length in bytes (30-byte header + BLTE).</param>
public readonly record struct ArchiveLocation(int ArchiveIndex, long Offset, int Size);

/// <summary>
/// The CASC <b>local index</b>: the 16 <c>Data/data/&lt;bucket&gt;*.idx</c>
/// files that map a 9-byte <see cref="EncodingKey"/> prefix to an
/// <see cref="ArchiveLocation"/>. Together they index every blob physically
/// present in the local <c>data.NNN</c> archives.
/// </summary>
/// <remarks>
/// Clean-room implementation of the v2/v7 local-index layout: a hashed
/// header (<c>HeaderHashSize</c>, <c>HeaderHash</c>, then the sized header
/// block), padded to a 16-byte boundary, then <c>EntriesSize</c> /
/// <c>EntriesHash</c> and a packed array of 18-byte entries
/// (9-byte key, 1-byte index-high, 4-byte big-endian index-low, 4-byte
/// little-endian size). The archive number and offset are bit-packed:
/// <c>archive = (high &lt;&lt; 2) | (low &gt;&gt; 30)</c>,
/// <c>offset = low &amp; 0x3FFFFFFF</c>. The first key wins on duplicates.
/// </remarks>
public sealed class LocalIndex
{
    private const int EntrySize = 18;

    private readonly Dictionary<ulong, ArchiveLocation> _byKey9;

    private LocalIndex(Dictionary<ulong, ArchiveLocation> byKey9) => _byKey9 = byKey9;

    /// <summary>Number of indexed blobs.</summary>
    public int Count => _byKey9.Count;

    /// <summary>Resolve a blob's archive location by encoding key.</summary>
    public bool TryGetLocation(in EncodingKey ekey, out ArchiveLocation location) =>
        _byKey9.TryGetValue(ekey.IndexPrefix, out location);

    /// <summary>Load every current bucket index under
    /// <c>&lt;installPath&gt;/Data/data</c>.</summary>
    public static LocalIndex LoadLocal(string installPath)
    {
        var dataDir = Path.Combine(installPath, "Data", "data");
        if (!Directory.Exists(dataDir))
            throw new CascContentNotFoundException(
                $"No Data/data directory under '{installPath}'.");

        var map = new Dictionary<ulong, ArchiveLocation>();
        // 16 buckets 00..0f; use the highest-versioned .idx in each bucket.
        for (var bucket = 0; bucket < 0x10; bucket++)
        {
            var newest = Directory
                .EnumerateFiles(dataDir, $"{bucket:x2}*.idx")
                .OrderBy(p => p, StringComparer.Ordinal)
                .LastOrDefault();
            if (newest is null) continue;
            // The running game / Battle.net agent keeps these files open for
            // write, so a plain read share fails — share read+write.
            ParseInto(ReadAllSharedBytes(newest), map);
        }
        if (map.Count == 0)
            throw new CascFormatException("Local index parsed zero entries.");
        return new LocalIndex(map);
    }

    /// <summary>Read a whole file, tolerating other processes (the live game
    /// or Battle.net agent) holding it open for write.</summary>
    internal static byte[] ReadAllSharedBytes(string path)
    {
        using var fs = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[fs.Length];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = fs.Read(buffer, read, buffer.Length - read);
            if (n <= 0) break;
            read += n;
        }
        return buffer;
    }

    /// <summary>Parse one .idx buffer (exposed for unit testing).</summary>
    public static LocalIndex ParseSingle(ReadOnlySpan<byte> idx)
    {
        var map = new Dictionary<ulong, ArchiveLocation>();
        ParseInto(idx, map);
        return new LocalIndex(map);
    }

    private static void ParseInto(
        ReadOnlySpan<byte> b, Dictionary<ulong, ArchiveLocation> map)
    {
        var headerHashSize = Bytes.I32LE(b, 0);
        // 8 = sizeof(HeaderHashSize) + sizeof(HeaderHash); pad to 16 bytes.
        var entriesPos = (8 + headerHashSize + 0x0F) & ~0x0F;
        var entriesSize = Bytes.I32LE(b, entriesPos);
        var p = entriesPos + 8;               // skip EntriesSize + EntriesHash
        var numBlocks = entriesSize / EntrySize;

        for (var i = 0; i < numBlocks; i++, p += EntrySize)
        {
            // 9-byte EKey prefix, packed big-endian into a ulong bucket key.
            ulong key9 = 0;
            for (var k = 0; k < 9; k++) key9 = (key9 << 8) | b[p + k];

            int indexHigh = b[p + 9];
            var indexLow = (uint)Bytes.I32BE(b, p + 10);
            var archive = (indexHigh << 2) | (int)((indexLow & 0xC0000000) >> 30);
            var offset = indexLow & 0x3FFFFFFF;
            var size = Bytes.I32LE(b, p + 14);

            // First key wins (mirrors the storage's own resolution).
            if (!map.ContainsKey(key9))
                map[key9] = new ArchiveLocation(archive, offset, size);
        }
    }
}
