// FR-T1 (#31) atlas-browser GUI — a WinForms visual browser over d4.Catalog.
// Browse/filter the UI texture atlases, view an atlas mip0, and list its frames,
// all through the FR-C20 Catalog API (Find / TryPeek / TryGetAtlasImage /
// TryGet<TextureDefinition>). Visual counterpart to build/AtlasExport.
using System.Windows.Forms;
using SkiaSharp;
using WiseOwl.Casc.Diablo4;

namespace AtlasBrowser;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string install = args.Length > 0 && Directory.Exists(args[0]) ? args[0] : @"D:\Diablo IV";
        ApplicationConfiguration.Initialize();
        Application.Run(new BrowserForm(install));
    }
}

/// <summary>The browser window: a name/codec filter + atlas list on the left, the
/// selected atlas image + its frame list on the right. All data comes from
/// <see cref="Diablo4Storage.Catalog"/>.</summary>
internal sealed class BrowserForm : Form
{
    private readonly Diablo4Storage _d4;
    private readonly TextBox _filter = new() { Dock = DockStyle.Top, PlaceholderText = "filter name… (e.g. 2DUI_Achievements)" };
    private readonly ComboBox _codec = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox _atlases = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly PictureBox _image = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(24, 24, 28) };
    private readonly ListBox _frames = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft };
    private List<AssetRef> _all = [];

    public BrowserForm(string installPath)
    {
        Text = "WiseOwl.Casc — UI Atlas Browser (d4.Catalog)";
        Width = 1280; Height = 820;
        StartPosition = FormStartPosition.CenterScreen;

        _d4 = Diablo4Storage.Open(installPath);

        // Codec filter from the decode-free facet tags.
        _codec.Items.Add("(any codec)");
        foreach (var c in Enum.GetNames<TextureCodec>()) _codec.Items.Add(c.ToLowerInvariant());
        _codec.SelectedIndex = 0;

        var left = new Panel { Dock = DockStyle.Left, Width = 380 };
        left.Controls.Add(_atlases);
        left.Controls.Add(_codec);
        left.Controls.Add(_filter);

        var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 560 };
        right.Panel1.Controls.Add(_image);
        right.Panel2.Controls.Add(_frames);

        Controls.Add(right);
        Controls.Add(left);
        Controls.Add(_status);

        _filter.TextChanged += (_, _) => RefreshList();
        _codec.SelectedIndexChanged += (_, _) => RefreshList();
        _atlases.SelectedIndexChanged += (_, _) => ShowSelected();

        // All UI atlases, ordered by name, once.
        _all = _d4.Catalog.Find(new AssetQuery { Kind = AssetKind.TextureAtlas, OrderByName = true }).ToList();
        RefreshList();
    }

    private void RefreshList()
    {
        string name = _filter.Text.Trim();
        string? codec = _codec.SelectedIndex > 0 ? $"codec:{_codec.SelectedItem}" : null;
        _atlases.BeginUpdate();
        _atlases.Items.Clear();
        int n = 0;
        foreach (var r in _all)
        {
            if (name.Length > 0 && !r.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (codec is not null && !r.Tags.Contains(codec)) continue;
            _atlases.Items.Add(new AtlasItem(r));
            n++;
        }
        _atlases.EndUpdate();
        _status.Text = $"{n} / {_all.Count} atlases  ·  install loaded";
    }

    private void ShowSelected()
    {
        _image.Image?.Dispose();
        _image.Image = null;
        _frames.Items.Clear();
        if (_atlases.SelectedItem is not AtlasItem item) return;
        var r = item.Ref;

        _d4.Catalog.TryPeek(r, out var f);
        if (_d4.Catalog.TryGetAtlasImage(r, out var img))
            _image.Image = ToBitmap(img);
        else
            _status.Text = $"{r.Name}: codec {f.Codec} not decodable (BC1/BC3 only)";

        if (_d4.Catalog.TryGet<TextureDefinition>(r, out var td))
        {
            for (int i = 0; i < td.Frames.Count; i++)
            {
                var (x, y, w, h) = td.Frames[i].PixelRect(td.Width, td.Height);
                _frames.Items.Add($"{i:D3}  0x{td.Frames[i].ImageHandle:X8}  {w}x{h} @ ({x},{y})");
            }
        }
        _status.Text = $"{r.Name}  (SNO {r.Sno})  ·  {f.Codec} {f.Width}x{f.Height}  ·  {f.FrameCount} frames";
    }

    // SkiaSharp RGBA → System.Drawing.Bitmap via a PNG round-trip (robust, no
    // pixel-order fiddling); the atlases are small enough that this is fine.
    private static Bitmap ToBitmap(DecodedImage img)
    {
        using var bmp = new SKBitmap(new SKImageInfo(img.Width, img.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(img.Rgba, 0, bmp.GetPixels(), img.Rgba.Length);
        using var data = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 90);
        return new Bitmap(new MemoryStream(data.ToArray()));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _image.Image?.Dispose(); _d4.Dispose(); }
        base.Dispose(disposing);
    }

    private sealed record AtlasItem(AssetRef Ref)
    {
        public override string ToString() => Ref.Name;
    }
}
