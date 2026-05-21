using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// One Diablo IV StringList table (a single <c>.stl</c> / SNO group 42
/// definition): an ordered set of <c>label → localized text</c> pairs.
/// A table is a domain bucket — e.g. <c>AttributeDescriptions</c>,
/// <c>Bnet_Chat</c>, a skill/affix/item table.
/// </summary>
public sealed class StringListTable
{
    private readonly Dictionary<string, string> _byLabel;

    internal StringListTable(int sno, string? name, Dictionary<string, string> byLabel)
    {
        Sno = sno;
        Name = name;
        _byLabel = byLabel;
    }

    /// <summary>The table's SNO id (group 42).</summary>
    public int Sno { get; }

    /// <summary>The table's CoreTOC name (e.g. <c>AttributeDescriptions</c>),
    /// or <see langword="null"/> if unavailable.</summary>
    public string? Name { get; }

    /// <summary>All <c>label → text</c> pairs.</summary>
    public IReadOnlyDictionary<string, string> Entries => _byLabel;

    /// <summary>Resolve a label to its localized text.</summary>
    public bool TryGet(string label, out string text)
    {
        var ok = _byLabel.TryGetValue(label, out var v);
        text = v ?? string.Empty;
        return ok;
    }
}

/// <summary>
/// A parsed per-locale Diablo IV StringList catalog
/// (<c>base/StringList-Text-&lt;locale&gt;.dat</c>): every localized
/// <c>label → text</c> string in the game for one locale, grouped by table
/// (SNO). This is genuinely a generic D4-container concern, so it lives in
/// the library; consumers map a table by name/SNO then look up labels.
/// </summary>
/// <remarks>
/// <para>The per-locale file is the same <c>0x44CF00F5</c> combined-meta
/// container as the texture catalog, but each entry's body is a
/// <b>StringListDefinition</b> and the per-entry base differs (see
/// <c>docs/casc-diablo4-format.md §6.3</c> — fully reverse-engineered and
/// validated bundle-wide against the live build):</para>
/// <list type="bullet">
/// <item><c>u32 magic (0x44CF00F5); u32 count; count × { i32 sno; u32 size }</c></item>
/// <item>per entry <c>i</c> (in index order), with <c>prevEnd</c> starting at
/// <c>8 + count*8</c>: <c>B = alignUp8(prevEnd)</c> (the body base —
/// <b>no</b> <c>+8</c>, and the SNO id comes from the index, not the body);
/// <c>prevEnd = B + size[i]</c>.</item>
/// <item>StringListDefinition body: <c>u32 infoLength @ B+20</c>;
/// <c>entryCount = infoLength / 40</c>; entries at <c>B+32</c>, stride 40:
/// <c>{ i64 pad; u32 keyOffset@+8; u32 keyLen@+12; i64 pad; u32 valOffset@+24;
/// u32 valLen@+28; i64 pad }</c>. Strings are UTF-8 at <c>B + offset</c>,
/// <c>len</c> bytes (trailing NUL stripped).</item>
/// </list>
/// </remarks>
public sealed class StringListCatalog
{
    /// <summary>The combined-meta magic (shared with the texture catalog).</summary>
    public const uint Magic = 0x44CF00F5;

    private readonly Dictionary<int, StringListTable> _bySno;

    private StringListCatalog(Dictionary<int, StringListTable> bySno, string locale)
    {
        _bySno = bySno;
        Locale = locale;
    }

    /// <summary>The locale this catalog was parsed for (e.g. <c>enUS</c>).</summary>
    public string Locale { get; }

    /// <summary>Number of tables (StringList SNOs).</summary>
    public int TableCount => _bySno.Count;

    /// <summary>All tables, keyed by SNO id.</summary>
    public IReadOnlyDictionary<int, StringListTable> Tables => _bySno;

    /// <summary>Get a table by its SNO id.</summary>
    public StringListTable? Table(int sno) =>
        _bySno.TryGetValue(sno, out var t) ? t : null;

