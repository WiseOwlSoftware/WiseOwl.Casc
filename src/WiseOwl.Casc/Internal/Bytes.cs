using System;

namespace WiseOwl.Casc.Internal;

/// <summary>
/// Tiny endian-aware primitive readers over <see cref="ReadOnlySpan{T}"/>.
/// CASC mixes little-endian (most fields) and big-endian (BLTE/index sizes),
/// so both are first-class here. Shared by the transport and the game
/// modules so byte-level parsing is single-source.
/// </summary>
public static class Bytes
{
    /// <summary>Little-endian <see cref="ushort"/> at <paramref name="o"/>.</summary>
    public static ushort U16LE(ReadOnlySpan<byte> b, int o) =>
        (ushort)(b[o] | (b[o + 1] << 8));

    /// <summary>Little-endian <see cref="uint"/> at <paramref name="o"/>.</summary>
    public static uint U32LE(ReadOnlySpan<byte> b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));

    /// <summary>Little-endian <see cref="int"/> at <paramref name="o"/>.</summary>
    public static int I32LE(ReadOnlySpan<byte> b, int o) => unchecked((int)U32LE(b, o));

    /// <summary>Little-endian <see cref="ulong"/> at <paramref name="o"/>.</summary>
    public static ulong U64LE(ReadOnlySpan<byte> b, int o) =>
        U32LE(b, o) | ((ulong)U32LE(b, o + 4) << 32);

    /// <summary>Big-endian <see cref="uint"/> at <paramref name="o"/>
    /// (BLTE header/frame sizes are stored big-endian).</summary>
    public static uint U32BE(ReadOnlySpan<byte> b, int o) =>
        (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);

    /// <summary>Big-endian <see cref="int"/> at <paramref name="o"/>.</summary>
    public static int I32BE(ReadOnlySpan<byte> b, int o) => unchecked((int)U32BE(b, o));

    /// <summary>Read a big-endian unsigned integer of <paramref name="n"/>
    /// bytes (1..8). CASC local-index location fields are packed big-endian
    /// across an odd number of bytes.</summary>
    public static ulong UIntBE(ReadOnlySpan<byte> b, int o, int n)
    {
        ulong v = 0;
        for (var i = 0; i < n; i++) v = (v << 8) | b[o + i];
        return v;
    }

    /// <summary>A NUL-terminated ASCII string starting at <paramref name="o"/>,
    /// stopping at the first NUL or <paramref name="max"/> bytes.</summary>
    public static string AsciiZ(ReadOnlySpan<byte> b, int o, int max = int.MaxValue)
    {
        var end = o;
        var limit = (int)Math.Min((long)o + max, b.Length);
        while (end < limit && b[end] != 0) end++;
        return System.Text.Encoding.ASCII.GetString(b.Slice(o, end - o));
    }
}
