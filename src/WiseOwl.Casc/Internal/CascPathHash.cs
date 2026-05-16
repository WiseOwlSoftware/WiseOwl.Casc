using System;

namespace WiseOwl.Casc.Internal;

/// <summary>
/// The 64-bit path hash CASC/TVFS uses to key file paths. The underlying
/// mixing function is Bob Jenkins' public-domain <c>lookup3</c> hash
/// (the <c>hashlittle2</c> variant, 96 bits of internal state) — the
/// algorithm every CASC implementation uses for path lookup. The type is
/// named for what it does here, not after the algorithm's author; the
/// lineage is credited in the docs and <c>NOTICE</c>.
/// </summary>
public static class CascPathHash
{
    private static uint Rot(uint x, int k) => (x << k) | (x >> (32 - k));

    /// <summary>Hash a CASC path. By convention paths are upper-cased and
    /// <c>/</c> is normalized to <c>\</c> before hashing.</summary>
    public static ulong OfPath(string path)
    {
        var norm = path.Replace('/', '\\').ToUpperInvariant();
        return Of(System.Text.Encoding.ASCII.GetBytes(norm));
    }

    /// <summary>Hash raw bytes with the <c>lookup3</c> / <c>hashlittle2</c>
    /// mixing function, returning the CASC <c>(c &lt;&lt; 32) | b</c> value.</summary>
    public static ulong Of(ReadOnlySpan<byte> data)
    {
        var length = (uint)data.Length;
        uint a = 0xDEADBEEF + length, b = a, c = a;
        if (length == 0) return ((ulong)c << 32) | b;

        // The algorithm consumes 12-byte blocks; the tail is zero-padded to a
        // 12-byte multiple (matching the reference behaviour).
        var padded = (int)(length + (12 - length % 12) % 12);
        Span<byte> buf = padded <= 256 ? stackalloc byte[padded] : new byte[padded];
        data.CopyTo(buf);
        buf.Slice(data.Length).Clear();

        // Static local function: a ref-struct span can't be captured by a
        // closure, so the buffer is passed explicitly.
        static uint U32(ReadOnlySpan<byte> s, int o) =>
            (uint)(s[o] | (s[o + 1] << 8) | (s[o + 2] << 16) | (s[o + 3] << 24));

        var pos = 0;
        for (; pos + 12 < padded; pos += 12)
        {
            a += U32(buf, pos);
            b += U32(buf, pos + 4);
            c += U32(buf, pos + 8);

            a -= c; a ^= Rot(c, 4);  c += b;
            b -= a; b ^= Rot(a, 6);  a += c;
            c -= b; c ^= Rot(b, 8);  b += a;
            a -= c; a ^= Rot(c, 16); c += b;
            b -= a; b ^= Rot(a, 19); a += c;
            c -= b; c ^= Rot(b, 4);  b += a;
        }

        // Final (last) 12-byte block — mixed with the "final" schedule.
        a += U32(buf, pos);
        b += U32(buf, pos + 4);
        c += U32(buf, pos + 8);

        c ^= b; c -= Rot(b, 14);
        a ^= c; a -= Rot(c, 11);
        b ^= a; b -= Rot(a, 25);
        c ^= b; c -= Rot(b, 16);
        a ^= c; a -= Rot(c, 4);
        b ^= a; b -= Rot(a, 14);
        c ^= b; c -= Rot(b, 24);

        return ((ulong)c << 32) | b;
    }
}