    /// <summary>Resolve <c>label</c> within a specific table (SNO).</summary>
    public bool TryGet(int sno, string label, out string text)
    {
        text = string.Empty;
        return _bySno.TryGetValue(sno, out var t) && t.TryGet(label, out text);
    }

    /// <summary>Resolve <c>label</c> across every table (first match wins).
    /// Prefer the <see cref="TryGet(int,string,out string)"/> overload when
    /// the table is known — labels are only unique within a table.</summary>
    public bool TryGet(string label, out string text)
    {
        foreach (var t in _bySno.Values)
            if (t.TryGet(label, out text)) return true;
        text = string.Empty;
        return false;
    }

    /// <summary>
    /// Parse a per-locale StringList bundle. <paramref name="nameOf"/> maps a
    /// table's SNO id to its CoreTOC name (optional, for diagnostics).
    /// </summary>
    /// <exception cref="CascFormatException">Bad container magic.</exception>
    public static StringListCatalog Parse(
        ReadOnlySpan<byte> bundle, string locale, Func<int, string?>? nameOf = null)
    {
        var magic = Bytes.U32LE(bundle, 0);
        if (magic != Magic)
            throw new CascFormatException(
                $"StringList bundle magic 0x{magic:X8} != 0x{Magic:X8}");

        var count = (int)Bytes.U32LE(bundle, 4);
        var bySno = new Dictionary<int, StringListTable>(count);

        long prevEnd = 8 + (long)count * 8;          // end of the index
        for (var i = 0; i < count; i++)
        {
            var sno = Bytes.I32LE(bundle, 8 + i * 8);
            var size = (int)Bytes.U32LE(bundle, 12 + i * 8);

            // Body base: 8-byte-aligned previous end. NB: unlike the texture
            // catalog there is no "+8" and no per-body snoId — the SNO id is
            // positional, taken from the index.
            var b = (int)((prevEnd + 7) & ~7L);
            prevEnd = (long)b + size;
            if (b + 32 > bundle.Length) break;

            var infoLength = (int)Bytes.U32LE(bundle, b + 20);
            if (infoLength <= 0 || infoLength % 40 != 0)
            {
                bySno[sno] = new StringListTable(sno, nameOf?.Invoke(sno),
                    new Dictionary<string, string>());
                continue;
            }

            var entryCount = infoLength / 40;
            var entries = new Dictionary<string, string>(entryCount);
            var ep = b + 32;
            for (var e = 0; e < entryCount; e++, ep += 40)
            {
                if (ep + 40 > bundle.Length) break;
                var keyOff = (int)Bytes.U32LE(bundle, ep + 8);
                var keyLen = (int)Bytes.U32LE(bundle, ep + 12);
                var valOff = (int)Bytes.U32LE(bundle, ep + 24);
                var valLen = (int)Bytes.U32LE(bundle, ep + 28);

                var label = Utf8(bundle, b + keyOff, keyLen);
                if (label.Length == 0) continue;
                entries[label] = Utf8(bundle, b + valOff, valLen);
            }

            bySno[sno] = new StringListTable(sno, nameOf?.Invoke(sno), entries);
        }

        return new StringListCatalog(bySno, locale);
    }

    /// <summary>Read a UTF-8 string of <paramref name="len"/> bytes at an
    /// absolute buffer offset, stripping any trailing NUL bytes. Returns
    /// <see cref="string.Empty"/> if the span is out of range.</summary>
    private static string Utf8(ReadOnlySpan<byte> b, int off, int len)
    {
        if (off < 0 || len < 0 || off + len > b.Length) return string.Empty;
        var n = len;
        while (n > 0 && b[off + n - 1] == 0) n--;
        if (n == 0) return string.Empty;
        return System.Text.Encoding.UTF8.GetString(b.Slice(off, n));
    }
}
