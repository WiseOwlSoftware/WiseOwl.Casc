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
/// <see cref="TextureDefinition"/>'s <c>ptFrame</c> array — carries both
/// the outer UV rect (the full sprite slot, including any authored
/// padding) and an inner UV rect (the trimmed-content / 9-slice-middle
/// rect) decoded from the trailing 16 bytes of the on-disk frame
/// (CL-71, FR-C20 #32 codec-tail investigation).</summary>
/// <remarks>
/// <para>
/// Across the live build's <c>140 197</c> texture definitions, ~<c>83%</c>
/// of frames carry an inner rect equal to the outer rect (no trim;
/// outer is the content). Of the rest:
/// </para>
/// <list type="bullet">
///   <item>~<c>17%</c> carry a non-equal inner rect inset from the
///     outer — typically a few pixels of padding for filtering /
///     mipmap safety, or a 9-slice middle for stretchable UI tiles.</item>
///   <item>A handful carry a <i>degenerate</i> inner rect collapsed to a
///     single point at the outer rect's top-left
///     (<c>InnerU0 == InnerU1</c>, <c>InnerV0 == InnerV1</c>); read as
///     "no authored inner rect" rather than as a zero-area sample.</item>
/// </list>
/// <para>
/// Both rects are normalised over the decoded mip0 dimensions. For
/// rendering a frame's content untrimmed, sample
/// <see cref="PixelRect"/>; for the inset / 9-slice middle, sample
/// <see cref="InnerPixelRect"/>.
/// </para>
/// </remarks>
/// <param name="ImageHandle">The image handle. This matches a
/// <c>ParagonNode.hIconMask</c> / <c>hIcon</c> — the node↔icon link is
/// first-party, no heuristic correlation needed.</param>
/// <param name="U0">Outer-rect left edge (normalized over mip0 width).</param>
/// <param name="V0">Outer-rect top edge (normalized over mip0 height).</param>
/// <param name="U1">Outer-rect right edge (normalized).</param>
/// <param name="V1">Outer-rect bottom edge (normalized).</param>
/// <param name="InnerU0">Inner-rect left edge (normalized) — the
/// trimmed-content / 9-slice-middle left.</param>
/// <param name="InnerV0">Inner-rect top edge (normalized).</param>
/// <param name="InnerU1">Inner-rect right edge (normalized).</param>
/// <param name="InnerV1">Inner-rect bottom edge (normalized).</param>
public readonly record struct TexFrame(
    uint ImageHandle,
    float U0, float V0, float U1, float V1,
    float InnerU0, float InnerV0, float InnerU1, float InnerV1)
{
    /// <summary>Integer pixel rectangle for this frame's outer rect
    /// over a <paramref name="width"/>×<paramref name="height"/>
    /// decoded atlas: <c>x0=floor(U0·W) … x1=ceil(U1·W)</c>.</summary>
    public (int X, int Y, int Width, int Height) PixelRect(int width, int height) =>
        ToPixelRect(U0, V0, U1, V1, width, height);

    /// <summary>Integer pixel rectangle for this frame's inner rect
    /// over the same decoded atlas. When the inner rect equals the
    /// outer rect (the common case), this returns the same pixel
    /// rectangle as <see cref="PixelRect"/>. When the inner rect is
    /// degenerate (<c>InnerU0 == InnerU1</c>,
    /// <c>InnerV0 == InnerV1</c>), the returned width/height are
    /// floored at <c>1</c> to keep the rect non-empty (consumers can
    /// detect the no-inner-authored case via
    /// <see cref="HasDistinctInner"/>).</summary>
    public (int X, int Y, int Width, int Height) InnerPixelRect(int width, int height) =>
        ToPixelRect(InnerU0, InnerV0, InnerU1, InnerV1, width, height);

    /// <summary>True when the inner rect is structurally different from
    /// the outer rect — i.e. the engine authored a real trim /
    /// 9-slice-middle. False on the ~<c>83%</c> of frames where the
    /// two are identical, and on the degenerate-point cases where the
    /// engine did not author an inner region.</summary>
    public bool HasDistinctInner =>
        (U0 != InnerU0 || V0 != InnerV0 || U1 != InnerU1 || V1 != InnerV1)
        && !(InnerU0 == InnerU1 && InnerV0 == InnerV1);

    private static (int X, int Y, int Width, int Height) ToPixelRect(
        float u0, float v0, float u1, float v1, int width, int height)
    {
        var x0 = Math.Max(0, (int)Math.Floor(u0 * width));
        var y0 = Math.Max(0, (int)Math.Floor(v0 * height));
        var x1 = Math.Min(width, (int)Math.Ceiling(u1 * width));
        var y1 = Math.Min(height, (int)Math.Ceiling(v1 * height));
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
                BitsToSingle(Bytes.U32LE(bundle, b + 4)),    // outer u0
                BitsToSingle(Bytes.U32LE(bundle, b + 8)),    // outer v0
                BitsToSingle(Bytes.U32LE(bundle, b + 12)),   // outer u1
                BitsToSingle(Bytes.U32LE(bundle, b + 16)),   // outer v1
                BitsToSingle(Bytes.U32LE(bundle, b + 20)),   // inner u0 (CL-71)
                BitsToSingle(Bytes.U32LE(bundle, b + 24)),   // inner v0
                BitsToSingle(Bytes.U32LE(bundle, b + 28)),   // inner u1
                BitsToSingle(Bytes.U32LE(bundle, b + 32))    // inner v1
            );
        }

        return new TextureDefinition(format, width, height, mipMin, mipMax, ser, frames);
    }

    private static float BitsToSingle(uint bits) =>
        BitConverter.UInt32BitsToSingle(bits);
}
