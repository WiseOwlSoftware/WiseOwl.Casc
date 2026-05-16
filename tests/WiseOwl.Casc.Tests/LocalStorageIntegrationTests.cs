using System.IO;
using System.Linq;
using WiseOwl.Casc;
using WiseOwl.Casc.Encoding;
using Xunit;

namespace WiseOwl.Casc.Tests;

/// <summary>
/// End-to-end proof of the local transport stack against a real Blizzard
/// install. Self-skips unless an install is present (set
/// <c>WISEOWL_CASC_INSTALL</c>, or have Diablo IV at <c>D:\Diablo IV</c>).
/// No game bytes are committed; this only reads the user's own install.
/// </summary>
public sealed class LocalStorageIntegrationTests
{
    private static string? ResolveInstall()
    {
        var env = System.Environment.GetEnvironmentVariable("WISEOWL_CASC_INSTALL");
        if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env!, ".build.info")))
            return env;
        const string d4 = @"D:\Diablo IV";
        return File.Exists(Path.Combine(d4, ".build.info")) ? d4 : null;
    }

    /// <summary>Opening the install must parse <c>.build.info</c>, the build
    /// config, and the full local index (16 buckets) — proving those three
    /// layers against real data.</summary>
    [SkippableFact]
    public void Opens_local_install_and_indexes_archives()
    {
        var install = ResolveInstall();
        Skip.If(install is null, "No local Blizzard install available.");

        using var casc = CascStorage.OpenLocal(install!);

        Assert.False(string.IsNullOrEmpty(casc.Build.BuildKey));
        Assert.Equal("tpr/fenris", casc.Build.Column("CDN Path"));
        Assert.True(casc.Index.Count > 1000,
            $"local index should hold many entries, got {casc.Index.Count}");
        Assert.NotNull(casc.Config.VfsRoot);
    }

    /// <summary>The strongest single proof: read the storage's own
    /// <c>encoding</c> table by its encoding key. That exercises the local
    /// index → archive envelope → BLTE decoder → encoding parser, all
    /// against the live install, on a large multi-chunk real blob.</summary>
    [SkippableFact]
    public void Reads_and_parses_the_real_encoding_table()
    {
        var install = ResolveInstall();
        Skip.If(install is null, "No local Blizzard install available.");

        using var casc = CascStorage.OpenLocal(install!);

        var eKey = casc.Config.EncodingEncodingKey;
        Assert.True(casc.Contains(eKey), "encoding blob must be in the local index");

        var raw = casc.Read(eKey);                       // idx → envelope → BLTE
        Assert.True(raw.Length > 1_000_000, $"encoding decoded to {raw.Length} B");
        Assert.Equal((byte)'E', raw[0]);
        Assert.Equal((byte)'N', raw[1]);

        var table = EncodingTable.Parse(raw);            // CKey → EKey index
        Assert.True(table.Count > 10_000,
            $"encoding table should map many keys, got {table.Count}");

        // Closed-loop on real data: the build config names the `install`
        // manifest as "<CKey> <EKey>". Resolving that CKey through the
        // freshly-parsed encoding table must yield an EKey that is present
        // in the local index — proving CKey → EKey → archive end to end.
        var installCKey = ContentKey.Parse(casc.Config.Values("install")[0]);
        Assert.True(table.TryGetEncodingKey(installCKey, out var installEKey),
            "encoding table should resolve the install manifest's content key");
        Assert.True(casc.Contains(installEKey),
            "the resolved install encoding key should be in the local index");
    }
}
