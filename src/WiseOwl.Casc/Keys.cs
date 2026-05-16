using System;
using System.Globalization;

namespace WiseOwl.Casc;

/// <summary>
/// A 128-bit CASC key (the MD5-derived hash CASC uses to address content).
/// Stored as two <see cref="ulong"/> halves so the type is a small,
/// allocation-free value with O(1) equality on every target framework
/// (including <c>netstandard2.0</c>, which lacks inline arrays).
/// </summary>
/// <remarks>
/// This is the shared representation behind the strongly-typed
/// <see cref="ContentKey"/> and <see cref="EncodingKey"/>. The two public
/// wrappers exist so the compiler stops you handing an encoding key to an
/// API that wants a content key — a real source of bugs in older CASC
/// libraries that used raw <c>byte[]</c> everywhere.
/// </remarks>
public readonly struct Md5Key : IEquatable<Md5Key>
{
    /// <summary>The number of bytes in a full CASC key.</summary>
    public const int Size = 16;

    // Big-endian-ordered halves: _hi holds bytes 0..7, _lo holds bytes 8..15.
    private readonly ulong _hi;
    private readonly ulong _lo;

    private Md5Key(ulong hi, ulong lo) { _hi = hi; _lo = lo; }

    /// <summary>Create a key from exactly 16 bytes (or the first 16 of a
    /// longer span; truncated keys are zero-padded on the right).</summary>
    /// <param name="bytes">Source bytes; 1..16 bytes are read.</param>
    public static Md5Key FromBytes(ReadOnlySpan<byte> bytes)
    {
        Span<byte> buf = stackalloc byte[Size];
        bytes.Slice(0, Math.Min(Size, bytes.Length)).CopyTo(buf);
        ulong hi = 0, lo = 0;
        for (var i = 0; i < 8; i++) hi = (hi << 8) | buf[i];
        for (var i = 8; i < 16; i++) lo = (lo << 8) | buf[i];
        return new Md5Key(hi, lo);
    }

    /// <summary>Parse a 32-character (16-byte) lowercase/uppercase hex string.
    /// Shorter even-length hex is accepted and right-zero-padded (truncated
    /// keys, e.g. a 9-byte index key, are valid).</summary>
    public static Md5Key Parse(ReadOnlySpan<char> hex)
    {
        if (!TryParse(hex, out var key))
            throw new FormatException($"'{hex.ToString()}' is not valid CASC key hex.");
        return key;
    }

    /// <summary>Try to parse a hex string into a key. Accepts 2..32 hex
    /// digits (even length); the result is right-zero-padded to 16 bytes.</summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out Md5Key key)
    {
        key = default;
        if (hex.Length is < 2 or > 32 || (hex.Length & 1) != 0) return false;
        Span<byte> buf = stackalloc byte[Size];
        for (var i = 0; i < hex.Length; i += 2)
        {
            if (!TryHex(hex[i], out var h) || !TryHex(hex[i + 1], out var l))
                return false;
            buf[i / 2] = (byte)((h << 4) | l);
        }
        key = FromBytes(buf);
        return true;

        static bool TryHex(char c, out int v)
        {
            v = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => -1,
            };
            return v >= 0;
        }
    }

    /// <summary>Copy the 16 key bytes into <paramref name="destination"/>.</summary>
    public void CopyTo(Span<byte> destination)
    {
        for (var i = 0; i < 8; i++) destination[i] = (byte)(_hi >> (56 - 8 * i));
        for (var i = 0; i < 8; i++) destination[8 + i] = (byte)(_lo >> (56 - 8 * i));
    }

    /// <summary>The first <paramref name="n"/> bytes of the key, packed into a
    /// <see cref="ulong"/> high-order-first. CASC local indices key on a
    /// 9-byte EKey prefix; this gives a fast, allocation-free bucket value.
    /// </summary>
    /// <param name="n">Prefix length in bytes (1..9).</param>
    public ulong Prefix(int n)
    {
        // n is at most 9; the 9th byte lives in _lo's top byte.
        ulong v = 0;
        for (var i = 0; i < n; i++)
        {
            var b = i < 8 ? (byte)(_hi >> (56 - 8 * i)) : (byte)(_lo >> 56);
            v = (v << 8) | b;
        }
        return v;
    }

    /// <inheritdoc/>
    public bool Equals(Md5Key other) => _hi == other._hi && _lo == other._lo;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Md5Key k && Equals(k);

    /// <inheritdoc/>
    public override int GetHashCode() => unchecked((int)(_hi ^ (_hi >> 32) ^ _lo ^ (_lo >> 32)));

    /// <summary>Lowercase 32-character hex representation.</summary>
    public override string ToString()
    {
        Span<byte> b = stackalloc byte[Size];
        CopyTo(b);
        Span<char> c = stackalloc char[Size * 2];
        for (var i = 0; i < Size; i++)
        {
            c[i * 2] = "0123456789abcdef"[b[i] >> 4];
            c[i * 2 + 1] = "0123456789abcdef"[b[i] & 0xF];
        }
        return c.ToString();
    }

    /// <summary>Value equality.</summary>
    public static bool operator ==(Md5Key a, Md5Key b) => a.Equals(b);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(Md5Key a, Md5Key b) => !a.Equals(b);
}

