using System.IO;
using System.Linq;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;
using Xunit;

namespace WiseOwl.Casc.Diablo4.Tests;

/// <summary>Round-2 consumer feature requests that are fully specified and
/// verifiable now (FR-11/12/14/15). FR-13 (StringList) is intentionally
/// absent — accepted but deferred to its own RE workstream; see
/// docs/feature-backlog.md.</summary>
public sealed class FeatureBacklogRound2Tests
{
    private static string? Install()
    {
        var env = System.Environment.GetEnvironmentVariable("WISEOWL_CASC_INSTALL");
        if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env!, ".build.info")))
            return env;
        const string d4 = @"D:\Diablo IV";
        return File.Exists(Path.Combine(d4, ".build.info")) ? d4 : null;
    }

    /// <summary>FR-12: the game-wide GBID hash. CI-safe — exact known
    /// vector from upstream §7.1, no install needed.</summary>
    [Fact]
    public void GbidHash_matches_the_known_vector()
    {
        Assert.Equal(0x42C16A1Bu, Diablo4.GbidHash("ParagonNodeCoreStat_Normal"));
        // Case-insensitive (DJB2 tolower).
        Assert.Equal(
            Diablo4.GbidHash("ParagonNodeCoreStat_Normal"),
            Diablo4.GbidHash("PARAGONNODECORESTAT_NORMAL"));
    }

    /// <summary>FR-11: character-modeling groups are named, and any group
    /// resolves by id (incl. the int escape hatch) round-tripping a SNO
    /// header.</summary>
    [SkippableFact]
    public void Reads_named_and_arbitrary_groups_by_id()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        Assert.Equal(29, (int)SnoGroup.Power);
        Assert.Equal(104, (int)SnoGroup.Affix);

        foreach (var g in new[] { SnoGroup.Power, SnoGroup.Affix })
        {
            var first = d4.CoreToc.EntriesInGroup(g).First();
            // Typed and int-escape-hatch overloads resolve the same SNO.
            var a = d4.ReadSno(g, first.Id);
            var b = d4.ReadSno((int)g, first.Id);
            Assert.Equal(a, b);
            Assert.Equal(SnoRecord.ExpectedSignature, new SnoRecord(a).Signature);
        }
    }

    /// <summary>FR-12: GameBalance enumeration covers the whole group, not
    /// just SNO 201912.</summary>
    [SkippableFact]
    public void Enumerates_all_GameBalance_snos()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var gam = d4.CoreToc.EntriesInGroup(SnoGroup.GameBalance).ToList();
        Assert.True(gam.Count > 1, $"expected many GameBalance SNOs, got {gam.Count}");
        Assert.Contains(gam, e => e.Id == 201912);   // AttributeFormulas
    }

    /// <summary>FR-14: the id-keyed resolver is folder-generic — it serves
    /// <see cref="SnoFolder.Child"/> too. Probes a group for a real child
    /// entry; skips (does not fail) if this build exposes none in the
    /// sample.</summary>
    [SkippableFact]
    public void Resolves_child_folder_by_id()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        Assert.Equal(@"Base\Child\1234", Diablo4Storage.SnoPath(1234, SnoFolder.Child));
        Assert.Equal(@"Base\Child\1234-0", Diablo4Storage.SnoPath(1234, SnoFolder.Child, 0));

        // Child sub-blobs are addressed Base\Child\<id>-<subId>.
        var found = false;
        foreach (var g in new[] { SnoGroup.Power, SnoGroup.Item, SnoGroup.Texture })
        {
            foreach (var e in d4.CoreToc.EntriesInGroup(g).Take(600))
            {
                if (!d4.Casc.TryResolvePath(
                        Diablo4Storage.SnoPath(e.Id, SnoFolder.Child, 0), out _)) continue;
                Assert.True(d4.TryReadSno(g, e.Id, SnoFolder.Child, out var b,
                    subId: 0));
                Assert.NotEmpty(b);
                found = true;
                break;
            }
            if (found) break;
        }
        Skip.IfNot(found, "No Child sub-blob in the sampled ranges on this build.");
    }

    /// <summary>FR-15: bulk group streaming reuses resident state (no
    /// re-open); skips legitimately-absent ids.</summary>
    [SkippableFact]
    public void Streams_a_group_by_id()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var n = 0;
        foreach (var (id, bytes) in d4.ReadGroup(SnoGroup.ParagonBoard))
        {
            Assert.NotEmpty(bytes);
            Assert.Equal(id, new SnoRecord(bytes).SnoId);
            if (++n == 50) break;
        }
        Assert.True(n > 10, $"expected a stream of boards, got {n}");
    }
}
