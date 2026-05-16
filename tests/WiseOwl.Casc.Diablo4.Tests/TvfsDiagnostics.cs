using System;
using System.IO;
using System.Linq;
using WiseOwl.Casc;
using WiseOwl.Casc.Internal;
using Xunit;
using Xunit.Abstractions;

namespace WiseOwl.Casc.Diablo4.Tests;

/// <summary>Throwaway investigation: dump what the clean-room TVFS walk
/// actually produces against the live install, and probe which per-SNO
/// path form (name-path vs id-keyed <c>base:meta\id</c>) resolves.</summary>
public sealed class TvfsDiagnostics(ITestOutputHelper o)
{
    private static string? Install()
    {
        var env = Environment.GetEnvironmentVariable("WISEOWL_CASC_INSTALL");
        if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env!, ".build.info")))
            return env;
        const string d4 = @"D:\Diablo IV";
        return File.Exists(Path.Combine(d4, ".build.info")) ? d4 : null;
    }

    [SkippableFact]
    public void Dump_tvfs_shape_and_probe_sno_path_forms()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install.");

        using var casc = CascStorage.OpenLocal(install!);
        var tvfs = casc.Tvfs!;
        var s = tvfs.Stats;

        o.WriteLine($"TVFS entries          : {tvfs.Count:N0}");
        o.WriteLine($"file nodes            : {s.FileNodes:N0}");
        o.WriteLine($"sub-manifest spans    : {s.SubManifestSpans:N0}");
        o.WriteLine($"sub-manifests descend : {s.SubManifestsDescended:N0}");
        o.WriteLine($"max depth             : {s.MaxDepth}");
        o.WriteLine("sample reconstructed paths:");
        foreach (var p in s.SamplePaths.Take(40)) o.WriteLine("  " + p);

        // Probe candidate per-SNO path forms for known ids.
        (string Label, string Path)[] probes =
        [
            ("name pbd",   @"Base\Meta\108\Paragon_Warlock_00.pbd"),
            ("name pgn",   @"Base\Meta\106\Generic_Normal_Int.pgn"),
            ("id meta",    @"base:meta\2458674"),
            ("id meta2",   @"base:meta\678776"),
            ("id payload", @"base:payload\1314234"),
            ("base meta\\","Base\\Meta\\2458674"),
            ("colon Base", @"Base:Meta\2458674"),
            ("toplevel",   @"Base\CoreTOC.dat"),
        ];
        o.WriteLine("path-form probes:");
        foreach (var (label, path) in probes)
            o.WriteLine($"  {label,-12} {(tvfs.TryResolve(path, out _) ? "HIT " : "miss")}  {path}");

        // Also: do ANY entries hash-match the id-keyed forms in bulk?
        var hMeta = CascPathHash.OfPath(@"base:meta\2458674");
        o.WriteLine($"id-meta hash present  : {tvfs.Entries.ContainsKey(hMeta)}");

        // Shared-payload mapping diagnostics.
        var spResolved = tvfs.TryResolve(@"Base\CoreTOCSharedPayloadsMapping.dat",
            out var spEk);
        o.WriteLine($"\nSharedPayloadsMapping resolves: {spResolved}");
        if (spResolved)
        {
            var raw = casc.Read(spEk);
            o.WriteLine($"  bytes={raw.Length:N0} magic=0x{Bytes.U32LE(raw,0):X8} " +
                        $"count={Bytes.I32LE(raw,4):N0}");
            o.WriteLine("  first 6 entries {snoId -> sharedSnoId}:");
            for (var i = 0; i < 6 && 8 + i*8 + 8 <= raw.Length; i++)
                o.WriteLine($"    {Bytes.I32LE(raw,8+i*8)} -> {Bytes.I32LE(raw,12+i*8)}");
            var m = WiseOwl.Casc.Diablo4.SharedPayloadMapping.Parse(raw);
            o.WriteLine($"  parsed count={m.Count:N0}  " +
                $"2550887 in map={m.TryGetSource(2550887, out var src2)} (src={src2})");
        }
        // Does the Warlock atlas have a direct Base\Payload\<id>?
        o.WriteLine($"Base\\Payload\\2550887 resolves: " +
            $"{tvfs.TryResolve(@"Base\Payload\2550887", out _)}");
        o.WriteLine($"Base\\Payload\\1314234 resolves: " +
            $"{tvfs.TryResolve(@"Base\Payload\1314234", out _)}");
    }
}
