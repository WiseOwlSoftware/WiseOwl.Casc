// FR-C7 reconnaissance. Drives WiseOwl.Casc.Diablo4 against the live
// install to locate + characterise the paragon UI-definition SNO format.
//
//   dotnet run --project build/SnoScan -- groups
//   dotnet run --project build/SnoScan -- find <substr>
//   dotnet run --project build/SnoScan -- dump <groupId> <id> [Meta|Payload]
//
// Optional first arg before the command: install path (default D:\Diablo IV).
using WiseOwl.Casc.Diablo4;

string install = @"D:\Diablo IV";
var argv = args.ToList();
if (argv.Count > 0 && Directory.Exists(argv[0])) { install = argv[0]; argv.RemoveAt(0); }
if (argv.Count == 0) { Console.Error.WriteLine("usage: groups | find <substr> | dump <gid> <id> [folder]"); return 2; }

using var d4 = Diablo4Storage.Open(install);
var toc = d4.CoreToc;
var cmd = argv[0].ToLowerInvariant();

switch (cmd)
{
    case "groups":
    {
        var byGroup = toc.Entries
            .GroupBy(e => (int)e.Group)
            .OrderBy(g => g.Key);
        Console.WriteLine($"{toc.Entries.Count:N0} SNOs / {toc.GroupCount} groups");
        Console.WriteLine($"{"gid",5}  {"hash",-10}  {"count",7}  sample names");
        foreach (var g in byGroup)
        {
            uint h = toc.FormatHashFor((SnoGroup)g.Key);
            var names = string.Join(", ", g.Take(3).Select(e => e.Name));
            Console.WriteLine($"{g.Key,5}  0x{h:X8}  {g.Count(),7:N0}  {names}");
        }
        return 0;
    }
    case "find":
    {
        if (argv.Count < 2) { Console.Error.WriteLine("find <substr>"); return 2; }
        var sub = argv[1];
        var hits = toc.Entries
            .Where(e => e.Name.Contains(sub, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => (int)e.Group).ThenBy(e => e.Name)
            .ToList();
        Console.WriteLine($"{hits.Count} hits for '{sub}'");
        Console.WriteLine($"{"gid",5}  {"hash",-10}  {"id",9}  name");
        foreach (var e in hits)
            Console.WriteLine($"{(int)e.Group,5}  0x{toc.FormatHashFor(e.Group):X8}  {e.Id,9}  {e.Name}");
        return 0;
    }
    case "strings":
    {
        // Printable ASCII runs >= minlen, with offsets — widget-name landmarks.
        if (argv.Count < 3) { Console.Error.WriteLine("strings <gid> <id> [minlen] [folder]"); return 2; }
        int gidS = int.Parse(argv[1]), idS = int.Parse(argv[2]);
        int minlen = argv.Count > 3 ? int.Parse(argv[3]) : 4;
        var folderS = argv.Count > 4 ? Enum.Parse<SnoFolder>(argv[4], true) : SnoFolder.Meta;
        if (!d4.TryReadSno(gidS, idS, folderS, out var bs)) { Console.WriteLine("no content"); return 1; }
        int run = 0;
        for (int i = 0; i <= bs.Length; i++)
        {
            bool p = i < bs.Length && bs[i] is >= 0x20 and < 0x7f;
            if (p) run++;
            else
            {
                if (run >= minlen)
                {
                    int st = i - run;
                    Console.WriteLine($"{st:X6}  {System.Text.Encoding.ASCII.GetString(bs, st, run)}");
                }
                run = 0;
            }
        }
        return 0;
    }
    case "scan":
    {
        // Every offset where a 32-bit value appears (LE), e.g. a texture handle.
        if (argv.Count < 4) { Console.Error.WriteLine("scan <gid> <id> <hex32> [folder]"); return 2; }
        int gidH = int.Parse(argv[1]), idH = int.Parse(argv[2]);
        uint needle = Convert.ToUInt32(argv[3], 16);
        var folderH = argv.Count > 4 ? Enum.Parse<SnoFolder>(argv[4], true) : SnoFolder.Meta;
        if (!d4.TryReadSno(gidH, idH, folderH, out var bh)) { Console.WriteLine("no content"); return 1; }
        int n = 0;
        for (int i = 0; i + 4 <= bh.Length; i++)
            if (BitConverter.ToUInt32(bh, i) == needle)
            {
                // context: 8 LE u32 before/at the hit, + float view
                var ctx = new System.Text.StringBuilder();
                for (int j = -16; j <= 16; j += 4)
                {
                    int o = i + j;
                    if (o >= 0 && o + 4 <= bh.Length)
                        ctx.Append(j == 0 ? "[" : " ").Append(BitConverter.ToUInt32(bh, o).ToString("X8")).Append(j == 0 ? "]" : "");
                }
                Console.WriteLine($"{i:X6} {ctx}");
                n++;
            }
        Console.WriteLine($"-- {n} hit(s) for 0x{needle:X8} --");
        return 0;
    }
    case "f32":
    {
        // Interpret a region as float32[] (offsets + ints alongside).
        if (argv.Count < 5) { Console.Error.WriteLine("f32 <gid> <id> <hexoff> <count> [folder]"); return 2; }
        int gidF = int.Parse(argv[1]), idF = int.Parse(argv[2]);
        int offF = Convert.ToInt32(argv[3], 16);
        int cnt = int.Parse(argv[4]);
        var folderF = argv.Count > 5 ? Enum.Parse<SnoFolder>(argv[5], true) : SnoFolder.Meta;
        if (!d4.TryReadSno(gidF, idF, folderF, out var bf)) { Console.WriteLine("no content"); return 1; }
        for (int k = 0; k < cnt && offF + k * 4 + 4 <= bf.Length; k++)
        {
            int o = offF + k * 4;
            uint u = BitConverter.ToUInt32(bf, o);
            float f = BitConverter.ToSingle(bf, o);
            string fs = (f != 0 && Math.Abs(f) is > 1e-4f and < 1e7f) ? f.ToString("0.###") : "";
            Console.WriteLine($"{o:X6}  u32=0x{u:X8} ({(int)u,11})  f32={fs}");
        }
        return 0;
    }
    case "dump":
    {
        if (argv.Count < 3) { Console.Error.WriteLine("dump <gid> <id> [folder]"); return 2; }
        int gid = int.Parse(argv[1]);
        int id = int.Parse(argv[2]);
        var folder = argv.Count > 3
            ? Enum.Parse<SnoFolder>(argv[3], ignoreCase: true)
            : SnoFolder.Meta;
        if (!d4.TryReadSno(gid, id, folder, out var bytes))
        {
            Console.WriteLine($"no {folder} content for gid={gid} id={id}");
            return 1;
        }
        var name = toc.TryGetName((SnoGroup)gid, id, out var n) ? n : "<unknown>";
        Console.WriteLine($"gid={gid} id={id} folder={folder} name={name}");
        Console.WriteLine($"length={bytes.Length} bytes  hash=0x{toc.FormatHashFor((SnoGroup)gid):X8}");
        if (bytes.Length >= 4)
            Console.WriteLine($"u32@0x00=0x{BitConverter.ToUInt32(bytes, 0):X8}");
        if (bytes.Length >= 0x14)
            Console.WriteLine($"u32@0x10=0x{BitConverter.ToUInt32(bytes, 0x10):X8} (={BitConverter.ToInt32(bytes, 0x10)})");
        int show = Math.Min(bytes.Length, argv.Count > 4 ? int.Parse(argv[4]) : 512);
        for (int off = 0; off < show; off += 16)
        {
            var hex = new System.Text.StringBuilder();
            var asc = new System.Text.StringBuilder();
            for (int j = 0; j < 16 && off + j < show; j++)
            {
                byte b = bytes[off + j];
                hex.Append(b.ToString("X2")).Append(j == 7 ? "  " : " ");
                asc.Append(b is >= 0x20 and < 0x7f ? (char)b : '.');
            }
            Console.WriteLine($"{off:X4}  {hex,-49} {asc}");
        }
        return 0;
    }
    default:
        Console.Error.WriteLine($"unknown command '{cmd}'");
        return 2;
}
