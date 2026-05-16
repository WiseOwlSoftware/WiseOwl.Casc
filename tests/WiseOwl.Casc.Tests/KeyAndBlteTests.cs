using System;
using System.IO;
using System.IO.Compression;
using WiseOwl.Casc;
using WiseOwl.Casc.Encoding;
using WiseOwl.Casc.Internal;
using Xunit;

namespace WiseOwl.Casc.Tests;

/// <summary>CI-safe unit coverage for the value types and BLTE — no game
/// bytes, all synthetic.</summary>
public sealed class KeyAndBlteTests
{
    [Fact]
    public void ContentKey_round_trips_hex_and_compares_by_value()
    {
        const string hex = "2d30daa9b141302900b469e2d5d68fb9";
        var a = ContentKey.Parse(hex);
        var b = ContentKey.Parse(hex.ToUpperInvariant());

        Assert.Equal(a, b);
        Assert.Equal(hex, a.ToString());
        Assert.True(ContentKey.TryParse(hex, out _));
        Assert.False(ContentKey.TryParse("xyz", out _));
    }

    [Fact]
    public void EncodingKey_index_prefix_is_first_9_bytes()
    {
        var k = EncodingKey.Parse("0011223344556677889900aabbccddee");
        Assert.Equal(0x0011223344556677UL << 8 | 0x88, k.IndexPrefix);
    }

    [Fact]
    public void CascPathHash_empty_input_is_deadbeef_pair()
    {
        // a = b = c = 0xDEADBEEF for empty input → (c<<32)|b.
        Assert.Equal(0xDEADBEEFDEADBEEFUL, CascPathHash.Of(ReadOnlySpan<byte>.Empty));
        // Path hashing is deterministic and separator/case-insensitive.
        Assert.Equal(
            CascPathHash.OfPath(@"Base\CoreTOC.dat"),
            CascPathHash.OfPath("base/coretoc.DAT"));
    }

    [Fact]
    public void Blte_decodes_single_raw_chunk()
    {
        var payload = "hello casc"u8.ToArray();
        var blob = BuildSingleChunkBlte((byte)'N', payload);
        Assert.Equal(payload, Blte.Decode(blob));
    }

    [Fact]
    public void Blte_decodes_chunked_zlib()
    {
        var payload = new byte[5000];
        new Random(42).NextBytes(payload);

        using var ms = new MemoryStream();
        // 'BLTE' + headerSize(BE) + 0x0F + 1 chunk + 24-byte entry, then 'Z'.
        var zlib = ZlibCompress(payload);
        var chunkBody = new byte[1 + zlib.Length];
        chunkBody[0] = (byte)'Z';
        Array.Copy(zlib, 0, chunkBody, 1, zlib.Length);

        var headerSize = 12 + 24;
        WriteBE(ms, 0x424C5445);
        WriteBE(ms, (uint)headerSize);
        ms.WriteByte(0x0F);
        ms.WriteByte(0); ms.WriteByte(0); ms.WriteByte(1);   // 1 block
        WriteBE(ms, (uint)chunkBody.Length);                  // comp size
        WriteBE(ms, (uint)payload.Length);                    // decomp size
        ms.Write(new byte[16], 0, 16);                        // md5 (unused)
        ms.Write(chunkBody, 0, chunkBody.Length);

        Assert.Equal(payload, Blte.Decode(ms.ToArray()));
    }

    [Fact]
    public void Blte_encrypted_chunk_throws_typed_exception()
    {
        var blob = BuildSingleChunkBlte((byte)'E', [0x08, 1, 2, 3, 4, 5, 6, 7, 8]);
        Assert.Throws<CascEncryptedContentException>(() => Blte.Decode(blob));
    }

    private static byte[] BuildSingleChunkBlte(byte mode, byte[] body)
    {
        // Header-less single chunk: 'BLTE', headerSize=0, then mode + body.
        using var ms = new MemoryStream();
        WriteBE(ms, 0x424C5445);
        WriteBE(ms, 0);
        ms.WriteByte(mode);
        ms.Write(body, 0, body.Length);
        return ms.ToArray();
    }

    private static void WriteBE(Stream s, uint v)
    {
        s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16));
        s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v);
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var outMs = new MemoryStream();
        using (var z = new ZLibStream(outMs, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(data, 0, data.Length);
        return outMs.ToArray();
    }
}