/// <summary>
/// A <b>content key</b> (CKey): the MD5 of a file's logical, fully-decoded
/// bytes. The game's <c>root</c>/TVFS maps game-meaningful names to CKeys;
/// the <c>encoding</c> table maps a CKey to one or more
/// <see cref="EncodingKey"/>s.
/// </summary>
public readonly struct ContentKey(Md5Key value) : IEquatable<ContentKey>
{
    /// <summary>The underlying 128-bit key value.</summary>
    public Md5Key Value { get; } = value;

    /// <summary>Wrap raw key bytes (1..16; truncated keys are zero-padded).</summary>
    public static ContentKey FromBytes(ReadOnlySpan<byte> bytes) => new(Md5Key.FromBytes(bytes));

    /// <summary>Parse from hex.</summary>
    public static ContentKey Parse(ReadOnlySpan<char> hex) => new(Md5Key.Parse(hex));

    /// <summary>Try to parse from hex.</summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out ContentKey key)
    {
        var ok = Md5Key.TryParse(hex, out var v);
        key = new ContentKey(v);
        return ok;
    }

    /// <inheritdoc/>
    public bool Equals(ContentKey other) => Value.Equals(other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ContentKey c && Equals(c);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <summary>Value equality.</summary>
    public static bool operator ==(ContentKey a, ContentKey b) => a.Equals(b);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(ContentKey a, ContentKey b) => !a.Equals(b);
}

/// <summary>
/// An <b>encoding key</b> (EKey): identifies the BLTE-encoded, stored form of
/// content (compressed and possibly encrypted). CASC archives and local
/// indices are addressed by EKey (the local index keys on the first 9 bytes).
/// </summary>
public readonly struct EncodingKey(Md5Key value) : IEquatable<EncodingKey>
{
    /// <summary>The underlying 128-bit key value.</summary>
    public Md5Key Value { get; } = value;

    /// <summary>Wrap raw key bytes (1..16; truncated keys are zero-padded).</summary>
    public static EncodingKey FromBytes(ReadOnlySpan<byte> bytes) => new(Md5Key.FromBytes(bytes));

    /// <summary>Parse from hex.</summary>
    public static EncodingKey Parse(ReadOnlySpan<char> hex) => new(Md5Key.Parse(hex));

    /// <summary>Try to parse from hex.</summary>
    public static bool TryParse(ReadOnlySpan<char> hex, out EncodingKey key)
    {
        var ok = Md5Key.TryParse(hex, out var v);
        key = new EncodingKey(v);
        return ok;
    }

    /// <summary>The 9-byte index lookup prefix CASC local indices key on.</summary>
    public ulong IndexPrefix => Value.Prefix(9);

    /// <inheritdoc/>
    public bool Equals(EncodingKey other) => Value.Equals(other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EncodingKey e && Equals(e);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    /// <summary>Value equality.</summary>
    public static bool operator ==(EncodingKey a, EncodingKey b) => a.Equals(b);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(EncodingKey a, EncodingKey b) => !a.Equals(b);
}
