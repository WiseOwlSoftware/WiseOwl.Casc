using BitmapToVector;
using BitmapToVector.SkiaSharp;
using SkiaSharp;

// Trace the hand-drawn Wise Owl card photo → a clean calligraphic SVG.
//
//   dotnet run --project build/OwlTrace -c Release -- [in] [outSvg] [opts]
//
// Opts (key=value): maxDim, threshBias, minAreaFrac, ruleWFrac, ruleHFrac,
//   close, smooth, turd, alpha, padFrac, color, tile(true/false).
//
// The big source photo is read ONLY here; it never enters any prompt.

var a = ParseArgs(args);
string inPath  = a.Pos(0, @"assets/Owl.jpg");
// The traced owl is the Wise Owl Software ORG brand mark (not a package
// icon). Tuned defaults bake in the result the owner accepted; `close`
// is kept small enough that the thin white ring between each spectacle
// rim and pupil is NOT bridged shut (so the owl still reads as bespectacled).
string outPath = a.Pos(1, @"assets/icons/wiseowl-org.svg");
int   maxDim   = a.I("maxDim", 1500);
int   bias     = a.I("threshBias", 0);
double minAreaF= a.D("minAreaFrac", 0.0014);
double ruleWF  = a.D("ruleWFrac", 0.40);
double ruleHF  = a.D("ruleHFrac", 0.055);
int   close    = a.I("close", 4);     // gap-bridge radius (dropouts)
int   smooth   = a.I("smooth", 1);    // open radius (de-jag)
int   turd     = a.I("turd", 10);
double alpha   = a.D("alpha", 1.3);
double padF    = a.D("padFrac", 0.10);
string color   = a.S("color", "#8A5A33");
bool   tile    = a.B("tile", true);
// Spectacle/pupil compositing is opt-in: the auto rim-blob detection is
// unreliable without visual verification of the source photo and can
// mis-pick a large loop. The faithful trace (eyes off) is the accepted
// org mark; targeted spectacle cleanup is a deliberate manual step.
bool   eyes    = a.B("eyes", false);   // legacy component-detected eyes (off)
bool   eyeAnat = a.B("eyeAnat", false); // anatomy-proportioned spectacles
double eyePadF = a.D("eyePadFrac", 0.10);
double pupilF  = a.D("pupilFrac", 0.40);
double rimF    = a.D("rimFrac", 0.15);  // rim stroke width / eye radius
// Great-horned-owl facial proportions (general anatomy — fact, not a
// copyrighted image): large forward eyes high in the facial disc,
// separated by ~one eye-width. Fractions of the owl bounding box.
double eyeYFrac = a.D("eyeYFrac", 0.40);
double eyeXFrac = a.D("eyeXFrac", 0.33);  // centres at x and (1-x)
double eyeRFrac = a.D("eyeRFrac", 0.165); // eye radius / bbox width

// --- 1. decode + EXIF-orient ------------------------------------------------
using var codec = SKCodec.Create(inPath);
var info = new SKImageInfo(codec.Info.Width, codec.Info.Height,
                           SKColorType.Bgra8888, SKAlphaType.Premul);
using var raw = new SKBitmap(info);
codec.GetPixels(info, raw.GetPixels());
using var upright = Orient(raw, codec.EncodedOrigin);

// --- 2. downscale -----------------------------------------------------------
double scale = (double)maxDim / Math.Max(upright.Width, upright.Height);
int w = scale < 1 ? (int)(upright.Width * scale) : upright.Width;
int h = scale < 1 ? (int)(upright.Height * scale) : upright.Height;
using var small = upright.Resize(new SKImageInfo(w, h), SKFilterQuality.High);

// --- 3. grayscale + Otsu threshold → ink mask ------------------------------
var lum = new byte[w * h];
for (int y = 0, i = 0; y < h; y++)
    for (int x = 0; x < w; x++, i++)
    {
        var c = small.GetPixel(x, y);
        lum[i] = (byte)((c.Red * 299 + c.Green * 587 + c.Blue * 114) / 1000);
    }
int thr = Math.Clamp(Otsu(lum) + bias, 1, 254);
var ink = new bool[w * h];
for (int i = 0; i < ink.Length; i++) ink[i] = lum[i] < thr;

