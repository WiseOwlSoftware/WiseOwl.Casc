using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>One mip's serialized-data locator (<c>SerialDataInfo</c>, 8
/// bytes) from a <see cref="TextureDefinition"/>'s <c>serTex</c> array.</summary>
/// <param name="Offset">Byte offset of this mip within the texture payload
/// (paragon atlases keep mip0 at offset 0).</param>
/// <param name="SizeAndFlags">Byte length of this mip's block data.</param>
public readonly record struct SerialDataInfo(uint Offset, uint SizeAndFlags);

/// <summary>One atlas sub-rect (<c>TexFrame</c>, 36 bytes) from a
/// <see cref="TextureDefinition"/>'s <c>ptFrame</c> array.</summary>
/// <param name="ImageHandle">The image handle. This matches a
/// <c>ParagonNode.hIconMask</c> / <c>hIcon</c> — the node↔icon link is
/// first-party, no heuristic correlation needed.</param>
/// <param name="U0">Left edge, normalized over the decoded mip0 width.</param>
/// <param name="V0">Top edge, normalized over the decoded mip0 height.</param>
/// <param name="U1">Right edge (normalized).</param>
/// <param name="V1">Bottom edge (normalized).</param>
public readonly record struct TexFrame(uint ImageHandle, float U0, float V0, float U1, float V1)
{
    /// <summary>Integer pixel rectangle for this frame over a
    /// <paramref name="width"/>×<paramref name="height"/> decoded atlas:
    /// <c>x0=floor(U0·W) … x1=ceil(U1·W)</c>.</summary>
    public (int X, int Y, int Width, int Height) PixelRect(int width, int height)
    {
        var x0 = Math.Max(0, (int)Math.Floor(U0 * width));
        var y0 = Math.Max(0, (int)Math.Floor(V0 * height));
        var x1 = Math.Min(width, (int)Math.Ceiling(U1 * width));
        var y1 = Math.Min(height, (int)Math.Ceiling(V1 * height));
        return (x0, y0, Math.Max(1, x1 - x0), Math.Max(1, y1 - y0));
    }
}

/// <summary>The pixel codec a <see cref="TextureDefinition.Format"/> selects.</summary>
public enum TextureCodec
{
    /// <summary>Unrecognized / unsupported <c>eTexFormat</c>.</summary>
    Unknown = 0,
    /// <summary>Uncompressed RGBA8.</summary>
    Rgba8,
    /// <summary>Single-channel R8.</summary>
    R8,
    /// <summary>BC1 / DXT1.</summary>
    Bc1,
    /// <summary>BC2 / DXT3.</summary>
    Bc2,
    /// <summary>BC3 / DXT5 — paragon node/glyph atlases use this.</summary>
    Bc3,
    /// <summary>BC4 / RGTC1.</summary>
    Bc4,
    /// <summary>BC5 / RGTC2.</summary>
    Bc5,
    /// <summary>BC6H (HDR).</summary>
    Bc6H,
    /// <summary>BC7 / BPTC UNORM.</summary>
    Bc7,
    /// <summary>RGBA16F.</summary>
    Rgba16F,
}

