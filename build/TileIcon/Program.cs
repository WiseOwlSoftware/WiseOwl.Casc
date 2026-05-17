using SkiaSharp;

// Composite a finished raster design onto the shared Wise Owl dark tile,
// emitting the committed PNG size ladder. Source colours + alpha are
// preserved (no recolour, no trace). The big source is read only here.
//
//   dotnet run --project build/TileIcon -c Release -- <in> <outStem> [pad]

var inPath  = args.Length > 0 ? args[0] : @"assets/Brown Owl.png";
var stem    = args.Length > 1 ? args[1] : @"assets/icons/wiseowl-org";
var padFrac = args.Length > 2 ? double.Parse(args[2]) : 0.06;
int[] sizes = [16, 32, 48, 64, 128, 256, 512];

using var src = SKBitmap.Decode(inPath);
if (src is null) { Console.Error.WriteLine($"cannot decode {inPath}"); return 1; }

foreach (var n in sizes)
{
    using var bmp = new SKBitmap(n, n, SKColorType.Rgba8888, SKAlphaType.Premul);
    using (var c = new SKCanvas(bmp))
    {
        c.Clear(SKColors.Transparent);
        float r = n * 0.205f;

        // brand dark tile (#161b22 → #0b0e13) + subtle border
        using (var tile = new SKPaint { IsAntialias = true })
        using (var sh = SKShader.CreateLinearGradient(
            new SKPoint(0, 0), new SKPoint(0, n),
            [new SKColor(0x16, 0x1b, 0x22), new SKColor(0x0b, 0x0e, 0x13)],
            null, SKShaderTileMode.Clamp))
        {
            tile.Shader = sh;
            c.DrawRoundRect(0, 0, n, n, r, r, tile);
        }
        var bw = MathF.Max(1f, n / 128f * 2f);
        using (var border = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = bw, Color = new SKColor(0x2a, 0x33, 0x40),
        })
            c.DrawRoundRect(bw / 2, bw / 2, n - bw, n - bw, r, r, border);

        // contain the design centred, preserving aspect + colours + alpha
        float box = n * (1f - 2f * (float)padFrac);
        float s = box / MathF.Max(src.Width, src.Height);
        float dw = src.Width * s, dh = src.Height * s;
        var dest = new SKRect((n - dw) / 2, (n - dh) / 2,
                              (n + dw) / 2, (n + dh) / 2);
        using var p = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
        };
        c.DrawBitmap(src, dest, p);
    }
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes($"{stem}-{n}.png", data.ToArray());
}
Console.WriteLine($"wrote {stem}-{{{string.Join(",", sizes)}}}.png from {inPath}");
return 0;
