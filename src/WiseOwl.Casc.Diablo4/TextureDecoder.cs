using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded image: tightly-packed, row-major, straight-alpha RGBA32
/// (<c>R,G,B,A</c> bytes). Deliberately <b>not</b> tied to any imaging
/// library — the consumer owns cropping/encoding/compositing (slice with
/// <see cref="TexFrame.PixelRect"/>).
/// </summary>
/// <param name="Width">Pixel width.</param>
/// <param name="Height">Pixel height.</param>
/// <param name="Rgba">Pixel bytes, length <c>Width*Height*4</c>.</param>
public readonly record struct DecodedImage(int Width, int Height, byte[] Rgba)
{
    /// <summary>The RGBA bytes of one sub-rectangle, tightly packed
    /// (row-major). Pair with <see cref="TexFrame.PixelRect"/> to cut an
    /// atlas frame out without an imaging dependency.</summary>
    public byte[] Crop(int x, int y, int w, int h)
    {
        var outp = new byte[w * h * 4];
        for (var row = 0; row < h; row++)
        {
            var src = ((y + row) * Width + x) * 4;
            Array.Copy(Rgba, src, outp, row * w * 4, w * 4);
        }
        return outp;
    }
}

/// <summary>
/// Clean-room block-compression (S3TC/DXT) decoder for the codecs Diablo IV
/// UI atlases use. Implements <b>BC1</b> (DXT1) and <b>BC3</b> (DXT5) — the
/// paragon node/glyph atlases are BC3. BC2/BC7/etc. are out of scope here
/// (the catalog reports the codec via <see cref="TextureDefinition.Codec"/>;
/// callers can pick another decoder if needed). No external dependency.
/// </summary>
public static class TextureDecoder
{
    private static int Align(int v, int a) => (v + a - 1) / a * a;

    /// <summary>
    /// Decode mip 0 of a texture payload to a <see cref="DecodedImage"/>.
    /// Diablo IV stores BC rows aligned up to 64&#160;px; this decodes at the
    /// aligned width and crops back to the true <see cref="TextureDefinition.Width"/>×
    /// <see cref="TextureDefinition.Height"/>. mip0 is taken from
    /// <c>SerTex[0]</c> when present (else payload offset 0 — the paragon
    /// atlas case).
    /// </summary>
    /// <exception cref="NotSupportedException">The codec is not BC1/BC3.</exception>
    public static DecodedImage DecodeMip0(this TextureDefinition td, ReadOnlySpan<byte> payload)
    {
        var codec = td.Codec;
        if (codec is not (TextureCodec.Bc1 or TextureCodec.Bc3))
            throw new NotSupportedException(
                $"DecodeMip0 supports BC1/BC3; this texture is {codec} " +
                $"(eTexFormat {td.Format}). Use the raw payload + an external " +
                "decoder for this codec.");

        var off = td.SerTex.Count > 0 ? (int)td.SerTex[0].Offset : 0;
        var src = payload.Slice(off);

        var aw = Align(td.Width, 64);              // stored row pitch
        var ah = Align(td.Height, 4);              // whole 4-px block rows
        var blocksX = aw / 4;
        var blocksY = ah / 4;
        var full = new byte[aw * ah * 4];
        var blockSize = codec == TextureCodec.Bc1 ? 8 : 16;

        // One scratch buffer reused for every 4×4 block. (stackalloc inside
        // the loop would not be freed until the method returns → overflow on
        // a large atlas.)
        Span<byte> rgba = stackalloc byte[16 * 4];

        for (var by = 0; by < blocksY; by++)
        for (var bx = 0; bx < blocksX; bx++)
        {
            var bo = (by * blocksX + bx) * blockSize;
            if (bo + blockSize > src.Length) break;
            if (codec == TextureCodec.Bc1)
                DecodeBc1Block(src.Slice(bo, 8), rgba);
            else
                DecodeBc3Block(src.Slice(bo, 16), rgba);

            for (var py = 0; py < 4; py++)
            for (var px = 0; px < 4; px++)
            {
                var dx = bx * 4 + px;
                var dy = by * 4 + py;
                var di = (dy * aw + dx) * 4;
                var si = (py * 4 + px) * 4;
                full[di] = rgba[si];
                full[di + 1] = rgba[si + 1];
                full[di + 2] = rgba[si + 2];
                full[di + 3] = rgba[si + 3];
            }
        }

        // Crop the alignment padding away → the true W×H.
        if (aw == td.Width && ah == td.Height)
            return new DecodedImage(td.Width, td.Height, full);

        var cropped = new byte[td.Width * td.Height * 4];
        for (var y = 0; y < td.Height; y++)
            Array.Copy(full, y * aw * 4, cropped, y * td.Width * 4, td.Width * 4);
        return new DecodedImage(td.Width, td.Height, cropped);
    }

