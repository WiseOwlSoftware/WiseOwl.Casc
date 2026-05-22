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
    case "iconfill":
    {
        // iconfill <handle> [handle...] — for each icon frame, measure the
        // alpha bounding box (the actual drawn shape) within its native frame,
        // and report fill% = shape extent / frame extent. Answers whether a
        // class emblem fills its 135² frame more than a stat icon does.
        if (argv.Count < 2) { Console.Error.WriteLine("iconfill <handle> [handle...]"); return 2; }
        for (int a = 1; a < argv.Count; a++)
        {
            uint h = Convert.ToUInt32(argv[a].Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16);
            if (!cat.TryGetFrameImage(h, out var fi)) { Console.WriteLine($"0x{h:X8} not decodable"); continue; }
            int minX = fi.Width, minY = fi.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < fi.Height; y++)
                for (int x = 0; x < fi.Width; x++)
                {
                    byte alpha = fi.Rgba[(y * fi.Width + x) * 4 + 3];
                    if (alpha <= 16) continue;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            if (maxX < 0) { Console.WriteLine($"0x{h:X8} {fi.Width}x{fi.Height}: empty alpha"); continue; }
            int bw = maxX - minX + 1, bh = maxY - minY + 1;
            Console.WriteLine($"0x{h:X8} frame {fi.Width}x{fi.Height}  shape {bw}x{bh} @ ({minX},{minY})  fill={100.0 * bw / fi.Width:0}%x{100.0 * bh / fi.Height:0}%");
        }
        return 0;
    }
    case "startnode":
    {
        // startnode <cellPx> <out.png> — composite the Warlock start node from
        // its recipe pieces (starter base 0xF8312CA8 @ inset-7/86², filigree
        // 0xA0F996FE @ -18/140²) with the class emblem (0x35B6E536) drawn at its
        // AUTHORED native size (135 ref units = 1.35× the 100 cell), centered.
        if (argv.Count < 3) { Console.Error.WriteLine("startnode <cellPx> <out.png>"); return 2; }
        int cellPx = int.Parse(argv[1]); double s = cellPx / 100.0;
        int canvas = cellPx * 2, off = cellPx / 2;   // 100-unit cell centred in a 200-unit canvas
        using var surf = new SKBitmap(new SKImageInfo(canvas, canvas, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var cv = new SKCanvas(surf))
        {
            cv.Clear(new SKColor(28, 28, 34));
            void Draw(uint handle, double insetUnits, double sizeUnits)
            {
                if (!cat.TryGetFrameImage(handle, out var im)) { Console.WriteLine($"  0x{handle:X8} undecodable"); return; }
                using var b = new SKBitmap(new SKImageInfo(im.Width, im.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
                System.Runtime.InteropServices.Marshal.Copy(im.Rgba, 0, b.GetPixels(), im.Rgba.Length);
                float x = (float)(off + insetUnits * s), w = (float)(sizeUnits * s);
                cv.DrawBitmap(b, new SKRect(0, 0, im.Width, im.Height), new SKRect(x, x, x + w, x + w));
            }
            Draw(0xF8312CA8u, 7, 86);                 // starter base disc (inset-7, 86²)
            Draw(0xA0F996FEu, -18, 140);              // starter filigree (overscan, 140²)
            Draw(0x35B6E536u, (100 - 135) / 2.0, 135);// Warlock emblem at NATIVE 135² (1.35× cell), centred
        }
        using (var enc = SKImage.FromBitmap(surf).Encode(SKEncodedImageFormat.Png, 95))
        using (var fs = File.OpenWrite(argv[2])) enc.SaveTo(fs);
        Console.WriteLine($"start node (emblem @ native 135²) -> {argv[2]}");
        return 0;
    }
    case "frame":
    {
        // frame <handle> <out.png> — extract a single texture frame by handle
        // (Catalog.TryGetFrameImage) for inspection/measurement.
        if (argv.Count < 3) { Console.Error.WriteLine("frame <handle> <out.png>"); return 2; }
        uint h = Convert.ToUInt32(argv[1].Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16);
        if (!cat.TryGetFrameImage(h, out var fi)) { Console.WriteLine($"0x{h:X8} not decodable"); return 1; }
        // Composite on a dark background so white-on-transparent masks are visible.
        using (var fb = new SKBitmap(new SKImageInfo(fi.Width, fi.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
        using (var fcanvas = new SKCanvas(fb))
        {
            System.Runtime.InteropServices.Marshal.Copy(fi.Rgba, 0, fb.GetPixels(), fi.Rgba.Length);
            using var bg = new SKBitmap(new SKImageInfo(fi.Width, fi.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            using (var bgc = new SKCanvas(bg)) { bgc.Clear(new SKColor(40, 40, 48)); bgc.DrawBitmap(fb, 0, 0); }
            using var enc = SKImage.FromBitmap(bg).Encode(SKEncodedImageFormat.Png, 95);
            using var fs = File.OpenWrite(argv[2]); enc.SaveTo(fs);
        }
        Console.WriteLine($"0x{h:X8} -> {fi.Width}x{fi.Height} -> {argv[2]}");
        return 0;
    }
    case "measure":
    {
        // measure <png> [whiteThresh] — column-profile a screenshot: find runs
        // of columns containing bright (symbol) pixels and runs with any node
        // content, so symbol/node widths can be compared by RATIO across
        // differently-zoomed captures. FR-C12 #22 start-icon sizing.
        if (argv.Count < 2) { Console.Error.WriteLine("measure <png> [whiteThresh]"); return 2; }
        byte wt = (byte)(argv.Count > 2 ? int.Parse(argv[2]) : 200);
        using var img = SKBitmap.Decode(argv[1]);
        Console.WriteLine($"image {img.Width}x{img.Height}, whiteThresh={wt}");
        // per-column counts of bright pixels and of "content" (non-near-black) pixels
        var bright = new int[img.Width]; var content = new int[img.Width];
        for (int x = 0; x < img.Width; x++)
            for (int y = 0; y < img.Height; y++)
            {
                var p = img.GetPixel(x, y);
                if (p.Red >= wt && p.Green >= wt && p.Blue >= wt) bright[x]++;
                if (p.Red > 70 || p.Green > 70 || p.Blue > 70) content[x]++;
            }
        void Runs(string label, int[] col, int minCount, int gap)
        {
            Console.WriteLine($"-- {label} runs (col has >= {minCount} px) --");
            int start = -1, lastOn = -1;
            for (int x = 0; x <= img.Width; x++)
            {
                bool on = x < img.Width && col[x] >= minCount;
                if (on) { if (start < 0) start = x; lastOn = x; }
                else if (start >= 0 && (x - lastOn > gap || x == img.Width))
                { Console.WriteLine($"   x[{start}..{lastOn}] width={lastOn - start + 1}"); start = -1; }
            }
        }
        Runs("bright/symbol", bright, Math.Max(2, img.Height / 12), 6);
        Runs("content/node", content, img.Height / 3, 10);
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
            // "nine": the CORRECT 9-slice mapping (verified by viewing each piece).
            // Blob order is corners-CW + centre + edges, NOT row-major:
            //   [0]=TL [1]=TR [2]=BR [3]=BL corners ; [4]=centre ;
            //   [5],[7]=vertical edges ; [6],[8]=horizontal edges.
            // "c4": just the 4 corners (verified [0]=TL [1]=TR [2]=BR [3]=BL),
            // each filling its quadrant — test whether corners alone surround the
            // node square (no edge/centre pieces).
            if (mode == "c4")
            {
                SKBitmap? Q(int i) {
                    if (!d4.Catalog.TryGetFrameImage(ts.WindowPieces[i], out var p)) return null;
                    var b = new SKBitmap(new SKImageInfo(p.Width, p.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
                    System.Runtime.InteropServices.Marshal.Copy(p.Rgba, 0, b.GetPixels(), p.Rgba.Length);
                    return b;
                }
                void BlitQ(int i, SKRect d) { using var b = Q(i); if (b is not null) canvas.DrawBitmap(b, new SKRect(0,0,b.Width,b.Height), d); }
                int h = cell / 2;
                BlitQ(0, new SKRect(0, 0, h, h));            // TL
                BlitQ(1, new SKRect(h, 0, cell, h));         // TR
                BlitQ(2, new SKRect(h, h, cell, cell));      // BR
                BlitQ(3, new SKRect(0, h, h, cell));         // BL
                goto done;
            }
            if (mode == "nine")
            {
                SKBitmap? Px(int i) {
                    if (!d4.Catalog.TryGetFrameImage(ts.WindowPieces[i], out var p)) return null;
                    var b = new SKBitmap(new SKImageInfo(p.Width, p.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
                    System.Runtime.InteropServices.Marshal.Copy(p.Rgba, 0, b.GetPixels(), p.Rgba.Length);
                    return b;
                }
                void Blit(int i, SKRect d) { using var b = Px(i); if (b is not null) canvas.DrawBitmap(b, new SKRect(0,0,b.Width,b.Height), d); }
                int cw = 64, ch = 64;   // corner native; scaled below via cs
                int cs = (int)(cw * scale);   // corner draw size (e.g. 64*0.6=38)
                int r = cell - cs;            // right/bottom corner origin
                // NB: piece [4] (centre fill) is NOT drawn — a node selection
                // highlight is a hollow border (the node shows through).
                Blit(6, new SKRect(cs, 0, r, cs));                 // top edge   (H)
                Blit(8, new SKRect(cs, r, r, cell));               // bottom edge (H)
                Blit(5, new SKRect(0, cs, cs, r));                 // left edge  (V)
                Blit(7, new SKRect(r, cs, cell, r));               // right edge (V)
                Blit(0, new SKRect(0, 0, cs, cs));                 // TL
                Blit(1, new SKRect(r, 0, cell, cs));               // TR
                Blit(2, new SKRect(r, r, cell, cell));             // BR
                Blit(3, new SKRect(0, r, cs, cell));               // BL
                goto done;
            }
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
            done: ;
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
