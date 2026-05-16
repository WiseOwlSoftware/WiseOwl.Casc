using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Encoding;

/// <summary>
/// The CASC <b>encoding</b> table: maps a <see cref="ContentKey"/> (the hash
/// of a file's logical bytes) to the <see cref="EncodingKey"/>(s) of its
/// stored, BLTE-encoded form(s). The table is itself stored as a BLTE blob;
/// its own key comes from the build configuration.
/// </summary>
/// <remarks>
/// Clean-room implementation of the TACT encoding layout: an
/// <c>EN</c>-magic header, an ESpec string block, a CKey page index, then
/// the CKey pages — 4&#160;KiB chunks of
/// <c>{ byte keyCount; uint40BE fileSize; CKey; keyCount × EKey }</c>
/// records, zero-padded to the chunk boundary. We index CKey → first EKey
/// (sufficient to open content); the EKey/ESpec section is skipped.
/// </remarks>
public sealed class EncodingTable
{
    private const int ChunkSize = 4096;

    private readonly Dictionary<Md5Key, EncodingKey> _cKeyToEKey;

    private EncodingTable(Dictionary<Md5Key, EncodingKey> map) => _cKeyToEKey = map;

    /// <summary>Number of content-key entries.</summary>
    public int Count => _cKeyToEKey.Count;

    /// <summary>Resolve a content key to its (first) encoding key.</summary>
    public bool TryGetEncodingKey(in ContentKey cKey, out EncodingKey eKey) =>
        _cKeyToEKey.TryGetValue(cKey.Value, out eKey);

    /// <summary>Parse a fully BLTE-decoded encoding file.</summary>
    public static EncodingTable Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 22 || data[0] != (byte)'E' || data[1] != (byte)'N')
            throw new CascFormatException("Not an encoding file (bad 'EN' magic).");

        // Header, big-endian:
        //  [0..1]'EN' [2]Version [3]CKeyLen [4]EKeyLen
        //  [5..6]CKeyPageSize(KiB) [7..8]EKeyPageSize(KiB)
        //  [9..12]CKeyPageCount [13..16]EKeyPageCount [17]unk1
        //  [18..21]ESpecBlockSize  then the ESpec string block.
        var ckeyLen = data[3];
        var ekeyLen = data[4];
        var cKeyPageCount = (int)Bytes.U32BE(data, 9);
        var especBlockSize = (int)Bytes.U32BE(data, 18);

        var pos = 22 + especBlockSize;        // skip header + ESpec strings
        pos += cKeyPageCount * 32;            // skip CKey page index

        var map = new Dictionary<Md5Key, EncodingKey>();
        var chunkStart = pos;

        // Mirrors the storage's own page walk exactly: a page is a run of
        // entries terminated by a zero key-count byte, then zero padding to
        // the next 4 KiB boundary. The (page++) advance plus the manual
        // increment on the 0xFFF edge case match the reference behaviour.
        for (var page = 0; page < cKeyPageCount; page++)
        {
            while (pos < data.Length && data[pos] != 0)
            {
                var keyCount = data[pos];
                pos += 1;
                pos += 5;                     // uint40BE fileSize (unused)
                var cKey = ContentKey.FromBytes(data.Slice(pos, ckeyLen));
                pos += ckeyLen;

                EncodingKey? first = null;
                for (var k = 0; k < keyCount; k++)
                {
                    var eKey = EncodingKey.FromBytes(data.Slice(pos, ekeyLen));
                    pos += ekeyLen;
                    first ??= eKey;
                }
                if (first is { } fe && !map.ContainsKey(cKey.Value))
                    map[cKey.Value] = fe;
            }
            pos += 1;                         // consume the zero terminator

            var remaining = ChunkSize - ((pos - chunkStart) % ChunkSize);
            if (remaining == 0xFFF) { pos -= 1; page++; continue; }
            if (remaining > 0) pos += remaining;
        }

        return new EncodingTable(map);
    }
}