// --- 4. connected components: drop noise, the 2 rules, and background ------
var (label, comps) = Components(ink, w, h);
long total = (long)w * h;
int minArea = (int)(total * minAreaF);
var keep = new bool[comps.Count + 1];
foreach (var c in comps)
{
    if (c.Id == 0) continue;
    bool isRule  = c.W >= ruleWF * w && c.H <= ruleHF * h;          // bottom lines
    bool isBack  = c.Touchesborder && c.Area > total * 0.22;         // card/desk
    bool isFrame = c.W > 0.92 * w && c.H > 0.92 * h;                 // whole-image
    bool isNoise = c.Area < minArea;
    keep[c.Id] = !(isRule || isBack || isFrame || isNoise);
}
// owl bbox from kept components (includes the eye area)
int minX = w, minY = h, maxX = 0, maxY = 0;
foreach (var c in comps)
{
    if (c.Id == 0 || !keep[c.Id]) continue;
    if (c.MinX < minX) minX = c.MinX; if (c.MaxX > maxX) maxX = c.MaxX;
    if (c.MinY < minY) minY = c.MinY; if (c.MaxY > maxY) maxY = c.MaxY;
}
if (maxX < minX) { Console.Error.WriteLine("no owl pixels kept — adjust threshBias/minAreaFrac"); return 1; }
int obw = maxX - minX + 1, obh = maxY - minY + 1;

// detect the two spectacle-rim blobs: upper, ~square, sized, separated.
// Their messy traced pixels are excluded; crisp rim+pupil are composited.
var eyeBox = new List<(int X0, int Y0, int X1, int Y1, double Cx, double Cy, double R)>();
if (eyeAnat)
{
    // Place the eyes from horned-owl facial proportions (not photo
    // detection): deterministic, so it cannot mis-pick a stray loop.
    double r = eyeRFrac * obw;
    double cy = minY + eyeYFrac * obh;
    foreach (var fx in new[] { eyeXFrac, 1.0 - eyeXFrac })
    {
        double cx = minX + fx * obw;
        int pX0 = (int)(cx - r), pY0 = (int)(cy - r);
        int pX1 = (int)(cx + r), pY1 = (int)(cy + r);
        eyeBox.Add((pX0, pY0, pX1, pY1, cx, cy, r));
    }
}
else if (eyes)
{
    var cand = comps.Where(c => c.Id != 0 && keep[c.Id]).Where(c =>
    {
        double ar = (double)c.W / Math.Max(1, c.H);
        double ccy = (c.MinY + c.MaxY) / 2.0;
        double af = (double)c.Area / total;
        return ar is > 0.55 and < 1.9 && ccy < minY + 0.62 * obh
               && af is > 0.0012 and < 0.10;
    }).OrderByDescending(c => c.Area).ToList();
    foreach (var c in cand)
    {
        if (eyeBox.Count == 2) break;
        double cx = (c.MinX + c.MaxX) / 2.0, cy = (c.MinY + c.MaxY) / 2.0;
        if (eyeBox.Any(e => Math.Abs(e.Cx - cx) < 0.12 * obw)) continue;
        double r = 0.5 * Math.Max(c.W, c.H);
        int p = (int)(r * eyePadF * 2);
        eyeBox.Add((c.MinX - p, c.MinY - p, c.MaxX + p, c.MaxY + p, cx, cy, r));
    }
    if (eyeBox.Count == 2 && Math.Abs(eyeBox[0].Cx - eyeBox[1].Cx) < 0.14 * obw)
        eyeBox.Clear();                       // not a separated pair → skip
}
bool InEye(int x, int y)
{
    foreach (var e in eyeBox)
        if (x >= e.X0 && x <= e.X1 && y >= e.Y0 && y <= e.Y1) return true;
    return false;
}

var mask = new bool[w * h];
for (int y = 0, i = 0; y < h; y++)
    for (int x = 0; x < w; x++, i++)
        if (ink[i] && keep[label[i]] && !InEye(x, y))
            mask[i] = true;

// --- 5. gap-bridge dropouts (close) then de-jag (open) ---------------------
mask = Close(mask, w, h, close);
if (smooth > 0) mask = Open(mask, w, h, smooth);

// --- 6. crop to owl bbox + pad, centre in a square -------------------------
int bw = maxX - minX + 1, bh = maxY - minY + 1;
int pad = (int)(Math.Max(bw, bh) * padF);
int side = Math.Max(bw, bh) + 2 * pad;
using var clean = new SKBitmap(side, side, SKColorType.Bgra8888, SKAlphaType.Opaque);
int ox = (side - bw) / 2 - minX, oy = (side - bh) / 2 - minY;
using (var cv = new SKCanvas(clean))
{
    cv.Clear(SKColors.White);
    using var p = new SKPaint { Color = SKColors.Black, IsAntialias = false };
    for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
            if (mask[y * w + x]) cv.DrawPoint(x + ox, y + oy, p);
}

