// FR-T1 (#31) atlas-export CLI — a thin browser/exporter over d4.Catalog.
// Dogfoods the FR-C20 discovery+retrieval API end-to-end:
//   list   <nameSubstr> [--codec bc3]          — decode-free facets (TryPeek)
//   export <nameSubstr> <outDir> [--frames]    — atlas (+ per-frame) PNGs
// Optional first arg before the command: install path (default D:\Diablo IV).
using System.Runtime.InteropServices;
using SkiaSharp;
using WiseOwl.Casc.Diablo4;

var argv = args.ToList();
string install = @"D:\Diablo IV";
if (argv.Count > 0 && Directory.Exists(argv[0])) { install = argv[0]; argv.RemoveAt(0); }
if (argv.Count < 1)
{
    Console.Error.WriteLine("usage: list <nameSubstr> [--codec <c>] | export <nameSubstr> <outDir> [--frames]");
    return 2;
}

using var d4 = Diablo4Storage.Open(install);
var cat = d4.Catalog;
string cmd = argv[0].ToLowerInvariant();

// Build the atlas query from a name substring + optional codec tag.
AssetQuery AtlasQuery(string? name)
{
    string? codec = null;
    int ci = argv.IndexOf("--codec");
    if (ci >= 0 && ci + 1 < argv.Count) codec = $"codec:{argv[ci + 1].ToLowerInvariant()}";
    return new AssetQuery { Kind = AssetKind.TextureAtlas, NameContains = name, Tag = codec };
}

static void SavePng(DecodedImage img, string path)
{
    using var bmp = new SKBitmap(new SKImageInfo(img.Width, img.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
    Marshal.Copy(img.Rgba, 0, bmp.GetPixels(), img.Rgba.Length);
    using var enc = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 90);
    using var fs = File.OpenWrite(path);
    enc.SaveTo(fs);
}

switch (cmd)
{
    case "list":
    {
        string? name = argv.Count > 1 && !argv[1].StartsWith("--", StringComparison.Ordinal) ? argv[1] : null;
        int n = 0;
        foreach (var r in cat.Find(AtlasQuery(name)))
        {
            // Decode-free facets — never touches pixels.
            if (!cat.TryPeek(r, out var f)) continue;
            Console.WriteLine($"{r.Sno,9}  {f.Codec,-7} {f.Width}x{f.Height} frames={f.FrameCount,-4} {r.Name}");
            n++;
        }
        Console.WriteLine($"-- {n} atlas(es) --");
        return 0;
    }
    case "export":
    {
        if (argv.Count < 3) { Console.Error.WriteLine("export <nameSubstr> <outDir> [--frames]"); return 2; }
        string name = argv[1], outDir = argv[2];
        bool frames = argv.Contains("--frames");
        Directory.CreateDirectory(outDir);
        int atlases = 0, framePngs = 0, skipped = 0;
        foreach (var r in cat.Find(AtlasQuery(name)))
        {
            // Retrieve pixels via the Catalog (BC1/BC3; unsupported → skipped).
            if (!cat.TryGetAtlasImage(r, out var img))
            {
                cat.TryPeek(r, out var pf);
                Console.WriteLine($"  skip {r.Name} (codec {pf.Codec} not decodable)");
                skipped++;
                continue;
            }
            SavePng(img, Path.Combine(outDir, $"{r.Name}.png"));
            atlases++;

            if (frames && cat.TryGet<TextureDefinition>(r, out var td))
            {
                var fdir = Path.Combine(outDir, r.Name);
                Directory.CreateDirectory(fdir);
                for (int i = 0; i < td.Frames.Count; i++)
                {
                    var (x, y, w, h) = td.Frames[i].PixelRect(td.Width, td.Height);
                    if (w <= 0 || h <= 0) continue;
                    SavePng(new DecodedImage(w, h, img.Crop(x, y, w, h)),
                        Path.Combine(fdir, $"{i:D3}_0x{td.Frames[i].ImageHandle:X8}.png"));
                    framePngs++;
                }
            }
        }
        Console.WriteLine($"-- exported {atlases} atlas PNG(s), {framePngs} frame PNG(s), {skipped} skipped -> {outDir} --");
        return 0;
    }
    case "compose":
    {
        // compose <tiledStyleSno> <cellPx> <out.png> — composite a
        // TiledWindowPieces 9-slice into a cellPx² cell to study the engine's
        // composition empirically. Pieces (blob 3×3 order): 0 TL,1 T,2 TR,
        // 3 L,4 C,5 R,6 BL,7 B,8 BR. Each drawn at native size × ImageScale,
        // anchored to its zone (corners→corners, edges→edge-centres, C→centre).
        if (argv.Count < 4) { Console.Error.WriteLine("compose <sno> <cellPx> <out.png> [mode]"); return 2; }
        int tsno = int.Parse(argv[1]); int cell = int.Parse(argv[2]); string outp = argv[3];
        string mode = argv.Count > 4 ? argv[4] : "zone";   // zone | full | corners
        var ts = d4.ReadTiledStyle(tsno);
        if (ts.WindowPieces.Count != 9) { Console.WriteLine($"not a 9-piece window ({ts.WindowPieces.Count} pieces)"); return 1; }
        float scale = ts.ImageScale <= 0 ? 1f : ts.ImageScale;
        using var surface = new SKBitmap(new SKImageInfo(cell, cell, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(surface))
        {
            canvas.Clear(new SKColor(30, 30, 36));
            int[] order = mode == "corners" ? [4, 0, 2, 6, 8] : Enumerable.Range(0, 9).ToArray();
            foreach (int i in order)
            {
                if (!d4.Catalog.TryGetFrameImage(ts.WindowPieces[i], out var pi)) continue;
                using var pbmp = new SKBitmap(new SKImageInfo(pi.Width, pi.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
                System.Runtime.InteropServices.Marshal.Copy(pi.Rgba, 0, pbmp.GetPixels(), pi.Rgba.Length);
                SKRect dst;
                if (mode is "full" || (mode == "corners"))
                    dst = new SKRect(0, 0, cell, cell);    // each piece stretched to the whole cell
                else
                {
                    int w = (int)(pi.Width * scale), h = (int)(pi.Height * scale);
                    int col = i % 3, row = i / 3;
                    int x = col == 0 ? 0 : col == 1 ? (cell - w) / 2 : cell - w;
                    int y = row == 0 ? 0 : row == 1 ? (cell - h) / 2 : cell - h;
                    dst = new SKRect(x, y, x + w, y + h);
                }
                canvas.DrawBitmap(pbmp, new SKRect(0, 0, pi.Width, pi.Height), dst);
            }
        }
        using (var enc = SKImage.FromBitmap(surface).Encode(SKEncodedImageFormat.Png, 95))
        using (var fs = File.OpenWrite(outp)) enc.SaveTo(fs);
        Console.WriteLine($"composed {tsno} ({ts.VariantName}, scale {scale}) -> {outp}");
        return 0;
    }
    default:
        Console.Error.WriteLine($"unknown command '{cmd}'");
        return 2;
}
