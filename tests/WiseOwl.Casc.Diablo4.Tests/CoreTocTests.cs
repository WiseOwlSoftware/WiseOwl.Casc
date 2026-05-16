using System;
using System.IO;
using System.Linq;
using WiseOwl.Casc.Diablo4;
using Xunit;

namespace WiseOwl.Casc.Diablo4.Tests;

/// <summary>
/// <see cref="CoreToc"/> coverage. The synthetic test always runs (CI-safe,
/// no game bytes). The real-data test self-skips unless a <c>CoreTOC.dat</c>
/// the user extracted themselves is present — never committed.
/// </summary>
public sealed class CoreTocTests
{
    /// <summary>Build a minimal new-format CoreTOC.dat in memory and round-trip
    /// it. Proves the header math (sentinel, header size, header-relative
    /// EntryOffsets, groupEnd-relative name pointers) with zero game data.</summary>
    [Fact]
    public void Parses_synthetic_new_format()
    {
        // One group (id 108 = ParagonBoard) with one entry.
        const int n = 109;                     // groups 0..108
        var counts = new int[n];
        counts[108] = 1;

        // Layout after header: [12-byte record][name bytes].
        var name = "Paragon_Test_00\0"u8.ToArray();
        var groupBlock = new byte[12 + name.Length];
        // record: SnoGroup, SnoId, PName(relative to groupEnd = +12)
        WriteI32(groupBlock, 0, 108);
        WriteI32(groupBlock, 4, 999001);
        WriteI32(groupBlock, 8, 0);            // name at groupEnd + 0
        Array.Copy(name, 0, groupBlock, 12, name.Length);

        var headerSize = 12 + 16 * n;
        var file = new byte[headerSize + groupBlock.Length];
        WriteU32(file, 0, CoreToc.NewFormatSentinel);
        WriteI32(file, 4, n);
        // counts[]
        for (var i = 0; i < n; i++) WriteI32(file, 8 + i * 4, counts[i]);
        // offsets[] — header-relative; our only group sits at offset 0
        // (offsets default 0). unkCounts[] and formatHashes[] left 0 except
        // we set the ParagonBoard format hash to a sentinel.
        WriteU32(file, 8 + 12 * n + 108 * 4, 0xABCD1234);
        Array.Copy(groupBlock, 0, file, headerSize, groupBlock.Length);

        var toc = CoreToc.Parse(file);

        Assert.Equal(n, toc.GroupCount);
        var e = Assert.Single(toc.Entries);
        Assert.Equal(SnoGroup.ParagonBoard, e.Group);
        Assert.Equal(999001, e.Id);
        Assert.Equal("Paragon_Test_00", e.Name);
        Assert.Equal("Paragon_Test_00", toc.GetName(SnoGroup.ParagonBoard, 999001));
        Assert.Equal(0xABCD1234u, toc.FormatHashFor(SnoGroup.ParagonBoard));
    }

    /// <summary>Proven against the live Diablo IV build: the real
    /// <c>0xBCDE6611</c> CoreTOC.dat (~40&#160;MB) the stock CascLib NuGet
    /// overflows on. Set <c>WISEOWL_CASC_CORETOC</c> to an extracted
    /// CoreTOC.dat to run; skipped otherwise.</summary>
    [SkippableFact]
    public void Parses_real_core_toc()
    {
        var path = ResolveRealCoreToc();
        Skip.If(path is null, "No real CoreTOC.dat available (set WISEOWL_CASC_CORETOC).");

        var toc = CoreToc.Load(path!);

        // Known-good anchors verified upstream against build 3.0.2.71886.
        Assert.True(toc.Entries.Count > 100_000,
            $"expected a large directory, got {toc.Entries.Count}");
        Assert.Equal("Paragon_Warlock_00",
            toc.GetName(SnoGroup.ParagonBoard, 2458674));
        Assert.Equal("Generic_Normal_Int",
            toc.GetName(SnoGroup.ParagonNode, 678776));

        // Texture atlas group must contain the paragon atlases.
        var atlases = toc.EntriesInGroup(SnoGroup.Texture)
            .Where(x => x.Name.StartsWith("2DUI_Paragon", StringComparison.Ordinal))
            .ToList();
        Assert.Contains(atlases, x => x.Name == "2DUI_ParagonNodes");
        Assert.Contains(atlases, x => x.Name == "2DUI_ParagonGlyphs");
    }

    private static string? ResolveRealCoreToc()
    {
        var env = Environment.GetEnvironmentVariable("WISEOWL_CASC_CORETOC");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

        // Fallback: the originating project's extracted copy, if this repo is
        // checked out next to it on the dev machine. Never committed here.
        foreach (var c in new[]
        {
            @"e:\Paragon\tools\CascProbe\bin\Debug\net10.0\extracted\CoreTOC.dat",
        })
            if (File.Exists(c)) return c;
        return null;
    }

    private static void WriteI32(byte[] b, int o, int v) => WriteU32(b, o, (uint)v);

    private static void WriteU32(byte[] b, int o, uint v)
    {
        b[o] = (byte)v; b[o + 1] = (byte)(v >> 8);
        b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24);
    }
}