// --- 7. potrace → SVG path -------------------------------------------------
var prm = new PotraceParam
{
    TurdSize = turd, AlphaMax = alpha, OptiCurve = true, OptTolerance = 0.2,
};
prm.TurnPolicy = PotraceParam.PotraceTurnpolicyMinority;
using var owl = new SKPath { FillType = SKPathFillType.EvenOdd };
foreach (var sp in PotraceSkiaSharp.Trace(prm, clean)) owl.AddPath(sp);
string d = owl.ToSvgPathData();

// --- 8. compose branded SVG (dark tile + autumn-brown owl) ----------------
var sb = new System.Text.StringBuilder();
sb.Append($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
sb.Append("<!-- WiseOwl.Casc mark: the owner's hand-drawn Wise Owl card,\n");
sb.Append("     potrace-vectorised (calligraphic), recoloured autumn brown,\n");
sb.Append("     parchment + the two bottom rules removed. Generated by\n");
sb.Append("     build/OwlTrace from assets/Owl.jpg; do not hand-edit. -->\n");
sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {side} {side}\" width=\"{side}\" height=\"{side}\">\n");
if (tile)
{
    sb.Append("  <defs><linearGradient id=\"t\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">");
    sb.Append("<stop offset=\"0\" stop-color=\"#161b22\"/><stop offset=\"1\" stop-color=\"#0b0e13\"/></linearGradient></defs>\n");
    int r = (int)(side * 0.205);
    sb.Append($"  <rect width=\"{side}\" height=\"{side}\" rx=\"{r}\" fill=\"url(#t)\"/>\n");
    sb.Append($"  <rect x=\"1\" y=\"1\" width=\"{side - 2}\" height=\"{side - 2}\" rx=\"{r - 1}\" fill=\"none\" stroke=\"#2a3340\" stroke-width=\"2\"/>\n");
}
sb.Append($"  <path fill=\"{color}\" fill-rule=\"evenodd\" d=\"{d}\"/>\n");

// crisp spectacles: rim ring + pupil + catchlight, replacing the messy
// traced eye blobs (which were excluded from the trace mask above).
var ci = System.Globalization.CultureInfo.InvariantCulture;
string F(double v) => v.ToString("0.##", ci);
foreach (var e in eyeBox)
{
    double ecx = e.Cx + ox, ecy = e.Cy + oy, R = e.R;
    double rimW = Math.Max(2, R * rimF);
    double rimR = R - rimW / 2;                       // ring centred on the rim
    double pupR = R * pupilF;
    sb.Append($"  <circle cx=\"{F(ecx)}\" cy=\"{F(ecy)}\" r=\"{F(rimR)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{F(rimW)}\"/>\n");
    sb.Append($"  <circle cx=\"{F(ecx)}\" cy=\"{F(ecy)}\" r=\"{F(pupR)}\" fill=\"#5E3F22\"/>\n");
    sb.Append($"  <circle cx=\"{F(ecx - pupR * 0.34)}\" cy=\"{F(ecy - pupR * 0.34)}\" r=\"{F(pupR * 0.30)}\" fill=\"#F0D6B0\" opacity=\"0.55\"/>\n");
}
sb.Append("</svg>\n");
File.WriteAllText(outPath, sb.ToString());
Console.WriteLine($"wrote {outPath}  (canvas {side}px, {comps.Count} comps, thr {thr}, close {close})");
return 0;

// ---------------------------------------------------------------------------
static SKBitmap Orient(SKBitmap src, SKEncodedOrigin o)
{
    if (o is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default) return src.Copy();
    bool swap = o is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                  or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
    var dst = new SKBitmap(swap ? src.Height : src.Width,
                           swap ? src.Width : src.Height,
                           SKColorType.Bgra8888, SKAlphaType.Premul);
    using var cv = new SKCanvas(dst);
    switch (o)
    {
        case SKEncodedOrigin.TopRight:    cv.Translate(dst.Width, 0); cv.Scale(-1, 1); break;
        case SKEncodedOrigin.BottomRight: cv.Translate(dst.Width, dst.Height); cv.Scale(-1, -1); break;
        case SKEncodedOrigin.BottomLeft:  cv.Translate(0, dst.Height); cv.Scale(1, -1); break;
        case SKEncodedOrigin.LeftTop:     cv.RotateDegrees(90); cv.Scale(1, -1); break;
        case SKEncodedOrigin.RightTop:    cv.Translate(dst.Width, 0); cv.RotateDegrees(90); break;
        case SKEncodedOrigin.RightBottom: cv.Translate(dst.Width, dst.Height); cv.RotateDegrees(90); cv.Scale(-1, 1); break;
        case SKEncodedOrigin.LeftBottom:  cv.Translate(0, dst.Height); cv.RotateDegrees(-90); break;
    }
    cv.DrawBitmap(src, 0, 0);
    return dst;
}

static int Otsu(byte[] g)
{
    Span<int> hist = stackalloc int[256];
    foreach (var v in g) hist[v]++;
    int n = g.Length; double sum = 0;
    for (int t = 0; t < 256; t++) sum += t * (double)hist[t];
    double sumB = 0; int wB = 0; double max = -1; int thr = 127;
    for (int t = 0; t < 256; t++)
    {
        wB += hist[t]; if (wB == 0) continue;
        int wF = n - wB; if (wF == 0) break;
        sumB += t * (double)hist[t];
        double mB = sumB / wB, mF = (sum - sumB) / wF;
        double between = (double)wB * wF * (mB - mF) * (mB - mF);
        if (between > max) { max = between; thr = t; }
    }
    return thr;
}

static (int[] label, List<Comp> comps) Components(bool[] ink, int w, int h)
{
    var label = new int[w * h];
    var comps = new List<Comp> { new(0) };
    int next = 1;
    var stack = new Stack<int>();
    for (int s = 0; s < ink.Length; s++)
    {
        if (!ink[s] || label[s] != 0) continue;
        int id = next++;
        var c = new Comp(id) { MinX = w, MinY = h };
        comps.Add(c);
        stack.Push(s); label[s] = id;
        while (stack.Count > 0)
        {
            int p = stack.Pop(); int x = p % w, y = p / w;
            c.Area++;
            if (x < c.MinX) c.MinX = x; if (x > c.MaxX) c.MaxX = x;
            if (y < c.MinY) c.MinY = y; if (y > c.MaxY) c.MaxY = y;
            if (x == 0 || y == 0 || x == w - 1 || y == h - 1) c.Touchesborder = true;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int q = ny * w + nx;
                    if (ink[q] && label[q] == 0) { label[q] = id; stack.Push(q); }
                }
        }
    }
    return (label, comps);
}

