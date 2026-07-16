using System;
using System.IO;
using System.Linq;
using System.Text;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;
using Xunit;

namespace WiseOwl.Casc.Diablo4.Tests;

/// <summary>
/// FR-13 — the Diablo IV StringList localized-name catalog. The synthetic
/// test (CI-safe, no game bytes) proves the reverse-engineered container +
/// StringListDefinition layout; the live test proves it against the real
/// enUS bundle. Format spec: docs/casc-format.md §9.
/// </summary>
public sealed class StringListTests
{
    /// <summary>Build a minimal <c>0x44CF00F5</c> StringList bundle in
    /// memory — one table, two label→text pairs — and round-trip it. Proves
    /// the index walk (<c>B = alignUp8(prevEnd)</c>, no <c>+8</c>, SNO from
    /// index) and the 40-byte entry layout with zero game data.</summary>
    [Fact]
    public void Parses_synthetic_stringlist_bundle()
    {
        // Body for one table: header (B+20 = infoLength), entries at B+32
        // (40B each), then a UTF-8 string pool. Offsets are B-relative.
        var pool = new MemoryStream();
        var poolBase = 32 + 2 * 40;                 // strings after 2 entries
        (int off, int len) Put(string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            var off = poolBase + (int)pool.Length;
            pool.Write(bytes, 0, bytes.Length);
            pool.WriteByte(0);                       // trailing NUL (stripped)
            return (off, bytes.Length + 1);
        }
        var k0 = Put("Greeting"); var v0 = Put("Hello, {name}!");
        var k1 = Put("Farewell"); var v1 = Put("Goodbye — see you.");

        var body = new MemoryStream();
        void U32(MemoryStream m, uint v) =>
            m.Write([(byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24)], 0, 4);
        // B+0..19 header (B+20 = infoLength). entryCount=2 → infoLength=80.
        body.Write(new byte[20], 0, 20);
        U32(body, 80);                               // B+20 infoLength
        body.Write(new byte[8], 0, 8);               // B+24..31 pad
        void Entry((int off, int len) k, (int off, int len) v)
        {
            body.Write(new byte[8], 0, 8);
            U32(body, (uint)k.off); U32(body, (uint)k.len);
            body.Write(new byte[8], 0, 8);
            U32(body, (uint)v.off); U32(body, (uint)v.len);
            body.Write(new byte[8], 0, 8);
        }
        Entry(k0, v0); Entry(k1, v1);
        body.Write(pool.ToArray(), 0, (int)pool.Length);
        var bodyBytes = body.ToArray();

        // Container: u32 magic; u32 count(1); {i32 sno; u32 size}; then the
        // body at alignUp8(8 + 1*8) = 16.
        var bundle = new MemoryStream();
        U32(bundle, StringListCatalog.Magic);
        U32(bundle, 1);
        U32(bundle, 4080);                           // sno
        U32(bundle, (uint)bodyBytes.Length);         // size
        // index ends at 16, already 8-aligned → body base = 16.
        bundle.Write(bodyBytes, 0, bodyBytes.Length);

        var cat = StringListCatalog.Parse(bundle.ToArray(), "enUS");
        Assert.Equal("enUS", cat.Locale);
        Assert.Equal(1, cat.TableCount);
        var t = cat.Table(4080)!;
        Assert.Equal(4080, t.Sno);
        Assert.Equal(2, t.Entries.Count);
        Assert.True(t.TryGet("Greeting", out var g));
        Assert.Equal("Hello, {name}!", g);
        Assert.True(cat.TryGet(4080, "Farewell", out var f));
        Assert.Equal("Goodbye — see you.", f);
        Assert.False(cat.TryGet(4080, "Missing", out _));
    }

    private static string? Install()
    {
        var env = Environment.GetEnvironmentVariable("WISEOWL_CASC_INSTALL");
        if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env!, ".build.info")))
            return env;
        const string d4 = @"D:\Diablo IV";
        return File.Exists(Path.Combine(d4, ".build.info")) ? d4 : null;
    }

    /// <summary>Proven against the live enUS bundle
    /// (<c>base/StringList-Text-enUS.dat</c>) — acceptance values captured
    /// during the reverse-engineering of build 3.0.2.71886.</summary>
    [SkippableFact]
    public void Resolves_real_enUS_strings()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var cat = d4.GetStrings();                   // default enUS
        Assert.Equal("enUS", cat.Locale);
        Assert.True(cat.TableCount > 50_000,
            $"expected tens of thousands of tables, got {cat.TableCount}");

        // Table 4080 = AttributeDescriptions. Structural: the table resolves
        // by name and parses to a sane size. The exact entry count is a
        // content snapshot (grows most seasons) — pinned in
        // TypedReaderTests.Season_content_anchors_pinned_to_build_3_1_1.
        var attr = cat.Table(4080)!;
        Assert.Equal("AttributeDescriptions", attr.Name);
        Assert.True(attr.Entries.Count >= 600,
            $"AttributeDescriptions parsed only {attr.Entries.Count} entries.");

        // Table 4087 = Bnet_Chat; a stable known label/value.
        Assert.True(d4.TryGetString(4087, "ChatLink_WhisperedTo", out var w));
        Assert.Equal("{s1} whispers: {s2}", w);

        // Flat (cross-table) lookup also resolves it.
        Assert.True(cat.TryGet("ChatLink_WhisperedTo", out var w2));
        Assert.Equal("{s1} whispers: {s2}", w2);

        // Caching: same instance back for the same locale.
        Assert.Same(cat, d4.GetStrings("enUS"));
    }
}
