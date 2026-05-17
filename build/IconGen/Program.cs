using SkiaSharp;
using Svg.Skia;

// Rasterise every assets/icons/*.svg to a committed PNG size ladder.
// The packed NuGet icon is the 128 px PNG (NuGet's recommended size).
// Usage: dotnet run --project build/IconGen -- <assetsIconsDir> [onlyStem]

var dir = args.Length > 0 ? args[0] : "assets/icons";
var only = args.Length > 1 ? args[1] : null;
int[] sizes = [16, 32, 48, 64, 128, 256, 512];

var svgs = Directory.GetFiles(dir, "*.svg")
    .Where(f => only is null ||
                Path.GetFileNameWithoutExtension(f)
                    .Equals(only, StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => f);

foreach (var svgPath in svgs)
{
    var stem = Path.GetFileNameWithoutExtension(svgPath);
    using var svg = new SKSvg();
    if (svg.Load(svgPath) is null || svg.Picture is not { } pic)
    {
        Console.Error.WriteLine($"FAILED to load {svgPath}");
        return 1;
    }
    var box = pic.CullRect;

    foreach (var n in sizes)
    {
        using var bmp = new SKBitmap(n, n, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Transparent);
            var scale = n / Math.Max(box.Width, box.Height);
            canvas.Scale(scale);
            canvas.DrawPicture(pic);
            canvas.Flush();
        }
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var outPath = Path.Combine(dir, $"{stem}-{n}.png");
        File.WriteAllBytes(outPath, data.ToArray());
    }
    Console.WriteLine($"{stem}: wrote {string.Join(",", sizes)} px");
}
return 0;
