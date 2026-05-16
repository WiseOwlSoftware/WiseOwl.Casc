// WiseOwl.Casc sample — open a local Diablo IV install, prove the transport
// chain, and list/extract from CoreTOC. Unofficial; use only with your own
// legally-obtained game files.
//
//   dotnet run --project samples/Casc.Sample.Console -- "D:\Diablo IV"
//
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;

var install = args.Length > 0 ? args[0] : @"D:\Diablo IV";
if (!File.Exists(Path.Combine(install, ".build.info")))
{
    Console.Error.WriteLine($"No Blizzard install at '{install}'.");
    Console.Error.WriteLine("Usage: casc-sample <installPath>");
    return 1;
}

Console.WriteLine($"Opening CASC storage at {install} …");
using var casc = CascStorage.OpenLocal(install);
Console.WriteLine($"  build      : {casc.Build.Version} ({casc.Config.BuildName})");
Console.WriteLine($"  local index: {casc.Index.Count:N0} blobs");

// Resolve + BLTE-read a top-level TVFS file (proves config→idx→BLTE→TVFS).
if (casc.TryResolvePath(@"Base\CoreTOC.dat", out _))
{
    using var d4 = Diablo4Storage.Attach(casc);
    var toc = d4.CoreToc;
    Console.WriteLine($"  CoreTOC    : {toc.Entries.Count:N0} SNOs, "
        + $"{toc.GroupCount} groups");

    Console.WriteLine("\nFirst 10 ParagonBoard SNOs:");
    var i = 0;
    foreach (var e in toc.EntriesInGroup(SnoGroup.ParagonBoard))
    {
        Console.WriteLine($"  {e.Id,9}  {e.Name}");
        if (++i == 10) break;
    }

    var meta = d4.TextureMeta;
    Console.WriteLine($"\nCombined texture meta: {meta.BySno.Count:N0} definitions");
    if (meta.TryGet(1208406, out var td))
        Console.WriteLine($"  2DUI_ParagonNodes -> {td.Codec} "
            + $"{td.Width}x{td.Height}, {td.Frames.Count} atlas frames");
}

Console.WriteLine("\nDone.");
return 0;
