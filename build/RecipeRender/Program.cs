// FR-C16 R14 validation: interpret ParagonNodeRecipe verbatim and render a
// labeled grid of (node kind × state). For each cell we evaluate every
// component's NodeActivation against the cell's fact set and composite the
// active components at their authored rects — the exact consumer contract.
//
//   dotnet run --project build/RecipeRender -- [install] [out.png]
using System.Runtime.InteropServices;
using SkiaSharp;
using WiseOwl.Casc.Diablo4;

string install = args.Length > 0 && Directory.Exists(args[0]) ? args[0] : @"D:\Diablo IV";
string outPath = args.FirstOrDefault(a => a.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                 ?? @"E:\tmp\paragon-node-recipe-grid.png";

using var d4 = Diablo4Storage.Open(install);

// Atlas-dump mode: `atlas <textureSno> <out.png>` — decode a texture's mip0
// to PNG (validation that DecodeMip0 produces coherent UI art beyond paragon).
if (args.Length >= 2 && args[0] == "atlas")
{
    int asno = int.Parse(args[1]);
    string apath = args.Length >= 3 ? args[2] : $@"E:\tmp\atlas-{asno}.png";
    if (!d4.TextureMeta.TryGet(asno, out var atd)) { Console.WriteLine($"no meta {asno}"); return 1; }
    var aimg = atd.DecodeMip0(d4.ReadSno(SnoGroup.Texture, asno, SnoFolder.Payload));
    var abmp = new SKBitmap(new SKImageInfo(aimg.Width, aimg.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
    Marshal.Copy(aimg.Rgba, 0, abmp.GetPixels(), aimg.Rgba.Length);
    using (var aenc = SKImage.FromBitmap(abmp).Encode(SKEncodedImageFormat.Png, 90))
    using (var afs = File.OpenWrite(apath)) aenc.SaveTo(afs);
    Console.WriteLine($"atlas {asno} {atd.Codec} {aimg.Width}x{aimg.Height} frames={atd.Frames.Count} -> {apath}");
    for (int fi = 0; fi < atd.Frames.Count; fi++)
    {
        var fr = atd.Frames[fi];
        var (px, py, pw, ph) = fr.PixelRect(aimg.Width, aimg.Height);
        Console.WriteLine($"  frame[{fi,2}] handle=0x{fr.ImageHandle:X8}  px=({px},{py},{pw},{ph})");
    }
    return 0;
}

var recipe = d4.ReadParagonNodeRecipe();
int cell = d4.ReadParagonBoardGrid().CellExtent;          // 100 ref units
Console.WriteLine($"recipe: {recipe.Components.Count} components, cell extent {cell}");
if (args.Contains("dump"))
    foreach (var x in recipe.Components)
        Console.WriteLine($"  z{x.ZOrder,2} {x.Source,-26} 0x{x.ImageHandle:X8} act=[{string.Join(",", x.Activation.AllOf)}] bActive={x.DefaultActive} tint={(x.Tint is { } t ? $"{t.R:X2}{t.G:X2}{t.B:X2}" : "-")} rect=({x.Rect.Left},{x.Rect.Top},{x.Rect.Width},{x.Rect.Height})");

// --- frame decode (handle -> SKBitmap), atlas-decode cached -----------------
var atlasCache = new Dictionary<int, DecodedImage>();
var bmpCache = new Dictionary<uint, SKBitmap?>();
SKBitmap? FrameBitmap(uint handle)
{
    if (handle == 0) return null;
    if (bmpCache.TryGetValue(handle, out var cached)) return cached;
    SKBitmap? result = null;
    if (d4.TryGetIconFrame(handle, out int sno, out var frame) &&
        d4.TextureMeta.TryGet(sno, out var td))
    {
        if (!atlasCache.TryGetValue(sno, out var img))
        {
            img = td.DecodeMip0(d4.ReadSno(SnoGroup.Texture, sno, SnoFolder.Payload));
            atlasCache[sno] = img;
        }
        var (x, y, w, h) = frame.PixelRect(img.Width, img.Height);
        if (w > 0 && h > 0)
        {
            var crop = img.Crop(x, y, w, h);                 // RGBA32
            var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            Marshal.Copy(crop, 0, bmp.GetPixels(), crop.Length);
            result = bmp;
        }
    }
    bmpCache[handle] = result;
    return result;
}

// --- the grid: node kinds (rows) × states (cols) ---------------------------
var kinds = new (string Label, NodeFact Fact)[]
{
    ("Common", NodeFact.KindCommon), ("Magic", NodeFact.KindMagic),
    ("Rare", NodeFact.KindRare), ("Legendary", NodeFact.KindLegendary),
    ("Socket", NodeFact.KindSocket), ("Gate", NodeFact.KindGate),
    ("Start", NodeFact.KindStart),
};
var states = new (string Label, NodeFact[] Facts)[]
{
    ("Unpurchased", new[] { NodeFact.Unpurchased }),
    ("Purchased",   new[] { NodeFact.Purchased }),
    ("Purchased+nbrs", new[] { NodeFact.Purchased, NodeFact.NeighbourPurchasableTop,
        NodeFact.NeighbourPurchasableRight, NodeFact.NeighbourPurchasedBottom, NodeFact.NeighbourPurchasedLeft }),
    ("Available",   new[] { NodeFact.Available, NodeFact.Unpurchased }),
    ("Located",     new[] { NodeFact.Located, NodeFact.Unpurchased }),
    // Selection is an external topmost cursor (not in this scene); this column
    // confirms no in-scene component wrongly activates on Selected.
    ("Selected",    new[] { NodeFact.Selected, NodeFact.Unpurchased }),
};

const int M = 26;                 // overscan margin per cell (ref units ~ px)
int cellPx = cell + 2 * M;        // 152
const int rowLabel = 96, colLabel = 30, pad = 6, title = 34;
int gridW = rowLabel + states.Length * (cellPx + pad) + pad;
int gridH = title + colLabel + kinds.Length * (cellPx + pad) + pad;

using var surface = SKSurface.Create(new SKImageInfo(gridW, gridH));
var g = surface.Canvas;
g.Clear(new SKColor(0x14, 0x14, 0x18));
using var txt = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = 14 };
using var hdr = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = 18, FakeBoldText = true };
using var dim = new SKPaint { Color = new SKColor(0x88, 0x88, 0x90), IsAntialias = true, TextSize = 12 };
using var cellBg = new SKPaint { Color = new SKColor(0x26, 0x24, 0x22) };
using var border = new SKPaint { Color = new SKColor(0x40, 0x40, 0x48), IsStroke = true, StrokeWidth = 1 };

g.DrawText("ParagonNodeRecipe — node kind × state (recipe-interpreted; active components composited at authored rects)",
    8, 22, hdr);

for (int ci = 0; ci < states.Length; ci++)
    g.DrawText(states[ci].Label, rowLabel + ci * (cellPx + pad) + pad, title + colLabel - 8, txt);

for (int ri = 0; ri < kinds.Length; ri++)
{
    int cy = title + colLabel + ri * (cellPx + pad) + pad;
    g.DrawText(kinds[ri].Label, 8, cy + cellPx / 2, txt);

    for (int ci = 0; ci < states.Length; ci++)
    {
        int cx = rowLabel + ci * (cellPx + pad) + pad;
        g.DrawRect(cx, cy, cellPx, cellPx, cellBg);

        var facts = new HashSet<NodeFact> { kinds[ri].Fact };
        // Only stat kinds have purchasable/purchased cardinal neighbours; a
        // gate/start/socket node has none, so the consumer wouldn't set those
        // facts (the gate's arrow is drawn by its neighbour, not the gate).
        bool statKind = kinds[ri].Fact is NodeFact.KindCommon or NodeFact.KindMagic
                        or NodeFact.KindRare or NodeFact.KindLegendary;
        foreach (var f in states[ci].Facts)
        {
            bool isNeighbour = f.ToString().StartsWith("Neighbour", StringComparison.Ordinal);
            if (isNeighbour && !statKind) continue;
            facts.Add(f);
        }

        int drawn = 0;
        foreach (var comp in recipe.Components)         // already z-ordered
        {
            if (!comp.Activation.Evaluate(facts)) continue;
            // The per-node symbol slot is runtime per-node art (the node's
            // HIconMask / class emblem), not a generic atlas frame — skip its
            // template-default in this structural grid (a socket node draws no
            // symbol; a start/legendary node draws its class emblem at runtime).
            if (comp.Source == "Node_Icon") continue;
            var bmp = FrameBitmap(comp.ImageHandle);
            if (bmp is null) continue;
            var r = comp.Rect;   // already resolved to absolute cell-space placement
            var dest = new SKRect(cx + M + r.Left, cy + M + r.Top,
                                  cx + M + r.Left + r.Width, cy + M + r.Top + r.Height);
            using var p = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
            // Authored rgbaTint (multiply) folded with the per-layer alpha.
            byte tr = comp.Tint?.R ?? 255, tg = comp.Tint?.G ?? 255, tb = comp.Tint?.B ?? 255;
            byte ta = (byte)((comp.Tint?.A ?? 255) * comp.Alpha / 255);
            if (tr != 255 || tg != 255 || tb != 255 || ta != 255)
                p.ColorFilter = SKColorFilter.CreateBlendMode(
                    new SKColor(tr, tg, tb, ta), SKBlendMode.Modulate);
            g.DrawBitmap(bmp, dest, p);
            drawn++;
        }
        // 100-ref cell outline (centering reference) + cell border.
        using (var ref100 = new SKPaint { Color = new SKColor(0x3a, 0x6a, 0x3a), IsStroke = true, StrokeWidth = 1 })
            g.DrawRect(cx + M, cy + M, cell, cell, ref100);
        g.DrawRect(cx, cy, cellPx, cellPx, border);
        g.DrawText($"{drawn} layers", cx + 4, cy + cellPx - 5, dim);
    }
}

g.Flush();
using var img2 = surface.Snapshot();
using var data = img2.Encode(SKEncodedImageFormat.Png, 100);
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
using (var fs = File.OpenWrite(outPath)) data.SaveTo(fs);
Console.WriteLine($"wrote {outPath} ({gridW}x{gridH})");

// --- diagnostic: each key disc handle decoded individually -----------------
var probe = new (string Label, uint H)[]
{
    ("Common unsel 1D16", 0x1D166DC7), ("Common sel D305", 0xD3051CCA),
    ("Magic unsel 621C", 0x621CB6FF), ("Magic sel 72C2", 0x72C29402), ("Magic FEC3", 0xFEC31E48),
    ("Rare unsel B71B", 0xB71BD068), ("Rare sel 03ED", 0x03EDABAB), ("Rare F837", 0xF8373491),
    ("Leg unsel 232D", 0x232DF7F9), ("Leg sel BD27", 0xBD27FB7C), ("Leg CC3E", 0xCC3E3B25), ("Leg 006E", 0x006ED182),
    ("Gate C2DF", 0xC2DF4786), ("Gate 0E6B", 0x0E6B6249), ("Gate A0F9 filigree", 0xA0F996FE),
    ("Start F831 base", 0xF8312CA8),
    // selection/overlay candidates (which is the white spiked SQUARE?)
    ("NodeAvailableGlow 4A90", 0x4A901508), ("SearchHighlight 49FD", 0x49FDA722),
    ("Node_Located 87A8", 0x87A89F86), ("GlyphNodeGlow BED4", 0xBED4CF21),
    ("EquipGlow FC80", 0xFC806F42), ("BG_Black C147", 0xC1473C21),
};
const int fcell = 132, fcols = 4, flab = 16;
int frows = (probe.Length + fcols - 1) / fcols;
int fw = fcols * (fcell + 8) + 8, fh = 24 + frows * (fcell + flab + 8) + 8;
using var fsurf = SKSurface.Create(new SKImageInfo(fw, fh));
var fg = fsurf.Canvas;
fg.Clear(new SKColor(0x14, 0x14, 0x18));
fg.DrawText("Decoded disc frames (individual, on checkerboard) — for visual diff of unselected vs selected", 8, 18, hdr);
using var chkA = new SKPaint { Color = new SKColor(0x33, 0x33, 0x38) };
using var chkB = new SKPaint { Color = new SKColor(0x44, 0x44, 0x4a) };
for (int i = 0; i < probe.Length; i++)
{
    int fx = 8 + (i % fcols) * (fcell + 8);
    int fy = 24 + (i / fcols) * (fcell + flab + 8);
    for (int yy = 0; yy < fcell; yy += 16)
        for (int xx = 0; xx < fcell; xx += 16)
            fg.DrawRect(fx + xx, fy + yy, 16, 16, ((xx / 16 + yy / 16) % 2 == 0) ? chkA : chkB);
    var bmp = FrameBitmap(probe[i].H);
    if (bmp is not null)
    {
        // fit native frame into the cell preserving aspect
        float s = System.Math.Min((float)fcell / bmp.Width, (float)fcell / bmp.Height);
        float dw = bmp.Width * s, dh = bmp.Height * s;
        var dest = new SKRect(fx + (fcell - dw) / 2, fy + (fcell - dh) / 2, fx + (fcell + dw) / 2, fy + (fcell + dh) / 2);
        using var p = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };
        fg.DrawBitmap(bmp, dest, p);
        fg.DrawText($"{probe[i].Label} {bmp.Width}x{bmp.Height}", fx + 2, fy + fcell + 12, dim);
    }
    else fg.DrawText($"{probe[i].Label} (no frame)", fx + 2, fy + fcell + 12, dim);
}
fg.Flush();
string framesPath = Path.Combine(Path.GetDirectoryName(outPath)!, "paragon-disc-frames.png");
using (var fimg = fsurf.Snapshot())
using (var fdata = fimg.Encode(SKEncodedImageFormat.Png, 100))
using (var ffs = File.OpenWrite(framesPath)) fdata.SaveTo(ffs);
Console.WriteLine($"wrote {framesPath} ({fw}x{fh})");
return 0;