static bool[] Dilate(bool[] m, int w, int h, int r)
{
    var t = new bool[m.Length];
    for (int y = 0; y < h; y++)               // horizontal
        for (int x = 0; x < w; x++)
        {
            bool v = false;
            for (int k = -r; k <= r && !v; k++) { int xx = x + k; if (xx >= 0 && xx < w && m[y * w + xx]) v = true; }
            t[y * w + x] = v;
        }
    var o = new bool[m.Length];
    for (int x = 0; x < w; x++)                // vertical
        for (int y = 0; y < h; y++)
        {
            bool v = false;
            for (int k = -r; k <= r && !v; k++) { int yy = y + k; if (yy >= 0 && yy < h && t[yy * w + x]) v = true; }
            o[y * w + x] = v;
        }
    return o;
}

static bool[] Erode(bool[] m, int w, int h, int r)
{
    var inv = new bool[m.Length];
    for (int i = 0; i < m.Length; i++) inv[i] = !m[i];
    var d = Dilate(inv, w, h, r);
    for (int i = 0; i < d.Length; i++) d[i] = !d[i];
    return d;
}

static bool[] Close(bool[] m, int w, int h, int r) => r <= 0 ? m : Erode(Dilate(m, w, h, r), w, h, r);
static bool[] Open(bool[] m, int w, int h, int r)  => r <= 0 ? m : Dilate(Erode(m, w, h, r), w, h, r);

static Args ParseArgs(string[] argv) => new(argv);

// --- types (must follow all top-level statements + local functions) -------
sealed class Args
{
    private readonly List<string> _pos = new();
    private readonly Dictionary<string, string> _kv = new(StringComparer.OrdinalIgnoreCase);
    public Args(string[] argv)
    {
        foreach (var s in argv)
        {
            int eq = s.IndexOf('=');
            if (eq > 0) _kv[s[..eq]] = s[(eq + 1)..]; else _pos.Add(s);
        }
    }
    public string Pos(int i, string def) => i < _pos.Count ? _pos[i] : def;
    public int I(string k, int d) => _kv.TryGetValue(k, out var v) ? int.Parse(v) : d;
    public double D(string k, double d) => _kv.TryGetValue(k, out var v) ? double.Parse(v) : d;
    public string S(string k, string d) => _kv.TryGetValue(k, out var v) ? v : d;
    public bool B(string k, bool d) => _kv.TryGetValue(k, out var v) ? bool.Parse(v) : d;
}

sealed record Comp(int Id)
{
    public int Area, MinX, MinY, MaxX, MaxY;
    public bool Touchesborder;
    public int W => MaxX - MinX + 1;
    public int H => MaxY - MinY + 1;
}