/// <summary>
/// A parsed Diablo IV <c>TextureDefinition</c>: pixel format, base
/// dimensions, the per-mip <c>serTex</c> table, and the atlas
/// <c>ptFrame</c> sub-rects. The pixel payload itself is fetched separately
/// (it is addressable by SNO id); this metadata only lives in the combined
/// bundle (see <see cref="CombinedTextureMeta"/>).
/// </summary>
/// <param name="Format">Raw <c>eTexFormat</c> selector value.</param>
/// <param name="Width">Base mip width in pixels.</param>
/// <param name="Height">Base mip height in pixels.</param>
/// <param name="MipMin">First valid mip level.</param>
/// <param name="MipMax">Last valid mip level.</param>
/// <param name="SerTex">Per-mip data locators (index 0 = largest mip).</param>
/// <param name="Frames">Atlas sub-rects, keyed by image handle.</param>
public sealed record TextureDefinition(
    int Format, int Width, int Height, int MipMin, int MipMax,
    IReadOnlyList<SerialDataInfo> SerTex, IReadOnlyList<TexFrame> Frames)
{
    /// <summary>The decoded codec for <see cref="Format"/>.</summary>
    public TextureCodec Codec => Format switch
    {
        0 or 45 => TextureCodec.Rgba8,
        7 or 23 => TextureCodec.R8,
        9 or 10 or 46 or 47 => TextureCodec.Bc1,
        48 => TextureCodec.Bc2,
        12 or 49 => TextureCodec.Bc3,
        41 => TextureCodec.Bc4,
        42 => TextureCodec.Bc5,
        43 or 51 => TextureCodec.Bc6H,
        44 or 50 => TextureCodec.Bc7,
        25 => TextureCodec.Rgba16F,
        _ => TextureCodec.Unknown,
    };

    /// <summary>
    /// Parse a <c>TextureDefinition</c> from a <c>0x44CF00F5</c>
    /// combined-meta descriptor. Unlike a standalone SNO payload, the
    /// <c>DT_VARIABLEARRAY</c> descriptor here is
    /// <c>{ i32 pad, i32 dataOffset@+4, i32 dataSize@+8, i32 pad }</c> and
    /// <c>dataOffset</c> is <b>blob-relative</b> (measured from
    /// <paramref name="descStart"/>), not payload-relative. Scalars are
    /// payload-base-relative with base <c>descStart + 4</c>.
    /// </summary>
    /// <param name="bundle">The whole combined-meta buffer.</param>
    /// <param name="descStart">Offset of this entry's descriptor (the per-
    /// entry <c>snoId</c> sits at <c>descStart+0</c>).</param>
    public static TextureDefinition ParseFromBundle(ReadOnlySpan<byte> bundle, int descStart)
    {
        var payloadBase = descStart + 4;
        var r = new SnoRecord(bundle, payloadBase);

        var format = (int)r.U32(8);
        var width = r.U16(16);
        var height = r.U16(18);
        var mipMin = r.U8(25);
        var mipMax = r.U8(26);

        // Static local function: a ref-struct span cannot be captured by a
        // closure, so the buffer is passed through explicitly.
        static (int Start, int Count) Arr(
            ReadOnlySpan<byte> buf, int payloadBase, int descStart,
            int fieldOff, int elemSize)
        {
            var f = payloadBase + fieldOff;
            if (f + 12 > buf.Length) return (0, 0);
            var dataOffset = Bytes.I32LE(buf, f + 4);
            var dataSize = Bytes.I32LE(buf, f + 8);
            var start = descStart + dataOffset;
            if (start < 0 || dataSize < 0 || start + dataSize > buf.Length)
                return (0, 0);
            return (start, dataSize / elemSize);
        }

        var (serStart, serCount) = Arr(bundle, payloadBase, descStart, 64, 8);
        var ser = new SerialDataInfo[Math.Max(0, serCount)];
        for (var i = 0; i < ser.Length; i++)
        {
            var b = serStart + i * 8;
            ser[i] = new SerialDataInfo(Bytes.U32LE(bundle, b), Bytes.U32LE(bundle, b + 4));
        }

        var (frStart, frCount) = Arr(bundle, payloadBase, descStart, 80, 36);
        var frames = new TexFrame[Math.Max(0, frCount)];
        for (var i = 0; i < frames.Length; i++)
        {
            var b = frStart + i * 36;
            frames[i] = new TexFrame(
                Bytes.U32LE(bundle, b),
                BitsToSingle(Bytes.U32LE(bundle, b + 4)),
                BitsToSingle(Bytes.U32LE(bundle, b + 8)),
                BitsToSingle(Bytes.U32LE(bundle, b + 12)),
                BitsToSingle(Bytes.U32LE(bundle, b + 16)));
        }

        return new TextureDefinition(format, width, height, mipMin, mipMax, ser, frames);
    }

    private static float BitsToSingle(uint bits) =>
        BitConverter.UInt32BitsToSingle(bits);
}
