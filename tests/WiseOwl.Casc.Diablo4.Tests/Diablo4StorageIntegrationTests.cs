using System.IO;
using System.Linq;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;
using Xunit;

namespace WiseOwl.Casc.Diablo4.Tests;

/// <summary>
/// End-to-end against a real Diablo IV install: SNO read by id (Meta +
/// Payload + shared-payload), CoreTOC via TVFS, the combined-meta bundle,
/// and image-agnostic BC3 decode. Acceptance values are the ones the
/// ParagonOptimizer assessment specified. Self-skips with no install.
/// </summary>
public sealed class Diablo4StorageIntegrationTests
{
    private static string? Install()
    {
        var env = System.Environment.GetEnvironmentVariable("WISEOWL_CASC_INSTALL");
        if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env!, ".build.info")))
            return env;
        const string d4 = @"D:\Diablo IV";
        return File.Exists(Path.Combine(d4, ".build.info")) ? d4 : null;
    }

    /// <summary>FR-1: open fenris, resolve CoreTOC via TVFS, then resolve +
    /// BLTE-read records strictly by SNO id (Meta).</summary>
    [SkippableFact]
    public void Reads_SNO_meta_by_id()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");

        using var d4 = Diablo4Storage.Open(install!);

        Assert.Equal("Paragon_Warlock_00",
            d4.CoreToc.GetName(SnoGroup.ParagonBoard, 2458674));
        Assert.Equal(@"Base\Meta\678776", Diablo4Storage.SnoPath(678776));

        // ParagonNode 678776: ~236 B, SNO signature 0xDEADBEEF, id at 0x10.
        var node = d4.ReadSno(SnoGroup.ParagonNode, 678776);
        var rec = new SnoRecord(node);
        Assert.Equal(SnoRecord.ExpectedSignature, rec.Signature);
        Assert.Equal(678776, rec.SnoId);

        // ParagonBoard 2458674 parses (board's own id at payload base).
        var board = new SnoRecord(d4.ReadSno(SnoGroup.ParagonBoard, 2458674));
        Assert.Equal(2458674, board.SnoId);

        // GameBalance AttributeFormulas (SNO 201912) is reachable too.
        var gam = d4.ReadSno(SnoGroup.GameBalance, 201912);
        Assert.True(gam.Length > 1000, $"gam blob {gam.Length} B");

        // Try-variant must not throw on a legitimately-absent SNO.
        Assert.False(d4.TryReadSno(SnoGroup.ParagonNode, 1, SnoFolder.Meta, out _));
        Assert.Throws<SnoNotFoundException>(
            () => d4.ReadSno(SnoGroup.ParagonNode, 1));
    }

    /// <summary>FR-2: texture payload by id, including shared-payload
    /// aliasing for a class atlas that has no direct payload.</summary>
    [SkippableFact]
    public void Reads_texture_payload_with_shared_payload_aliasing()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");

        using var d4 = Diablo4Storage.Open(install!);

        // Direct payloads by id (FR-2 acceptance sizes). On the current
        // build the full TVFS exposes both atlases directly; the
        // shared-payload alias is a transparent fallback when it does not.
        Assert.Equal(576_000,
            d4.ReadSno(SnoGroup.Texture, 1314234, SnoFolder.Payload).Length);
        Assert.Equal(632_832,
            d4.ReadSno(SnoGroup.Texture, 2550887, SnoFolder.Payload).Length);

        // The 0xABBA0003 mapping is parsed and populated.
        Assert.True(d4.SharedPayloads.Count > 10_000,
            $"shared-payload entries: {d4.SharedPayloads.Count}");

        // Prove transparent aliasing on a real entry: find a mapping whose
        // requesting id has NO direct Base\Payload\<id> but whose source
        // does — ReadSno(Payload) on the requester must return the source's
        // exact bytes (the alias was followed).
        var proved = false;
        foreach (var (key, src) in d4.SharedPayloads.Pairs())
        {
            var hasDirect = d4.Casc.TryResolvePath(
                $@"Base\Payload\{key}", out _);
            if (hasDirect) continue;
            if (!d4.Casc.TryResolvePath($@"Base\Payload\{src}", out _)) continue;

            var viaAlias = d4.ReadSno(SnoGroup.Texture, key, SnoFolder.Payload);
            var direct = d4.ReadSno(SnoGroup.Texture, src, SnoFolder.Payload);
            Assert.NotEmpty(viaAlias);
            Assert.Equal(direct, viaAlias);
            Assert.True(d4.TryGetSharedPayloadSource(key, out var s) && s == src);
            proved = true;
            break;
        }
        Assert.True(proved, "no exercisable shared-payload alias entry found");
    }

    /// <summary>FR-1/FR-4/FR-5: combined-meta catalog + image-agnostic BC3
    /// decode + atlas frame crop, no imaging dependency.</summary>
    [SkippableFact]
    public void Combined_meta_and_image_agnostic_bc3_decode()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");

        using var d4 = Diablo4Storage.Open(install!);

        var meta = d4.TextureMeta;
        Assert.True(meta.BySno.Count > 1000, $"defs {meta.BySno.Count}");
        Assert.True(meta.TryGet(1208406, out var td));     // 2DUI_ParagonNodes
        Assert.Equal(TextureCodec.Bc3, td.Codec);
        Assert.True(td.Frames.Count > 0);

        var payload = d4.ReadSno(SnoGroup.Texture, 1208406, SnoFolder.Payload);
        var img = td.DecodeMip0(payload);                  // raw RGBA32
        Assert.Equal(td.Width, img.Width);
        Assert.Equal(td.Height, img.Height);
        Assert.Equal(img.Width * img.Height * 4, img.Rgba.Length);

        // Crop one ptFrame and prove it is real art (not blank): some pixels
        // opaque, some transparent (atlas icons sit on transparency).
        var f = td.Frames[0];
        var (x, y, w, h) = f.PixelRect(img.Width, img.Height);
        var crop = img.Crop(x, y, w, h);
        var opaque = 0;
        for (var i = 3; i < crop.Length; i += 4) if (crop[i] > 16) opaque++;
        var px = w * h;
        Assert.InRange(opaque, 1, px - 1);
    }

    /// <summary>FR-C7 §7.4: the generic UI-scene reader. Decodes
    /// <c>ParagonBoard</c> (657304, group 46, <c>0xE4825AB8</c>) and
    /// asserts the spec §10 facts proven during RE: the root widget's
    /// class id, and the CanvasRef rect (nWidth 1920 / nHeight 1200)
    /// bound on <c>ParagonBoard_main</c>. Self-skips with no
    /// install.</summary>
    [SkippableFact]
    public void ReadUiScene_decodes_ParagonBoard_widget_graph()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var scene = d4.ReadUiScene(657304);               // ParagonBoard
        Assert.Equal(657304, scene.SnoId);
        Assert.True(scene.Widgets.Count > 50,
            $"expected a rich widget graph, got {scene.Widgets.Count}");

        // The root widget, by the pinned header (§10.3): name +
        // classOff(0xFFFFFFFF sentinel) decoded its class id.
        var root = scene.Widgets.First(w => w.Name == "ParagonBoard_main");
        Assert.Equal(0x1E3077C7u, root.ClassId);

        // Named via the shipped hashes; values are the decoded facts
        // (CanvasRef = 1920×1200; §10 / §10.11 table).
        uint fW = Diablo4.FieldHash("nWidth");
        uint fH = Diablo4.FieldHash("nHeight");
        Assert.Contains(scene.Widgets, w =>
            w.Fields.Any(f => f.FieldHash == fW && f.HasValue && f.RawValue == 1920) &&
            w.Fields.Any(f => f.FieldHash == fH && f.HasValue && f.RawValue == 1200));

        // The node container is present (the §10.11 ParagonNodes widget).
        Assert.Contains(scene.Widgets, w => w.Name == "ParagonNodes");

        // Every schema entry's separator is DT_BINDABLEPROPERTY by
        // construction, so every field's TypeHash is a real DT_* id
        // (DT_INT etc.) — sanity that pairing held.
        Assert.Contains(scene.Widgets, w =>
            w.Fields.Any(f => f.TypeHash == Diablo4.TypeHash("DT_INT")));
    }
}