    // --- BC1 (DXT1): 2×rgb565 + 4 bytes of 2-bit indices ---
    private static void DecodeBc1Block(ReadOnlySpan<byte> b, Span<byte> outRgba)
    {
        int c0 = b[0] | (b[1] << 8);
        int c1 = b[2] | (b[3] << 8);
        Span<int> r = stackalloc int[4];
        Span<int> g = stackalloc int[4];
        Span<int> bl = stackalloc int[4];
        Span<int> a = stackalloc int[4];

        Rgb565(c0, out r[0], out g[0], out bl[0]); a[0] = 255;
        Rgb565(c1, out r[1], out g[1], out bl[1]); a[1] = 255;
        if (c0 > c1)
        {
            r[2] = (2 * r[0] + r[1]) / 3; g[2] = (2 * g[0] + g[1]) / 3; bl[2] = (2 * bl[0] + bl[1]) / 3; a[2] = 255;
            r[3] = (r[0] + 2 * r[1]) / 3; g[3] = (g[0] + 2 * g[1]) / 3; bl[3] = (bl[0] + 2 * bl[1]) / 3; a[3] = 255;
        }
        else
        {
            r[2] = (r[0] + r[1]) / 2; g[2] = (g[0] + g[1]) / 2; bl[2] = (bl[0] + bl[1]) / 2; a[2] = 255;
            r[3] = 0; g[3] = 0; bl[3] = 0; a[3] = 0;          // transparent black
        }

        var idx = b[4] | (b[5] << 8) | (b[6] << 16) | (b[7] << 24);
        for (var i = 0; i < 16; i++)
        {
            var s = (idx >> (i * 2)) & 3;
            var o = i * 4;
            outRgba[o] = (byte)r[s];
            outRgba[o + 1] = (byte)g[s];
            outRgba[o + 2] = (byte)bl[s];
            outRgba[o + 3] = (byte)a[s];
        }
    }

    // --- BC3 (DXT5): 8-byte alpha (a0,a1 + 16×3-bit) then a BC1-style
    //     colour block that always uses the 4-colour (opaque) mode. ---
    private static void DecodeBc3Block(ReadOnlySpan<byte> b, Span<byte> outRgba)
    {
        int a0 = b[0], a1 = b[1];
        Span<int> al = stackalloc int[8];
        al[0] = a0; al[1] = a1;
        if (a0 > a1)
            for (var i = 1; i < 7; i++) al[i + 1] = ((7 - i) * a0 + i * a1) / 7;
        else
        {
            for (var i = 1; i < 5; i++) al[i + 1] = ((5 - i) * a0 + i * a1) / 5;
            al[6] = 0; al[7] = 255;
        }
        // 48-bit alpha index block (3 bits/texel), little-endian.
        ulong abits = 0;
        for (var i = 0; i < 6; i++) abits |= (ulong)b[2 + i] << (8 * i);

        int c0 = b[8] | (b[9] << 8);
        int c1 = b[10] | (b[11] << 8);
        Span<int> r = stackalloc int[4];
        Span<int> g = stackalloc int[4];
        Span<int> bl = stackalloc int[4];
        Rgb565(c0, out r[0], out g[0], out bl[0]);
        Rgb565(c1, out r[1], out g[1], out bl[1]);
        r[2] = (2 * r[0] + r[1]) / 3; g[2] = (2 * g[0] + g[1]) / 3; bl[2] = (2 * bl[0] + bl[1]) / 3;
        r[3] = (r[0] + 2 * r[1]) / 3; g[3] = (g[0] + 2 * g[1]) / 3; bl[3] = (bl[0] + 2 * bl[1]) / 3;

        var cidx = b[12] | (b[13] << 8) | (b[14] << 16) | (b[15] << 24);
        for (var i = 0; i < 16; i++)
        {
            var cs = (cidx >> (i * 2)) & 3;
            var as_ = (int)((abits >> (i * 3)) & 7);
            var o = i * 4;
            outRgba[o] = (byte)r[cs];
            outRgba[o + 1] = (byte)g[cs];
            outRgba[o + 2] = (byte)bl[cs];
            outRgba[o + 3] = (byte)al[as_];
        }
    }

    private static void Rgb565(int c, out int r, out int g, out int b)
    {
        var r5 = (c >> 11) & 0x1F;
        var g6 = (c >> 5) & 0x3F;
        var b5 = c & 0x1F;
        r = (r5 << 3) | (r5 >> 2);
        g = (g6 << 2) | (g6 >> 4);
        b = (b5 << 3) | (b5 >> 2);
    }
}
