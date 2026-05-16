using System;
using System.IO;
using System.IO.Compression;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Encoding;

/// <summary>
/// Decoder for the <b>BLTE</b> container ("Block Table Encoded"), the
/// chunked codec wrapping every stored CASC blob. A blob is a
/// <c>'BLTE'</c> magic, an optional chunk table, then one or more chunks;
/// each chunk's first byte is its mode: <c>'N'</c> raw, <c>'Z'</c> zlib,
/// <c>'F'</c> recursive BLTE, <c>'E'</c> encrypted (Salsa20/ARC4).
/// </summary>
/// <remarks>
/// Clean-room from the public TACT documentation, cross-checked against the
/// upstream record. Decodes eagerly into a single buffer — correct and
/// simple; CASC blobs are individually small-to-medium. Encrypted chunks
/// require a TACT key this library does not ship, and raise
/// <see cref="CascEncryptedContentException"/>.
/// </remarks>
public static class Blte
{
    private const uint Magic = 0x424C5445; // 'BLTE' big-endian

    /// <summary>Decode a BLTE blob (already stripped of any CASC archive
    /// envelope) into its logical bytes.</summary>
    /// <param name="blob">The full BLTE blob.</param>
    /// <returns>The decoded content.</returns>
    /// <exception cref="CascFormatException">Not a BLTE blob / malformed.</exception>
    /// <exception cref="CascEncryptedContentException">An encrypted chunk
    /// was encountered (no TACT key available).</exception>
    public static byte[] Decode(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < 8 || Bytes.U32BE(blob, 0) != Magic)
            throw new CascFormatException("Not a BLTE blob (bad magic).");

        var headerSize = (int)Bytes.U32BE(blob, 4);

        // chunk = (compressed offset, compressed size, decompressed size)
        (int Off, int CSize, int DSize)[] chunks;
        int pos;

        if (headerSize > 0)
        {
            if (blob[8] != 0x0F)
                throw new CascFormatException(
                    $"Bad BLTE chunk-table flag 0x{blob[8]:X2} (expected 0x0F).");
            var count = (blob[9] << 16) | (blob[10] << 8) | blob[11];
            if (count <= 0)
                throw new CascFormatException("BLTE chunk count is zero.");

            chunks = new (int, int, int)[count];
            var t = 12;                       // start of the 24-byte entries
            var data = headerSize;            // chunk data begins after header
            for (var i = 0; i < count; i++)
            {
                var cSize = (int)Bytes.U32BE(blob, t);
                var dSize = (int)Bytes.U32BE(blob, t + 4);
                t += 24;                      // 4 + 4 + 16-byte md5 (unused)
                chunks[i] = (data, cSize, dSize);
                data += cSize;
            }
            pos = 0; // unused below; chunk offsets are absolute
        }
        else
        {
            // Single, header-less chunk: everything after the 8-byte magic.
            chunks = [(8, blob.Length - 8, -1)];
            pos = 0;
        }
        _ = pos;

        using var outMs = new MemoryStream();
        for (var i = 0; i < chunks.Length; i++)
        {
            var (off, cSize, dSize) = chunks[i];
            DecodeChunk(blob.Slice(off, cSize), dSize, outMs, i);
        }
        return outMs.ToArray();
    }

    /// <summary>Decode a blob and expose it as a forward-only stream.</summary>
    public static Stream DecodeToStream(ReadOnlySpan<byte> blob) =>
        new MemoryStream(Decode(blob), writable: false);

    private static void DecodeChunk(
        ReadOnlySpan<byte> chunk, int decompSize, Stream output, int chunkIndex)
    {
        var mode = (char)chunk[0];
        var body = chunk.Slice(1);
        switch (mode)
        {
            case 'N': // stored verbatim
#if NETSTANDARD2_0
                output.Write(body.ToArray(), 0, body.Length);
#else
                output.Write(body);
#endif
                break;

            case 'Z': // zlib (RFC 1950) stream
                Inflate(body, output, decompSize);
                break;

            case 'F': // a nested BLTE blob
#if NETSTANDARD2_0
                var inner = Decode(body.ToArray());
#else
                var inner = Decode(body);
#endif
                output.Write(inner, 0, inner.Length);
                break;

            case 'E':
                throw new CascEncryptedContentException(
                    $"BLTE chunk {chunkIndex} is encrypted; a TACT key is " +
                    "required and WiseOwl.Casc has no key store yet.");

            default:
                throw new CascFormatException(
                    $"Unknown BLTE chunk mode '{mode}' (0x{(byte)mode:X2}).");
        }
    }

    private static void Inflate(ReadOnlySpan<byte> zlib, Stream output, int decompSize)
    {
        _ = decompSize;
#if NETSTANDARD2_0
        // No ZLibStream on ns2.0: skip the 2-byte zlib header, raw-inflate.
        using var src = new MemoryStream(zlib.Slice(2).ToArray(), writable: false);
        using var inflate = new DeflateStream(src, CompressionMode.Decompress);
        inflate.CopyTo(output);
#else
        var rented = zlib.ToArray();
        using var src = new MemoryStream(rented, writable: false);
        using var zs = new ZLibStream(src, CompressionMode.Decompress);
        zs.CopyTo(output);
#endif
    }
}
