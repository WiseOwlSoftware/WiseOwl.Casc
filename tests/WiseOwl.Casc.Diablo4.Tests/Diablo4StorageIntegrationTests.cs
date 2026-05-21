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

    /// <summary>#28 — BC row-pitch must be the texture's actual stored
    /// pitch, not a hard-coded <c>Align(width,64)</c>. Atlas 447106
    /// (1208×1464, BC1) is stored 128-aligned (pitch 1280); the legacy
    /// 64-aligned guess (1216) drifts the row stride and garbles the
    /// image. Asserts the decoder uses the size-derived pitch: its output
    /// differs from a forced-1216 decode and is more row-coherent.</summary>
    [SkippableFact]
    public void DecodeMip0_uses_stored_row_pitch_for_non_64_aligned_bc1()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        Assert.True(d4.TextureMeta.TryGet(447106, out var td)); // 2DUI_Paragon
        Assert.Equal(TextureCodec.Bc1, td.Codec);
        Assert.NotEqual(0, td.Width % 64);                      // 1208 % 64 = 56

        // The stored mip0 byte count implies an exact blocks-per-row that
        // is NOT the 64-aligned guess — this is the bug condition.
        long mip0 = td.SerTex[0].SizeAndFlags;
        int blocksY = ((td.Height + 3) / 4);
        Assert.Equal(0, (int)(mip0 % (blocksY * 8L)));
        int storedPitch = (int)(mip0 / (blocksY * 8L)) * 4;
        int legacyPitch = (td.Width + 63) / 64 * 64;
        Assert.Equal(1280, storedPitch);
        Assert.Equal(1216, legacyPitch);

        var payload = d4.ReadSno(SnoGroup.Texture, 447106, SnoFolder.Payload);
        var img = td.DecodeMip0(payload);
        Assert.Equal(td.Width, img.Width);
        Assert.Equal(td.Height, img.Height);

        // Reference decode at the buggy 1216 pitch — the shipped decode
        // must NOT match it (proves the fix is applied, catches a revert
        // to Align(width,64)).
        var buggy = DecodeBc1AtPitch(payload.AsSpan((int)td.SerTex[0].Offset),
                                     td.Width, td.Height, legacyPitch);
        Assert.False(img.Rgba.AsSpan().SequenceEqual(buggy),
            "decode matches the buggy 1216 pitch — the row-pitch fix is not applied");

        // …and the shipped decode is more row-coherent (the drift bug
        // scrambles rows → larger row-to-row luminance deltas).
        Assert.True(RowCoherence(img.Rgba, td.Width, td.Height)
                  < RowCoherence(buggy, td.Width, td.Height),
            "shipped decode is no more coherent than the buggy 1216 decode");
    }

    private static double RowCoherence(byte[] p, int w, int h)
    {
        double sum = 0; long n = 0;
        for (var y = 1; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4, j = ((y - 1) * w + x) * 4;
                sum += Math.Abs((p[i] + p[i + 1] + p[i + 2]) - (p[j] + p[j + 1] + p[j + 2]));
                n++;
            }
        return n > 0 ? sum / n : 0;
    }

    // Minimal BC1 decode at an explicit row pitch — reference for the
    // #28 regression (independent of the library's pitch logic).
    private static byte[] DecodeBc1AtPitch(ReadOnlySpan<byte> src, int width, int height, int pitch)
    {
        int ah = (height + 3) / 4 * 4, byN = ah / 4, bxN = pitch / 4;
        var full = new byte[pitch * ah * 4];
        Span<byte> blk = stackalloc byte[64];
        for (var by = 0; by < byN; by++)
            for (var bx = 0; bx < bxN; bx++)
            {
                int bo = (by * bxN + bx) * 8;
                if (bo + 8 > src.Length) break;
                Bc1Block(src.Slice(bo, 8), blk);
                for (var py = 0; py < 4; py++)
                    for (var px = 0; px < 4; px++)
                    {
                        int di = ((by * 4 + py) * pitch + bx * 4 + px) * 4, si = (py * 4 + px) * 4;
                        full[di] = blk[si]; full[di + 1] = blk[si + 1];
                        full[di + 2] = blk[si + 2]; full[di + 3] = blk[si + 3];
                    }
            }
        var crop = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            Array.Copy(full, y * pitch * 4, crop, y * width * 4, width * 4);
        return crop;
    }

    private static void Bc1Block(ReadOnlySpan<byte> b, Span<byte> o)
    {
        int c0 = b[0] | (b[1] << 8), c1 = b[2] | (b[3] << 8);
        Span<int> r = stackalloc int[4], g = stackalloc int[4], bl = stackalloc int[4], a = stackalloc int[4];
        static void U(int c, Span<int> r, Span<int> g, Span<int> bl, int i)
        { int r5 = (c >> 11) & 0x1F, g6 = (c >> 5) & 0x3F, b5 = c & 0x1F; r[i] = (r5 << 3) | (r5 >> 2); g[i] = (g6 << 2) | (g6 >> 4); bl[i] = (b5 << 3) | (b5 >> 2); }
        U(c0, r, g, bl, 0); a[0] = 255; U(c1, r, g, bl, 1); a[1] = 255;
        if (c0 > c1)
        { r[2] = (2 * r[0] + r[1]) / 3; g[2] = (2 * g[0] + g[1]) / 3; bl[2] = (2 * bl[0] + bl[1]) / 3; a[2] = 255; r[3] = (r[0] + 2 * r[1]) / 3; g[3] = (g[0] + 2 * g[1]) / 3; bl[3] = (bl[0] + 2 * bl[1]) / 3; a[3] = 255; }
        else
        { r[2] = (r[0] + r[1]) / 2; g[2] = (g[0] + g[1]) / 2; bl[2] = (bl[0] + bl[1]) / 2; a[2] = 255; r[3] = g[3] = bl[3] = a[3] = 0; }
        int idx = b[4] | (b[5] << 8) | (b[6] << 16) | (b[7] << 24);
        for (var i = 0; i < 16; i++) { int s = (idx >> (i * 2)) & 3, oo = i * 4; o[oo] = (byte)r[s]; o[oo + 1] = (byte)g[s]; o[oo + 2] = (byte)bl[s]; o[oo + 3] = (byte)a[s]; }
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

    /// <summary>FR-C7 §7.1: the typed paragon projection over
    /// <c>ReadUiScene</c>. Asserts the spec §10 proven facts: CanvasRef
    /// 1920×1200, the ParagonNodes container rect, the 90°-quadrant
    /// rotation (0 at the Warlock-Start provenance, CL-10), and the
    /// staged-delivery contract (`Ratios.Provisional` = true; no pitch
    /// number asserted). Self-skips with no install.</summary>
    [SkippableFact]
    public void ReadParagonRenderLayout_decodes_proven_structure()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();

        // CanvasRef = the root ParagonBoard_main rect — confirmed by
        // BOTH the header-pinned ReadUiScene and the exploratory tool
        // (§10 decoded fact, the authoritative value).
        Assert.Equal(1920, rl.CanvasReference.Width);
        Assert.Equal(1200, rl.CanvasReference.Height);

        // The per-node element is `Template_Node_Common` (~100² ref
        // units), per the authoritative header-pinned parse (CL-14;
        // the earlier "ParagonNodes 450×1115" was an exploratory-tool
        // nearest-name mis-attribution — 450 is SidePanel_Content).
        Assert.Equal(100, rl.NodeTemplate.Width);

        // The container's own rect is runtime-bound (bindable premise,
        // §10.7) — truthfully 0, not an authored constant.
        Assert.Equal(0, rl.NodeContainer.Width);

        // CL-10: 90°-multiple quadrant only; Warlock-Start = 0
        // (45° is unrepresentable by the int type).
        Assert.InRange(rl.BoardRotationQuadrant, 0, 3);
        Assert.Equal(0, rl.BoardRotationQuadrant);

        // Gate-2: the over-determined 67.7 anchor reproduces.
        // Decode-true PitchRef = Template_Node_Common box (100) ÷
        // CanvasRef.Height (1200); DiscRef = (100 − 2×7 insets) ÷ 1200.
        // Provisional flips false (consumer dual-validated anchor ÷ the
        // 100-ref pitch converges square at ≈0.677 px/ref).
        Assert.False(rl.Ratios.Provisional);
        Assert.Equal(100.0 / 1200.0, rl.Ratios.PitchRef, 6);
        Assert.Equal(86.0 / 1200.0, rl.Ratios.DiscRef, 6);
        // Cross-check: the consumer's 67.7 px/grid at the §10.8
        // provenance ÷ the decode-true pitch (100 ref) = the implied
        // single uniform scale, consistent with their square autocorr.
        double impliedScale = 67.7 / (rl.Ratios.PitchRef * rl.CanvasReference.Height);
        Assert.InRange(impliedScale, 0.66, 0.69);

        // Refinements (decode-true): ornate + socket-ring fill the
        // 100-ref node box ⇒ ÷ disc(86) ≈ 1.163. The symbol is smaller —
        // FR-C16 R7 decodes Node_Icon's true symmetric 28-inset (the
        // tag-2-encoded rect the pre-R7 0x22-only parser could not read),
        // so the symbol is 100−(28+28)=44 ref units ÷ disc(86) ≈ 0.512
        // (the class glyph sits inside the disc with margin). The grey rim
        // ring is app-drawn (absent from scene) ⇒ 0 (the truthful answer,
        // not a gap). Per-rarity Tint / pulse AnimSpec are NOT bound (fixed
        // shader recipe / engine-driven, §2.3) ⇒ null is correct.
        Assert.Equal(100.0 / 86.0, rl.Ratios.OrnateOverDisc, 3);
        Assert.Equal(44.0 / 86.0, rl.Ratios.SymbolOverDisc, 3);
        Assert.Equal(100.0 / 86.0, rl.Ratios.SocketRingOverDisc, 3);
        Assert.Equal(0d, rl.Ratios.GreyRingOverDisc);
        Assert.All(rl.States, s => Assert.Null(s.Tint));
        Assert.All(rl.States, s => Assert.Null(s.Animation));

        // §7.5 gate 1: the §7.2 rows (FR-C12 R2 — 21 rows; was 19,
        // contract amended pre-publish to add overlay.locatedHighlight
        // and overlay.equipGlow surfaced by the broad scene 657304
        // probe — CL-34). Verbatim keys.
        var expected = new (int r, string s)[]
        {
            (0,"unselected"),(0,"selected"),(2,"unselected"),(2,"selected"),
            (3,"unselected"),(3,"selected"),(4,"unselected"),(4,"selected"),
            (-1,"socket.unselected"),(-1,"socket.selected"),(-1,"socket.socketed"),
            (-1,"gate.unselected"),(-1,"gate.selected"),
            (-1,"start.unselected"),(-1,"start.selected"),
            (-1,"overlay.selectionRing"),(-1,"overlay.connectorBar"),
            (-1,"overlay.pointerTriangle"),
            (-1,"overlay.locatedHighlight"),(-1,"overlay.equipGlow"),
            (-1,"overlay.availableGlow"),
        };
        Assert.Equal(21, rl.States.Count);
        Assert.Equal(expected,
            rl.States.Select(s => (s.RarityOverride, s.State)).ToArray());

        // CL-25 (FR-C8 R9): the disc is the base; the genuine
        // Rare/Legendary ornate is Template_Node_Rare/_Legendary's OWN
        // bound layer (Rare → 0xB71BD068), NOT NodeAvailableGlow's
        // 0x4A901508 (that is the selectable glow — its own overlay row,
        // below). FR-C7 conflated them; this is the corrected decode.
        Assert.Equal(0x1D166DC7u, rl.Disc.TextureHandle);
        var rare = rl.States.First(s => s.RarityOverride == 3 && s.State == "unselected");
        Assert.Contains(rare.Layers, e => e.TextureHandle == 0x1D166DC7u);
        Assert.Contains(rare.Layers, e => e.TextureHandle == 0xB71BD068u);
        Assert.DoesNotContain(rare.Layers, e => e.TextureHandle == 0x4A901508u);
        var common = rl.States.First(s => s.RarityOverride == 0 && s.State == "unselected");
        Assert.Contains(common.Layers, e => e.TextureHandle == 0x1D166DC7u);
        Assert.DoesNotContain(common.Layers, e => e.TextureHandle == 0xB71BD068u);
        // The selectable glow is its own overlay row, handle 0x4A901508
        // + a decoded Rect — distinct from the rarity ornate (R9 #2).
        var glow = rl.States.First(s => s.State == "overlay.availableGlow").Layers;
        Assert.Contains(glow, e => e.TextureHandle == 0x4A901508u);
        Assert.DoesNotContain(rare.Layers, e => e.TextureHandle == 0x4A901508u);
        // CL-24 (FR-C8 R6): connectorBar is NOT empty — the FR-C7-era
        // "overlays app-drawn / not in data" was wrong for the
        // connector bars too (their bound art was the dropped last
        // 0x22 record). See ReadParagonRenderLayout_decodes_directional_arrows.
        Assert.NotEmpty(rl.States.First(s => s.State == "overlay.connectorBar").Layers);
    }

    /// <summary>FR-C8: the start/gate composites ARE in ParagonBoard
    /// 657304 (Template_Node_Starter / _Quest, 0x58-block bindings the
    /// FR-C7 0x22 scan dropped). The decoded scene handles must match the
    /// consumer's owner-verified oracle exactly (CL-23). The per-node
    /// symbol on top is the ParagonNode HIconMask — correctly NOT a scene
    /// layer (already exposed via ParagonNodeDefinition).</summary>
    [SkippableFact]
    public void ReadParagonRenderLayout_decodes_start_gate_composites()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();
        uint[] H(string key) => rl.States.First(s => s.State == key)
            .Layers.Select(e => e.TextureHandle).ToArray();

        // Start: filigree 0xA0F996FE + grey hexagon 0xF8312CA8
        // (Template_Node_Starter), no disc (NOT 0x1D166DC7).
        foreach (var key in new[] { "start.unselected", "start.selected" })
        {
            Assert.Contains(0xA0F996FEu, H(key));
            Assert.Contains(0xF8312CA8u, H(key));
            Assert.DoesNotContain(0x1D166DC7u, H(key)); // not the common disc
        }

        // Gate/Exit: filigree 0xA0F996FE + ornate squares 0xC2DF4786
        // (selected) / 0x0E6B6249 (unselected) (Template_Node_Quest).
        foreach (var key in new[] { "gate.unselected", "gate.selected" })
        {
            var h = H(key);
            Assert.Contains(0xA0F996FEu, h);
            Assert.Contains(0xC2DF4786u, h);
            Assert.Contains(0x0E6B6249u, h);
            Assert.DoesNotContain(0x1D166DC7u, h);
        }

        // The symbol handles are the per-node HIconMask, NOT scene layers
        // (start spider 0x35B6E536 / gate portal 0xE1316816).
        Assert.Equal(0x35B6E536u, d4.ReadParagonNode(2458702).HIconMask);
        Assert.Equal(0xE1316816u, d4.ReadParagonNode(994337).HIconMask);
        foreach (var key in new[] { "start.unselected", "gate.unselected" })
        {
            Assert.DoesNotContain(0x35B6E536u, H(key));
            Assert.DoesNotContain(0xE1316816u, H(key));
        }

        // Lossless raw path also surfaces them (scope-B).
        var scene = d4.ReadUiScene(657304);
        var starter = scene.Widgets.First(w => w.Name == "Template_Node_Starter");
        Assert.Contains(0xA0F996FEu, starter.ExtraLayerValues);
        Assert.Contains(0xF8312CA8u, starter.ExtraLayerValues);
    }

    private static readonly uint[] ExpectedCardinalArrows =
        { 0xD51CAB25u, 0x6D3CB8DEu, 0x8EEAC178u, 0xB6D8C741u };
    private static readonly uint[] ConnectorHandles =
        { 0x77ECA3A8u, 0x288DE11Fu };

    /// <summary>The node-overlay state rows. The directional arrows
    /// (<c>Arrow_{Top,Right,Bottom,Left}</c>) bind the pre-oriented red
    /// arrow art with authored rect; the connector bars
    /// (<c>Connector_{T,R,B,L}</c>) bind the connector art with
    /// authored rect; <c>overlay.selectionRing</c> is enumerated for
    /// schema completeness but has no scene-widget binding — the
    /// selected-state red ring lives baked into each per-rarity
    /// selected composite (§10.15), not as a separate overlay — and
    /// is surfaced as <c>Unresolved = true</c> with empty
    /// <c>Layers</c>.</summary>
    [SkippableFact]
    public void ReadParagonRenderLayout_decodes_directional_arrows()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();
        var ptr = rl.States.First(s => s.State == "overlay.pointerTriangle").Layers;

        // Four cardinal arrows, decoded T/R/B/L, oracle-exact handles.
        Assert.Equal(ExpectedCardinalArrows,
            ptr.Select(e => e.TextureHandle).ToArray());

        // R6/R5: the arrow rect IS authored (unlike the start/gate 0x58
        // frame layers) — at least one non-default rect dimension.
        Assert.Contains(ptr, e =>
            e.Rect.Width != 0 || e.Rect.Height != 0 ||
            e.Rect.Left != 0 || e.Rect.Top != 0);

        // Connectors ALSO bind scene art (same dropped-last-record
        // cause, CL-24) — the catalogued connector handles.
        var conn = rl.States.First(s => s.State == "overlay.connectorBar").Layers
            .Select(e => e.TextureHandle).ToArray();
        Assert.NotEmpty(conn);
        Assert.All(conn, h => Assert.Contains(h, ConnectorHandles));

        // overlay.selectionRing is enumerated for schema completeness
        // but has no scene-widget binding — the selected-state ring is
        // baked into each per-rarity selected composite (§10.15), not
        // a separate overlay. Surfaced as Unresolved=true with empty
        // Layers.
        var selRow = rl.States.First(s => s.State == "overlay.selectionRing");
        Assert.Empty(selRow.Layers);
        Assert.True(selRow.Unresolved);

        // R5 (definitive): the start/gate 0x58 frame-layer blocks are
        // handle-only — no per-layer rect authored (engine/template-
        // inherited, sized to NodeTemplate). Honest default, not eyeballed.
        var startL = rl.States.First(s => s.State == "start.unselected").Layers;
        Assert.All(startL, e => Assert.Equal(default, e.Rect));
        Assert.True(rl.NodeTemplate.Width > 0); // the size hint for those frames
    }

    /// <summary>FR-D1: a ParagonBoard's localized display name resolves
    /// first-party from the board's sibling StringList table
    /// (<c>ParagonBoard_&lt;boardSnoName&gt;</c>, label <c>Name</c>) — the
    /// verbatim acceptance probes (§6.4 / CL-15).</summary>
    [SkippableFact]
    public void ReadParagonBoardName_resolves_localized_board_name()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        // Probe 1: Paragon_Warlock_00 (SnoId 2458674, IsStart) → "Start".
        Assert.Equal("Paragon_Warlock_00",
            d4.CoreToc.GetName(SnoGroup.ParagonBoard, 2458674));
        Assert.True(d4.TryReadParagonBoardName(2458674, out var start));
        Assert.Equal("Start", start);
        Assert.Equal("Start", d4.ReadParagonBoardName(2458674));

        // Probe 2: a non-start Warlock board (Paragon_Warlock_03,
        // SnoId 2458680) → its distinct in-game name.
        Assert.Equal("Dynamism", d4.ReadParagonBoardName(2458680));

        // Locale-aware: the same board resolves a different localized
        // string (no English baked in).
        Assert.Equal("Dynamismus", d4.ReadParagonBoardName(2458680, "deDE"));

        // The convention is name-keyed (no fixed SNO offset): it holds
        // for another class whose StringList id is NOT board−1
        // (Paragon_Sorc_04 939... → ParagonBoard_Paragon_Sorc_04).
        var sorc04 = d4.CoreToc.GetId(SnoGroup.ParagonBoard, "Paragon_Sorc_04");
        Assert.NotNull(sorc04);
        Assert.True(d4.TryReadParagonBoardName(sorc04!.Value, out var sn));
        Assert.False(string.IsNullOrEmpty(sn));

        // No fallback policy is baked in: an unknown board SNO yields
        // false / throws (the consumer owns the SnoName fallback).
        Assert.False(d4.TryReadParagonBoardName(1, out var none));
        Assert.Equal(string.Empty, none);
        Assert.Throws<SnoNotFoundException>(() => d4.ReadParagonBoardName(1));
    }

    /// <summary>FR-D1 (rescoped): typed board class + index resolved
    /// library-side from the SNO-name convention — the consumer never
    /// parses the SnoName (§6.6 / CL-16).</summary>
    [SkippableFact]
    public void ReadParagonBoard_resolves_typed_class_and_index()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        // Probe 1: Paragon_Warlock_00 (2458674, IsStart) → class Warlock
        // (the group-74 PlayerClass SNO, a stable id), index 0.
        var warlockClass = d4.CoreToc.GetId(SnoGroup.PlayerClass, "Warlock");
        Assert.NotNull(warlockClass);
        var b0 = d4.ReadParagonBoard(2458674);
        Assert.Equal(warlockClass!.Value, b0.ClassSnoId);
        Assert.Equal("Warlock", b0.ClassSnoName);
        Assert.Equal(0, b0.BoardIndex);

        // Probe 2: Paragon_Warlock_03 (2458680) → Warlock, index 3 —
        // without the consumer parsing the SnoName.
        var b3 = d4.ReadParagonBoard(2458680);
        Assert.Equal(warlockClass.Value, b3.ClassSnoId);
        Assert.Equal("Warlock", b3.ClassSnoName);
        Assert.Equal(3, b3.BoardIndex);

        // Abbreviated token resolves by unique-prefix to the roster name:
        // Sorc→Sorcerer, Spirit→Spiritborn (and the single-digit start
        // board Paragon_Spirit_0 → index 0).
        var sorc = d4.CoreToc.GetId(SnoGroup.ParagonBoard, "Paragon_Sorc_04")!.Value;
        var bs = d4.ReadParagonBoard(sorc);
        Assert.Equal("Sorcerer", bs.ClassSnoName);
        Assert.Equal(4, bs.BoardIndex);
        var spirit0 = d4.CoreToc.GetId(SnoGroup.ParagonBoard, "Paragon_Spirit_0")!.Value;
        var bsp = d4.ReadParagonBoard(spirit0);
        Assert.Equal("Spiritborn", bsp.ClassSnoName);
        Assert.Equal(0, bsp.BoardIndex);

        // ClassSnoId matches the FR-D2 roster (same stable key).
        var roster = d4.ReadCharacterClasses();
        Assert.Contains(roster, c => c.SnoId == b0.ClassSnoId && c.SnoName == "Warlock");

        // The byte-only Parse leaves identity unresolved (honest sentinels;
        // identity derives from the SNO name, not the bytes).
        var byteOnly = ParagonBoardDefinition.Parse(
            d4.ReadSno(SnoGroup.ParagonBoard, 2458674));
        Assert.Equal(0, byteOnly.ClassSnoId);
        Assert.Equal(string.Empty, byteOnly.ClassSnoName);
        Assert.Equal(-1, byteOnly.BoardIndex);
    }

    private static readonly string[] ExpectedClassRoster =
        { "Barbarian", "Druid", "Necromancer", "Paladin",
          "Rogue", "Sorcerer", "Spiritborn", "Warlock" };

    /// <summary>FR-D2: first-party character-class roster + localized
    /// names from D4's own class data (group 74 + General table), with
    /// no dependency on the paragon SNO groups (§6.5 / CL-17).</summary>
    [SkippableFact]
    public void ReadCharacterClasses_returns_first_party_roster()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var classes = d4.ReadCharacterClasses();

        // The latest classes are present with their exact localized names.
        Assert.Contains(classes, c => c.SnoName == "Warlock"    && c.DisplayName == "Warlock");
        Assert.Contains(classes, c => c.SnoName == "Paladin"    && c.DisplayName == "Paladin");
        Assert.Contains(classes, c => c.SnoName == "Spiritborn" && c.DisplayName == "Spiritborn");

        // Full current-build roster = exactly the eight playable classes;
        // the non-class junk entry (Axe Bad Data) is filtered data-driven.
        var names = classes.Select(c => c.SnoName).OrderBy(s => s).ToArray();
        Assert.Equal(ExpectedClassRoster, names);
        Assert.DoesNotContain(classes, c => c.SnoName.Contains("Bad Data"));

        // Stable per-class key = the PlayerClass SNO id (not array
        // position); matches CoreTOC group 74.
        var warlock = classes.First(c => c.SnoName == "Warlock");
        Assert.Equal(d4.CoreToc.GetId(SnoGroup.PlayerClass, "Warlock"), warlock.SnoId);

        // Locale-aware; deterministic (cached → same instance).
        Assert.Same(classes, d4.ReadCharacterClasses());
        var de = d4.ReadCharacterClasses("deDE");
        Assert.Equal(classes.Count, de.Count);
        Assert.Contains(de, c => c.SnoName == "Sorcerer");
    }

    /// <summary>FR-D3: glyph→class membership is first-party, keyed to
    /// the shared PlayerClass SNO id, decoded from the fUsableByClass
    /// fixed array indexed by eClass rank (§7.3 / CL-18).</summary>
    [SkippableFact]
    public void ReadParagonGlyph_resolves_usable_by_class()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var sorcerer    = d4.CoreToc.GetId(SnoGroup.PlayerClass, "Sorcerer")!.Value;
        var druid       = d4.CoreToc.GetId(SnoGroup.PlayerClass, "Druid")!.Value;
        var necromancer = d4.CoreToc.GetId(SnoGroup.PlayerClass, "Necromancer")!.Value;
        var spiritborn  = d4.CoreToc.GetId(SnoGroup.PlayerClass, "Spiritborn")!.Value;
        var paladin     = d4.CoreToc.GetId(SnoGroup.PlayerClass, "Paladin")!.Value;
        const int warlock = 2207749; // FR-D1/D2 shared key (acceptance probe)
        Assert.Equal(warlock, d4.CoreToc.GetId(SnoGroup.PlayerClass, "Warlock"));

        // Warlock-usable glyph (slot = eClass rank 7) → includes 2207749.
        var wlk = d4.ReadParagonGlyph(2529463); // Rare_111_Willpower_Main
        Assert.Contains(warlock, wlk.UsableByClassSnoIds);

        // Single-class Sorcerer glyph (slot 0) → Sorcerer only, excludes Warlock.
        var sor = d4.ReadParagonGlyph(1023184); // Rare_001_Intelligence_Main
        Assert.Equal(new[] { sorcerer }, sor.UsableByClassSnoIds);
        Assert.DoesNotContain(warlock, sor.UsableByClassSnoIds);

        // Independent first-party anchor: an explicitly Necromancer glyph
        // (eClass rank 4) → Necromancer.
        var nec = d4.ReadParagonGlyph(1331846); // Rare_033_Willpower_Side_Necro
        Assert.Equal(new[] { necromancer }, nec.UsableByClassSnoIds);

        // Multi-class glyph → the full correct set (slots 3/5/6 =
        // Druid / Spiritborn / Paladin by eClass rank).
        var multi = d4.ReadParagonGlyph(1029487); // Rare_063_Intelligence_Side
        Assert.Equal(
            new[] { druid, spiritborn, paladin }.OrderBy(x => x).ToArray(),
            multi.UsableByClassSnoIds.OrderBy(x => x).ToArray());

        // Junk/placeholder ("Axe Bad Data", malformed record) → honest
        // empty sentinel, never a silently-wrong (all-class) set.
        var junk = d4.ReadParagonGlyph(732443);
        Assert.Empty(junk.UsableByClassSnoIds);

        // Byte-only Parse leaves it unresolved (identity needs CoreTOC).
        var byteOnly = ParagonGlyphDefinition.Parse(
            d4.ReadSno(SnoGroup.ParagonGlyph, 2529463));
        Assert.Empty(byteOnly.UsableByClassSnoIds);

        // Shared key really joins to FR-D1/D2.
        Assert.Contains(d4.ReadCharacterClasses(),
            c => c.SnoId == warlock && c.SnoName == "Warlock");
    }

    /// <summary>C6 (scope-unfrozen 2026-05-17): typed PlayerClass / Power
    /// / Affix / Item readers — identity + localized text via the
    /// generalized sibling-StringList convention (§8 / CL-20..22).</summary>
    [SkippableFact]
    public void C6_typed_readers_decode_identity_and_localized_text()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        // PlayerClass (group 74): SnoId + binary eClass (§8.1 / CL-21).
        var warlock = d4.ReadPlayerClass(2207749);
        Assert.Equal(2207749, warlock.SnoId);
        Assert.Equal(10, warlock.EClass);
        Assert.Equal(0, d4.ReadPlayerClass(131965).EClass);   // Sorcerer
        Assert.Equal(6, d4.ReadPlayerClass(199277).EClass);   // Necromancer

        // Power (group 29): sibling Power_<snoName> labels name/desc.
        var pow = d4.ReadPower(2521393);    // Paragon_Warlock_Legendary_001
        Assert.Equal(2521393, pow.SnoId);
        Assert.Equal("Fathomless", pow.Name);
        Assert.StartsWith("Each demon", pow.Description);

        // Affix (group 104): sibling Affix_<snoName> label Desc.
        var aff = d4.ReadAffix(2586362);    // Talisman_Charm_Affix_1HAxe_Unique_Generic_001
        Assert.Equal(2586362, aff.SnoId);
        Assert.StartsWith("Your attacks Critically Strike", aff.Description);

        // Item (group 73): sibling Item_<snoName> Name/Flavor/TransmogName.
        var item = d4.ReadItem(223287);     // 1HAxe_Unique_Generic_001
        Assert.Equal(223287, item.SnoId);
        Assert.Equal("The Butcher's Cleaver", item.Name);
        Assert.Equal("Cadaver Chopper", item.TransmogName);
        Assert.False(string.IsNullOrEmpty(item.Flavor));

        // Locale-aware (sibling catalog is per-locale).
        Assert.NotEqual(pow.Name, d4.ReadPower(2521393, "deDE").Name);

        // Byte-only Parse = identity only (localized fields need CoreTOC)
        // — honest empty sentinel, no fabrication.
        var bo = PowerDefinition.Parse(d4.ReadSno(SnoGroup.Power, 2521393));
        Assert.Equal(2521393, bo.SnoId);
        Assert.Equal(string.Empty, bo.Name);
        Assert.Equal(string.Empty,
            ItemDefinition.Parse(d4.ReadSno(SnoGroup.Item, 223287)).Name);

        // C6 eClass matches the FR-D2/D3 shared roster key.
        Assert.Equal(
            d4.ReadCharacterClasses().First(c => c.SnoName == "Warlock").SnoId,
            warlock.SnoId);
    }

    /// <summary>FR-C13 Phase 1 — Power Script Formula slot table decode.
    /// For each of the 9 Warlock Legendary anchor powers, assert the
    /// decoded slot table matches the engine SF_N values established by
    /// owner game-vs-app oracle (R2 + R3 confirmations) — i.e. the
    /// positional values that resolve <c>[SF_<i>n</i>...]</c> placeholders
    /// in <see cref="PowerDefinition.Description"/>. Slots whose Text is
    /// a numeric literal expose <see cref="PowerScriptFormula.LiteralValue"/>;
    /// slots whose Text is an arithmetic expression (Demonic Spicules's
    /// <c>"SF_1 / 3"</c>) carry the raw text and Phase 2's evaluator will
    /// resolve them.</summary>
    [SkippableFact]
    public void PowerDefinition_decodes_script_formulas_for_anchored_legendaries()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        static PowerScriptFormula F(int idx, string text, float val) =>
            new PowerScriptFormula(idx, text, val);

        // The 8 layout-A-clean powers (the slot table is the last
        // 16-byte run terminated by ("0",0.0) with the "10" sentinel
        // stripped). Engine SF_N values per the format-string indices
        // (R3 confirmation 2026-05-20).
        (int Sno, PowerScriptFormula[] Expected)[] anchors =
        {
            // Pyrosis: [SF_0*100|%x|] = "450%[x]" → SF_0=4.5.
            (2527268, new[] { F(0, "4.5", 4.5f) }),

            // Fathomless: [SF_2] 6s, [SF_0*100] 15%[x], [SF_0*SF_1*100] 105[x]% cap.
            //   Stored slots: SF_0=0.15, SF_1=7 (max-stacks), SF_2=6.
            (2521393, new[] {
                F(0, ".15", 0.15f),
                F(1, "7", 7.0f),
                F(2, "6", 6.0f),
            }),

            // Overmind: [SF_0*100] 45%[x] CC, [SF_1*100] 65%[x] Elite. IEEE-754
            //   round-to-nearest in storage (1-bit higher than the canonical
            //   0.45/0.65 representations the owner first relayed in R2).
            (2524552, new[] {
                F(0, ".45", 0.45000002f),
                F(1, ".65", 0.65000004f),
            }),

            // Ritualism: [SF_0*100] 90%[x], [SF_2] 15s, [1+SF_1] 10 kills
            //   (engine evaluates 1+9 = 10; SF_1 stored as raw 9).
            (2526168, new[] {
                F(0, ".9", 0.9f),
                F(1, "9", 9.0f),
                F(2, "15", 15.0f),
            }),

            // Chaos: [SF_0*100|%x|] 100%[x], [SF_1] 2 stacks, [SF_2] 1 stack.
            (2527294, new[] {
                F(0, "1", 1.0f),
                F(1, "2", 2.0f),
                F(2, "1", 1.0f),
            }),

            // Dominion: [SF_1*100|%|] 50% cost cut, [SF_0*100|%x|] 80%[x] dmg,
            //   {SF_2} 12s. Engine indices: SF_0=damage, SF_1=cost, SF_2=duration.
            (2524673, new[] {
                F(0, "0.8", 0.8f),
                F(1, "0.5", 0.5f),
                F(2, "12", 12.0f),
            }),

            // Dynamism: [SF_0*100] 3%[x], [SF_2] 1 Dominance, [SF_3] 2s.
            //   The format string SKIPS SF_1 (engine has a 4-slot table
            //   with slot[1] = 1.0 unused).
            (2524312, new[] {
                F(0, ".03", 0.03f),
                F(1, "1", 1.0f),
                F(2, "1", 1.0f),
                F(3, "2", 2.0f),
            }),
        };

        foreach (var (sno, expected) in anchors)
        {
            var pow = d4.ReadPower(sno);
            Assert.NotNull(pow.ScriptFormulas);
            Assert.True(pow.ScriptFormulas.Count >= expected.Length,
                $"Power {sno} ({pow.Name}): expected at least {expected.Length} " +
                $"slots, got {pow.ScriptFormulas.Count}");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].Index, pow.ScriptFormulas[i].Index);
                Assert.Equal(expected[i].Text, pow.ScriptFormulas[i].Text);
                Assert.Equal(expected[i].LiteralValue, pow.ScriptFormulas[i].LiteralValue);
                Assert.False(pow.ScriptFormulas[i].IsExpression,
                    $"Power {sno} slot {i} text \"{pow.ScriptFormulas[i].Text}\" classified as expression");
            }
        }

        // Demonic Spicules (SNO 2525006) + Greater Hex (SNO 2527280) —
        // anchors whose stored slot table uses an alternate layout
        // (4-character ASCII chunks like "0.02"/"0.75"/"0.25") that
        // Phase 1's Layout-A-only decoder doesn't yet handle. Phase 2
        // will lift the disambiguating layouts. For now: no-crash
        // assertion only — the decoder returns either an empty list or
        // a partial table; no fabrication.
        var spicules = d4.ReadPower(2525006);
        Assert.NotNull(spicules.ScriptFormulas);
        var ghex = d4.ReadPower(2527280);
        Assert.NotNull(ghex.ScriptFormulas);
    }

    /// <summary>FR-C13 Phase 2 — extended slot decoder (Layout A + B) +
    /// resolved <c>SF_N → value</c> dictionary + engine-function ref
    /// surfacing. Per the R4 sign-off (2026-05-20), Phase 2 lifts the
    /// Greater Hex / Demonic Spicules slots that Phase 1 deferred and
    /// resolves expression-text slots (Demonic Spicules's
    /// <c>"SF_1 / 3"</c>) through the recursive-descent evaluator.
    /// FunctionRefs surface from format-string scanning of the
    /// Description (Barbarian Warbringer's <c>[SF_1 * PlayerHealthMax()]</c>
    /// is the canonical anchor).</summary>
    [SkippableFact]
    public void PowerDefinition_resolves_phase2_formulas_and_function_refs()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        // Greater Hex (SNO 2527280) — Phase 1 returned empty (Layout B
        // unsupported); Phase 2 must surface the 2-slot table.
        var ghex = d4.ReadPower(2527280);
        Assert.True(ghex.ScriptFormulas.Count >= 2,
            $"Greater Hex expected ≥2 slots; got {ghex.ScriptFormulas.Count}");
        Assert.Equal("0.75", ghex.ScriptFormulas[0].Text);
        Assert.Equal(0.75f, ghex.ScriptFormulas[0].LiteralValue);
        Assert.Equal("0.25", ghex.ScriptFormulas[1].Text);
        Assert.Equal(0.25f, ghex.ScriptFormulas[1].LiteralValue);
        Assert.True(ghex.ResolvedFormulas.ContainsKey("SF_0"));
        Assert.Equal(0.75, ghex.ResolvedFormulas["SF_0"], 4);
        Assert.Equal(0.25, ghex.ResolvedFormulas["SF_1"], 4);

        // ResolvedFormulas across the layout-A-clean anchors — keys are
        // SF_0/SF_1/.../SF_N positional; values are the raw slot doubles
        // (no expression evaluation needed). Pyrosis (1 slot), Dominion
        // (3 slots), Ritualism (3 slots).
        var pyrosis = d4.ReadPower(2527268);
        Assert.Equal(4.5, pyrosis.ResolvedFormulas["SF_0"], 4);
        var dominion = d4.ReadPower(2524673);
        Assert.Equal(0.8, dominion.ResolvedFormulas["SF_0"], 4);
        Assert.Equal(0.5, dominion.ResolvedFormulas["SF_1"], 4);
        Assert.Equal(12.0, dominion.ResolvedFormulas["SF_2"], 4);
        var ritualism = d4.ReadPower(2526168);
        Assert.Equal(0.9, ritualism.ResolvedFormulas["SF_0"], 4);
        Assert.Equal(9.0, ritualism.ResolvedFormulas["SF_1"], 4);
        Assert.Equal(15.0, ritualism.ResolvedFormulas["SF_2"], 4);

        // Fathomless: stored slots [.15, 7, 6]; ResolvedFormulas
        // surfaces them raw. The format-string-rendered cap value (1.05
        // = SF_0 × SF_1) is the consumer's tooltip-eval concern, NOT
        // a ResolvedFormulas value (the dictionary keys SF_N to raw
        // slot evaluation, not to per-rendered-expression values).
        var fathomless = d4.ReadPower(2521393);
        Assert.Equal(0.15, fathomless.ResolvedFormulas["SF_0"], 4);
        Assert.Equal(7.0, fathomless.ResolvedFormulas["SF_1"], 4);
        Assert.Equal(6.0, fathomless.ResolvedFormulas["SF_2"], 4);

        // Overmind: stored slots [.45, .65] (IEEE-754 round-to-nearest,
        // one ULP higher than 0.45/0.65 canonical reps).
        var overmind = d4.ReadPower(2524552);
        Assert.Equal(0.45, overmind.ResolvedFormulas["SF_0"], 3);
        Assert.Equal(0.65, overmind.ResolvedFormulas["SF_1"], 3);

        // Chaos: stored slots [1, 2, 1].
        var chaos = d4.ReadPower(2527294);
        Assert.Equal(1.0, chaos.ResolvedFormulas["SF_0"], 4);
        Assert.Equal(2.0, chaos.ResolvedFormulas["SF_1"], 4);
        Assert.Equal(1.0, chaos.ResolvedFormulas["SF_2"], 4);

        // Dynamism: 4 slots [0.03, 1, 1, 2] — engine format string uses
        // SF_0, SF_2, SF_3 (skips SF_1; slot 1 = 1 is unused).
        var dynamism = d4.ReadPower(2524312);
        Assert.Equal(0.03, dynamism.ResolvedFormulas["SF_0"], 4);
        Assert.Equal(1.0, dynamism.ResolvedFormulas["SF_2"], 4);
        Assert.Equal(2.0, dynamism.ResolvedFormulas["SF_3"], 4);

        // Demonic Spicules: tail-data layout has an expression-text
        // record ("SF_1 / 3" for SF_2 = 60/3 = 20) interleaved with
        // trivial slots. The "SF_1 / 3" record uses a NON-16-byte
        // structure the Phase 2 decoder doesn't yet lift, and the
        // terminator pattern is non-standard for this power. The
        // ResolvedFormulas may be empty — Phase 3 will RE the
        // expression-text record format (per d4parse
        // DT_STRING_FORMULA model). Until then this anchor remains
        // pending; the no-crash sweep still covers it.
        var spicules = d4.ReadPower(2525006);
        Assert.NotNull(spicules.ResolvedFormulas);

        // Barbarian Warbringer (SNO 664973) — format string contains
        // [SF_1 * PlayerHealthMax()]; FunctionRefs must surface the
        // PlayerHealthMax engine-function reference.
        var warbringer = d4.ReadPower(664973);
        Assert.NotNull(warbringer.FunctionRefs);
        Assert.Contains(warbringer.FunctionRefs,
            fr => fr.Name == "PlayerHealthMax");
    }

    /// <summary>FR-C13 Phase 3 — compiled-form AST decode + cross-
    /// validation gate (R5 regression gate). For each of the 9 Warlock
    /// legendary anchors, assert that every entry in
    /// <see cref="PowerDefinition.ResolvedFormulas"/> agrees with the
    /// corresponding entry in <see cref="PowerDefinition.CompiledFormulas"/>
    /// to float precision. Phase 2 derives the resolved value from the
    /// slot's TEXT (e.g. evaluating "SF_1 / 3" via the recursive-descent
    /// parser); Phase 3 derives it from the BINARY compiled record
    /// (literal slots: IEEE-754 single read directly; expression slots:
    /// operator from text + embedded literal operand read from the AST
    /// opcode region's binary bytes). Disagreement between the two
    /// flags a text-vs-binary inconsistency in the engine-compiled
    /// record — the exact regression the FR-C13 R5 gate is designed
    /// to catch. Demonic Spicules is the load-bearing anchor: its
    /// <c>SF_2</c> goes through the binary literal path
    /// (binary 3.0f → 60 / 3 = 20), exercising the type=0x05
    /// expression-record decoder added in Phase 3.</summary>
    [SkippableFact]
    public void PowerDefinition_phase3_compiled_formulas_match_resolved_for_9_warlock_anchors()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        (int Sno, string Label)[] anchors =
        {
            (2527268, "Pyrosis"),
            (2521393, "Fathomless"),
            (2524552, "Overmind"),
            (2526168, "Ritualism"),
            (2527294, "Chaos"),
            (2524673, "Dominion"),
            (2524312, "Dynamism"),
            (2527280, "Greater Hex"),
            (2525006, "Demonic Spicules"),
        };

        var mismatches = new List<string>();
        foreach (var (sno, label) in anchors)
        {
            var pow = d4.ReadPower(sno);
            Assert.True(pow.ResolvedFormulas.Count > 0,
                $"{label} (SNO {sno}): ResolvedFormulas empty — Phase 2/3 decoder did not surface slots");
            Assert.Equal(pow.ResolvedFormulas.Count, pow.CompiledFormulas.Count);

            foreach (var kv in pow.ResolvedFormulas)
            {
                if (!pow.CompiledFormulas.TryGetValue(kv.Key, out var compiledValue))
                {
                    mismatches.Add($"{label} {kv.Key}: present in Resolved but missing from Compiled");
                    continue;
                }
                if (double.IsNaN(kv.Value) && double.IsNaN(compiledValue)) continue;
                if (Math.Abs(kv.Value - compiledValue) >= 1e-4)
                    mismatches.Add($"{label} {kv.Key}: Resolved={kv.Value:R} Compiled={compiledValue:R}");
            }
        }
        Assert.True(mismatches.Count == 0,
            "Phase 2 ↔ Phase 3 cross-validation mismatches:\n  " +
            string.Join("\n  ", mismatches));
    }

    /// <summary>FR-C13 Phase 3 — Demonic Spicules's
    /// <c>SF_2 = "SF_1 / 3"</c> is the canonical expression-record
    /// anchor (the only Warlock legendary with an expression-text slot
    /// rather than a plain literal). Phase 1/2 returned an empty slot
    /// list for this power because the 48-byte type=0x05 expression
    /// record between the literal slots and the trailing sentinels
    /// halted the backward-walk decoder. Phase 3 adds the
    /// expression-record reader and the 52-byte backward stride so
    /// the decoder returns 3 slots: SF_0 = 0.02 (Layout B literal),
    /// SF_1 = 60 (Layout C literal), SF_2 = "SF_1 / 3" (type=0x05
    /// expression with embedded literal 3.0f). The resolver chain
    /// evaluates SF_2 to 60 / 3 = 20.</summary>
    [SkippableFact]
    public void PowerDefinition_phase3_decodes_demonic_spicules_expression_slot()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var pow = d4.ReadPower(2525006);
        Assert.Equal(3, pow.ScriptFormulas.Count);

        // SF_0 "0.02" — Layout B literal.
        Assert.Equal(0, pow.ScriptFormulas[0].Index);
        Assert.Equal("0.02", pow.ScriptFormulas[0].Text);
        Assert.Equal(0.02f, pow.ScriptFormulas[0].LiteralValue);
        Assert.False(pow.ScriptFormulas[0].IsExpression);

        // SF_1 "60" — Layout C literal.
        Assert.Equal(1, pow.ScriptFormulas[1].Index);
        Assert.Equal("60", pow.ScriptFormulas[1].Text);
        Assert.Equal(60f, pow.ScriptFormulas[1].LiteralValue);
        Assert.False(pow.ScriptFormulas[1].IsExpression);

        // SF_2 "SF_1 / 3" — 48-byte type=0x05 expression record. The
        // record's LiteralValue is NaN (expression — text-eval needed);
        // ResolvedFormulas and CompiledFormulas both produce 60/3 = 20.
        Assert.Equal(2, pow.ScriptFormulas[2].Index);
        Assert.Equal("SF_1 / 3", pow.ScriptFormulas[2].Text);
        Assert.True(float.IsNaN(pow.ScriptFormulas[2].LiteralValue));
        Assert.True(pow.ScriptFormulas[2].IsExpression);

        // Both resolution paths produce SF_2 = 20:
        //   Phase 2 (text): "SF_1 / 3" parsed → 60 / 3 = 20
        //   Phase 3 (binary): operator "/" from text, operand 3.0f from
        //                     compiled record bytes at +40, → 60 / 3.0 = 20
        Assert.Equal(0.02, pow.ResolvedFormulas["SF_0"], 4);
        Assert.Equal(60.0, pow.ResolvedFormulas["SF_1"], 4);
        Assert.Equal(20.0, pow.ResolvedFormulas["SF_2"], 4);
        Assert.Equal(0.02, pow.CompiledFormulas["SF_0"], 4);
        Assert.Equal(60.0, pow.CompiledFormulas["SF_1"], 4);
        Assert.Equal(20.0, pow.CompiledFormulas["SF_2"], 4);
    }

    /// <summary>FR-C13 Phase 1 — no-crash sweep across all 72 legendary
    /// node Powers (8 classes × ~9 each). The decoder must not throw on
    /// any legendary's blob; expected counts vary per power (some have
    /// no slot table, some have many). This is the "honest decode under
    /// any shape" assertion — parallel to the FR-C9 coverage gate's
    /// shape-agnostic discipline.</summary>
    [SkippableFact]
    public void PowerDefinition_decodes_script_formulas_for_all_legendaries_no_crash()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var legendaries = d4.CoreToc.EntriesInGroup(SnoGroup.ParagonNode)
            .Where(e => e.Name.Contains("Legendary", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(legendaries.Count >= 70,
            $"expected ~72 legendary nodes, found {legendaries.Count}");

        var failures = new List<string>();
        foreach (var entry in legendaries)
        {
            try
            {
                var node = d4.ReadParagonNode(entry.Id);
                if (node.SnoPassivePower == 0 ||
                    (uint)node.SnoPassivePower == 0xFFFFFFFF) continue;
                var pow = d4.ReadPower(node.SnoPassivePower);
                _ = pow.ScriptFormulas;             // exercise the surface
                _ = pow.ScriptFormulas.Count;
                foreach (var sf in pow.ScriptFormulas)
                {
                    _ = sf.Index;
                    _ = sf.Text;
                    _ = sf.LiteralValue;
                    _ = sf.IsExpression;
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{entry.Name} (SNO {entry.Id}): {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0,
            "decoder crashed on " + failures.Count + " legendaries: " +
            string.Join("; ", failures.Take(5)) +
            (failures.Count > 5 ? $" (+{failures.Count - 5})" : ""));
    }

    /// <summary>FR-C9 #2 — the coverage gate (the decisive part).
    /// Shape-agnostic: every handle-magnitude u32 anywhere in the raw
    /// paragon scenes that resolves to a real atlas frame MUST be
    /// surfaced by the exhaustive render-model. A future binding shape
    /// that drops a real handle fails casc's own CI here — not the
    /// consumer's eyeballs months later (the CL-23/24/25/26 lesson made
    /// structural). Includes the canonical previously-dropped handles
    /// (e.g. the grey rim ring 0x87A89F86, FR-C7-era "not in data").</summary>
    [SkippableFact]
    public void ParagonRenderModel_covers_every_bound_atlas_handle()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var model = d4.ReadParagonRenderModel();
        Assert.Equal(new[] { 657304, 964599 },
            model.Scenes.Select(s => s.SnoId).ToArray());
        Assert.NotNull(model.Layout);

        foreach (var sno in new[] { 657304, 964599 })
        {
            var blob = d4.ReadSno(UiScene.Group, sno);
            // Structural texture-binding set: every 4-aligned, handle-
            // magnitude u32 in the raw scene that resolves to an atlas
            // frame (shape-agnostic — not tied to 0x22 / 0x58).
            var rawHandles = new HashSet<uint>();
            for (var p = 0; p + 4 <= blob.Length; p += 4)
            {
                var v = (uint)(blob[p] | (blob[p + 1] << 8) |
                               (blob[p + 2] << 16) | (blob[p + 3] << 24));
                if (d4.IsParagonTextureHandle(v)) rawHandles.Add(v);
            }

            var modelHandles = model.Scenes.First(s => s.SnoId == sno)
                .Widgets.SelectMany(w => w.Layers)
                .Select(e => e.TextureHandle).ToHashSet();

            var dropped = rawHandles.Where(h => !modelHandles.Contains(h))
                .Select(h => $"0x{h:X8}").ToArray();
            Assert.True(dropped.Length == 0,
                $"scene {sno}: render-model dropped bound atlas handles: " +
                string.Join(", ", dropped));
            Assert.NotEmpty(rawHandles); // sanity: the gate actually ran
        }

        // The exhaustive model carries handle + decoded rect; the grey
        // rim ring (0x87A89F86 — CL-26, the FR-C7 "not in data" miss)
        // is now present with its widget rect.
        var all = model.Scenes.SelectMany(s => s.Widgets)
            .SelectMany(w => w.Layers).ToArray();
        Assert.Contains(all, e => e.TextureHandle == 0x87A89F86u);
        Assert.Contains(all, e => e.Rect.Width != 0 || e.Rect.Height != 0 ||
                                  e.Rect.Left != 0 || e.Rect.Top != 0);
    }

    /// <summary>The paragon node composite recipe (§10.15). Per
    /// (rarity × state) the layers compose the shared grey base disc
    /// (<c>0x1D166DC7</c>) + (rarity-specific interior fill, when the
    /// rarity template's 0x58 block binds one) + (the rarity's
    /// selected-state composite when <c>state == "selected"</c>):
    /// <c>0x72C29402</c> for Magic / <c>0x03EDABAB</c> for Rare /
    /// <c>0xBD27FB7C</c> for Legendary (all bound on
    /// <c>Template_Node_&lt;rarity&gt;</c>'s 0x58 block), or
    /// <c>0xD3051CCA</c> on the separate <c>Node_Purchased</c> widget
    /// for Common. Each surfaced <see cref="NodeElement"/> carries its
    /// atlas SNO and native pixel size.</summary>
    [SkippableFact]
    public void ReadParagonRenderLayout_decodes_node_composite_recipe()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();
        StateElements Row(int r, string s) =>
            rl.States.First(x => x.RarityOverride == r && x.State == s);

        // Common (r0): just the grey base disc when unselected; selected
        // swaps to Node_Purchased's 0xD3051CCA (153² dark disc + perimeter
        // ring composite — scene-bound, NOT the standalone engine-internal
        // ring which would draw a small centred ring instead).
        var commonUn = Row(0, "unselected").Layers;
        Assert.Single(commonUn);
        Assert.Equal(0x1D166DC7u, commonUn[0].TextureHandle);

        var commonSel = Row(0, "selected").Layers;
        Assert.Equal(2, commonSel.Count);
        Assert.Equal(0x1D166DC7u, commonSel[0].TextureHandle);
        Assert.Equal(0xD3051CCAu, commonSel[1].TextureHandle);

        // Magic (r2): grey base + 0x621CB6FF (153² magic base composite,
        // FR-C12 R2 — Template_Node_Magic's first 0x58-block layer
        // previously dropped by the curated row) + 0xFEC31E48 (135²
        // blue interior fill); selected adds 0x72C29402 (Template_Node_
        // Magic's 0x58-block 154² blue disc + perimeter ring composite).
        var magicUn = Row(2, "unselected").Layers;
        Assert.Equal(3, magicUn.Count);
        Assert.Equal(0x1D166DC7u, magicUn[0].TextureHandle);
        Assert.Equal(0x621CB6FFu, magicUn[1].TextureHandle);
        Assert.Equal(0xFEC31E48u, magicUn[2].TextureHandle);

        var magicSel = Row(2, "selected").Layers;
        Assert.Equal(4, magicSel.Count);
        Assert.Equal(0x621CB6FFu, magicSel[1].TextureHandle);
        Assert.Equal(0xFEC31E48u, magicSel[2].TextureHandle);
        Assert.Equal(0x72C29402u, magicSel[3].TextureHandle);

        // Rare (r3): grey base + 0xF8373491 (interior fill) +
        // 0xB71BD068 (yellow ornate, unselected) → swap ornate to
        // 0x03EDABAB on selected (yellow ornate + red perimeter ring
        // composite — the ring is baked into the selected-variant).
        var rareUn = Row(3, "unselected").Layers;
        Assert.Contains(rareUn, e => e.TextureHandle == 0x1D166DC7u);
        Assert.Contains(rareUn, e => e.TextureHandle == 0xF8373491u);
        Assert.Contains(rareUn, e => e.TextureHandle == 0xB71BD068u);
        Assert.DoesNotContain(rareUn, e => e.TextureHandle == 0x03EDABABu);

        var rareSel = Row(3, "selected").Layers;
        Assert.Contains(rareSel, e => e.TextureHandle == 0x03EDABABu);
        Assert.DoesNotContain(rareSel, e => e.TextureHandle == 0xB71BD068u);

        // Legendary (r4): grey base + 0x006ED182 (interior fill) +
        // 0x232DF7F9 (orange spike ornate) → swap ornate to 0xBD27FB7C
        // on selected (orange ornate + red ring composite).
        var legUn = Row(4, "unselected").Layers;
        Assert.Contains(legUn, e => e.TextureHandle == 0x1D166DC7u);
        Assert.Contains(legUn, e => e.TextureHandle == 0x006ED182u);
        Assert.Contains(legUn, e => e.TextureHandle == 0x232DF7F9u);
        Assert.DoesNotContain(legUn, e => e.TextureHandle == 0xBD27FB7Cu);

        var legSel = Row(4, "selected").Layers;
        Assert.Contains(legSel, e => e.TextureHandle == 0xBD27FB7Cu);
        Assert.DoesNotContain(legSel, e => e.TextureHandle == 0x232DF7F9u);

        // Every surfaced layer carries AtlasSno and native px (the
        // engine's authoritative composite-extent, since the scene
        // does not author per-layer sub-rects inside the disc).
        foreach (var row in rl.States)
            foreach (var layer in row.Layers)
            {
                Assert.NotEqual(0, layer.AtlasSno);
                Assert.True(layer.NativeWidth > 0, $"layer {layer.TextureHandle:X8} native W");
                Assert.True(layer.NativeHeight > 0, $"layer {layer.TextureHandle:X8} native H");
            }
    }

    /// <summary>The paragon board-chrome render model. Scene 657304's
    /// main board is a 5-piece composite: centre background
    /// (<c>Template_Board_Background_Center → 0x2954DF0C</c>, 1200²
    /// catalog-resolvable) plus a 4-cardinal-side rim — Top + Bottom
    /// share <c>0x900C7D87</c>, Left + Right share <c>0x225F2DA8</c>,
    /// both bound via the standard <c>0x6B1C5D9C</c> texture-handle
    /// field on <c>Template_Board_Background_{Top,Right,Bottom,Left}</c>.
    /// Scene 964599's board-select panel chrome carries the
    /// preview-frame backing (<c>Board_BG</c>'s two block handles) +
    /// the filigree band (<c>Board_Icon_Filigrees</c>). Engine-internal
    /// rim animation art is not surfaced (CL-28 / CL-30 / CL-32
    /// no-fabrication discipline).</summary>
    [SkippableFact]
    public void ReadParagonBoardChrome_surfaces_scene_bound_chrome()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var chrome = d4.ReadParagonRenderModel().BoardChrome;

        // Centre background — handle, atlas SNO, native px all present.
        // FR-C16 R7: the centre carries an authored 1200×1200 rect (its
        // nWidth/nHeight are tag-2-encoded; the pre-R7 0x22-only parser
        // read zero records for this widget and so reported an all-zero
        // rect — that "no authored sub-rect" was an artifact, not a fact).
        Assert.Equal(0x2954DF0Cu, chrome.BackgroundCenter.TextureHandle);
        Assert.Equal(447106, chrome.BackgroundCenter.AtlasSno);
        Assert.True(chrome.BackgroundCenter.NativeWidth > 1000);
        Assert.True(chrome.BackgroundCenter.NativeHeight > 1000);
        Assert.Equal(1200, chrome.BackgroundCenter.Rect.Width);
        Assert.Equal(1200, chrome.BackgroundCenter.Rect.Height);

        // Rim — 4 cardinal sides, Top/Bottom share one band handle,
        // Left/Right share another. Handles are scene-bound but do
        // NOT resolve via the icon catalog (AtlasSno / native px
        // come back 0 — the consumer uses a non-icon-catalog texture
        // path or a procedural equivalent).
        Assert.Equal(0x900C7D87u, chrome.BorderTop.TextureHandle);
        Assert.Equal(chrome.BorderTop.TextureHandle,
                     chrome.BorderBottom.TextureHandle);
        Assert.Equal(0x225F2DA8u, chrome.BorderRight.TextureHandle);
        Assert.Equal(chrome.BorderRight.TextureHandle,
                     chrome.BorderLeft.TextureHandle);
        foreach (var side in new[]
        {
            chrome.BorderTop, chrome.BorderRight,
            chrome.BorderBottom, chrome.BorderLeft,
        })
        {
            Assert.NotEqual(0u, side.TextureHandle);
            Assert.Equal(0, side.AtlasSno);       // not icon-catalog
            Assert.Equal(0, side.NativeWidth);
            Assert.Equal(0, side.NativeHeight);
        }

        // Board-select chrome — every layer is non-zero-handle and
        // carries an atlas SNO + native size (catalog-resolvable).
        Assert.NotEmpty(chrome.BoardSelectChrome);
        foreach (var layer in chrome.BoardSelectChrome)
        {
            Assert.NotEqual(0u, layer.TextureHandle);
            Assert.NotEqual(0, layer.AtlasSno);
            Assert.True(layer.NativeWidth > 0);
            Assert.True(layer.NativeHeight > 0);
        }

        // Fire-border discipline: the typed model does not surface
        // unverified engine-internal frame candidates. None of the
        // ember-strip handles the FR listed are scene-bound to any
        // board-chrome widget, so none appear in the typed model.
        var fireBorderCatalog = new[]
        {
            0x6CFA1668u, 0x749F8139u, 0xAA7571ABu,
        };
        var chromeHandles = new[]
        {
            chrome.BackgroundCenter.TextureHandle,
            chrome.BorderTop.TextureHandle,
            chrome.BorderRight.TextureHandle,
            chrome.BorderBottom.TextureHandle,
            chrome.BorderLeft.TextureHandle,
        }.Concat(chrome.BoardSelectChrome.Select(l => l.TextureHandle));
        Assert.All(chromeHandles,
            h => Assert.DoesNotContain(h, fireBorderCatalog));
    }

    /// <summary>FR-C14 R9 — surface the engine's tile-style overlay
    /// recipe (<c>snoTiledStyle</c> field, FieldHash <c>0x07DB38D3</c>).
    /// Scene 657304 binds 13 widgets to <see cref="SnoGroup.UiStyle"/>
    /// SNOs; this test asserts (a) the chrome surface exposes them as
    /// <see cref="ParagonBoardChrome.TiledStyleBindings"/>, (b) the
    /// canonical <c>Vignette → InnerShadow</c> (SNO 843662) binding is
    /// present and read, (c) the parsed <see cref="TiledStyleDefinition"/>
    /// carries the expected <c>flImageScale</c> = 1.0 + the
    /// <see cref="TiledStyleDefinition.TypeTagNSlice"/> variant, fully
    /// decoded (R10).</summary>
    [SkippableFact]
    public void ReadParagonRenderModel_surfaces_tiled_style_bindings()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var model = d4.ReadParagonRenderModel();
        Assert.NotEmpty(model.BoardChrome.TiledStyleBindings);

        // The Vignette → SNO 843662 ("InnerShadow") binding. FR-C14 R10
        // decoded the NSlice variant fully: this is a STRETCHED
        // inner-shadow (all tile flags 0), NOT a tiled pattern — which
        // corrects the R8/R9 "Vignette is the board pattern overlay"
        // hypothesis.
        var vignette = model.BoardChrome.TiledStyleBindings
            .FirstOrDefault(b => b.WidgetName == "Vignette");
        Assert.NotNull(vignette);
        Assert.Equal(843662, vignette!.TiledStyleSnoId);
        Assert.NotNull(vignette.Style);
        Assert.Equal(843662, vignette.Style!.SnoId);
        Assert.Equal(TiledStyleDefinition.TypeTagNSlice, vignette.Style.TypeTag);
        Assert.Equal("NSlice", vignette.Style.VariantName);
        Assert.Equal(1.0f, vignette.Style.ImageScale);
        Assert.False(vignette.Style.HasPartialDecode);
        Assert.Equal(0, vignette.Style.TileCenter);
        Assert.Equal(0, vignette.Style.TileHorizontalBorders);
        Assert.Equal(0, vignette.Style.TileVerticalBorders);

        // Frame_AbilityPoints (SNO 1309282) IS a genuinely tiled NSlice
        // (fTileCenter=1) — confirms the decode distinguishes
        // stretched vs tiled.
        var pts = model.BoardChrome.TiledStyleBindings
            .FirstOrDefault(b => b.WidgetName == "Paragon_Points_Container");
        if (pts?.Style is not null && pts.Style.VariantName == "NSlice")
        {
            Assert.Equal(1, pts.Style.TileCenter);
            Assert.Equal(0.5f, pts.Style.ImageScale);
        }

        // The standalone reader matches the chrome surface's pre-read.
        var direct = d4.ReadTiledStyle(843662);
        Assert.Equal(vignette.Style, direct);

        // The 13 scene-657304 bindings observed in R8: every one of
        // these widget-names should be present in TiledStyleBindings.
        var widgets = new[]
        {
            "Vignette", "Paragon_Points_Container", "Points_Tutorial_Highlight",
            "Glyph_BG", "Glyph_Frame", "BoardPreview_Text_Container",
            "ParagonStats", "Board_Info", "Node_Tutorial_Highlight",
            "CoreStatEntryStack",
        };
        foreach (var name in widgets)
            Assert.Contains(model.BoardChrome.TiledStyleBindings, b => b.WidgetName == name);
    }

    /// <summary>FR-C17 — the board grid-layout metric. Asserts the
    /// engine's authored canvas (1920×1200) + node cell extent (100) +
    /// cell-adjacent pitch are read from game data, replacing the
    /// empirical pixel pitch. Validates the owner's ~67.7px measurement
    /// = cell extent 100 × render scale.</summary>
    [SkippableFact]
    public void ReadParagonBoardGrid_surfaces_engine_cell_metric()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var grid = d4.ReadParagonBoardGrid();
        Assert.Equal(1920, grid.CanvasWidth);
        Assert.Equal(1200, grid.CanvasHeight);
        Assert.Equal(100, grid.CellExtent);
        Assert.Equal(grid.CellExtent, grid.Pitch);   // cells adjacent

        // The owner's empirical 67.7px pitch is the authored 100 ref
        // units at the consumer's board render scale: 100 * s ≈ 67.7
        // ⇒ s ≈ 0.677. Assert the metric reproduces it within 1px.
        double scale = 67.7 / grid.Pitch;
        Assert.InRange(grid.Pitch * scale, 66.7, 68.7);
    }

    /// <summary>FR-C16 — the per-node render program. Asserts the recipe
    /// is the ordered (z-sorted) node state-widget run with the engine's
    /// verbatim names + hImageFrame handles, including the owner-oracle
    /// anchor (<c>Node_IconBase → 0x1D166DC7</c>) and the directional
    /// arrows (<c>Arrow_Top/Right/Bottom/Left</c>).</summary>
    [SkippableFact]
    public void ReadParagonNodeRecipe_surfaces_ordered_state_widget_layers()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var recipe = d4.ReadParagonNodeRecipe();
        Assert.NotEmpty(recipe.Layers);

        // Z-order is strictly increasing (= scene serialization order).
        for (int k = 1; k < recipe.Layers.Count; k++)
            Assert.True(recipe.Layers[k].ZOrder > recipe.Layers[k - 1].ZOrder);

        ParagonNodeRecipeLayer L(string name) =>
            recipe.Layers.First(l => l.WidgetName == name);

        // Owner-oracle anchor: the unselected grey base disc.
        Assert.Equal(0x1D166DC7u, L("Node_IconBase").ImageHandle);
        // Purchased-state disc.
        Assert.Equal(0xD3051CCAu, L("Node_Purchased").ImageHandle);
        // Directional arrows are present as their own ordered layers.
        Assert.Equal(0xD51CAB25u, L("Arrow_Top").ImageHandle);
        Assert.Equal(0x6D3CB8DEu, L("Arrow_Right").ImageHandle);
        Assert.Equal(0x8EEAC178u, L("Arrow_Bottom").ImageHandle);
        Assert.Equal(0xB6D8C741u, L("Arrow_Left").ImageHandle);

        // The base disc draws before the purchased overlay (z-order).
        Assert.True(L("Node_IconBase").ZOrder < L("Node_Purchased").ZOrder);

        // The glow layers are UIBlinkerStyle (pulsing-glow class).
        Assert.Equal(0x145F2056u, L("NodeAvailableGlow").WidgetClassId);

        // FR-C16 R5 — the per-rarity disc state pair is split into
        // SelectionDiscs (unselected vs selected), matching the owner #22
        // oracle for all three rarities. CL-46 flattened both into one
        // CompositeHandles list (drew the selected ring on unselected
        // nodes); the split is the fix.
        var magic = L("Template_Node_Magic").SelectionDiscs;
        Assert.NotNull(magic);
        Assert.Equal(0x621CB6FFu, magic!.Unselected);   // magic unselected disc
        Assert.Equal(0x72C29402u, magic.Selected);      // magic selected (ring baked in)

        var rare = L("Template_Node_Rare").SelectionDiscs;
        Assert.NotNull(rare);
        Assert.Equal(0xB71BD068u, rare!.Unselected);    // rare unselected
        Assert.Equal(0x03EDABABu, rare.Selected);       // rare selected

        var leg = L("Template_Node_Legendary").SelectionDiscs;
        Assert.NotNull(leg);
        Assert.Equal(0x232DF7F9u, leg!.Unselected);     // legendary unselected
        Assert.Equal(0xBD27FB7Cu, leg.Selected);        // legendary selected

        // FR-C16 R5 — the small-negative rect-inset sentinels CL-46
        // surfaced as bogus composite handles (0xFFFFFFFD = −3 overscan on
        // the larger Legendary disc, etc.) are excluded: every surfaced
        // composite handle resolves to a real atlas frame.
        foreach (var layer in recipe.Layers)
        {
            foreach (var h in layer.CompositeHandles)
                Assert.True(d4.IsParagonTextureHandle(h),
                    $"{layer.WidgetName} composite 0x{h:X8} is not a resolvable handle");
            if (layer.SelectionDiscs is { } sd)
            {
                Assert.True(d4.IsParagonTextureHandle(sd.Unselected));
                Assert.True(d4.IsParagonTextureHandle(sd.Selected));
            }
        }

        // Non-rarity layers carry no selection-disc pair.
        Assert.Null(L("Node_IconBase").SelectionDiscs);
        Assert.Empty(L("Node_IconBase").CompositeHandles);

        // FR-C16 R7 — Node_Icon decodes exactly now: it is a
        // tag-2-encoded sparse widget the pre-R7 0x22-only parser mis-keyed
        // (the handle 0x25DAA956 landed in nBottom = 635087190). The R7
        // reader keys it correctly: a symmetric 28-inset symbol slot with
        // the handle on hImageFrame, not nBottom. (The handle is the
        // template default; per-node it is replaced by ParagonNode.HIconMask.)
        var nodeIcon = L("Node_Icon");
        Assert.Equal(28, nodeIcon.Rect.Top);
        Assert.Equal(28, nodeIcon.Rect.Bottom);
        Assert.Equal(28, nodeIcon.Rect.Left);
        Assert.Equal(28, nodeIcon.Rect.Right);
        Assert.Equal(0x25DAA956u, nodeIcon.ImageHandle);

        // Every rect field across the recipe is now a sane authored
        // reference-unit value (no mis-keyed handle leaking into a rect).
        foreach (var layer in recipe.Layers)
        {
            var r = layer.Rect;
            foreach (var v in new[] { r.Left, r.Right, r.Top, r.Bottom, r.Width, r.Height })
                Assert.InRange(v, -4096, 4096);
        }
    }

    /// <summary>FR-C11 R3 §2 — scene-bound binding on the
    /// <c>Common_Node_Revealed</c> widget. <c>0xC1473C21</c> via the
    /// standard <c>0x6B1C5D9C</c> texture-handle field; authored rect
    /// L=R=T=B=3 inside the 100-pitch <c>NodeTemplate</c> box (94×94
    /// footprint centred in the 100×100 cell). Surfaced on
    /// <see cref="ParagonRenderLayout.CommonNodeRevealedLayer"/>.
    /// <br/><br/>
    /// <b>FR-C15 R2 / CL-39 role retraction:</b> CL-33 originally
    /// proposed this binding as the "per-node cell background tile"
    /// (the persistent darker rounded square the lighter field shows
    /// through); the role-claim was retracted after the consumer
    /// plumbed the binding end-to-end and visual inspection of
    /// <c>0xC1473C21</c>'s atlas frame revealed a horizontal
    /// ember-strip / cell-reveal glow, NOT a clean rounded square.
    /// The actual visual role is more likely a transient cell-reveal
    /// effect (consistent with the widget name <c>_Revealed</c>) than
    /// the persistent per-node tile owner sees in-game. This test
    /// asserts ONLY the binding facts (handle, rect, atlas) — no
    /// role assertion.</summary>
    [SkippableFact]
    public void ReadParagonRenderLayout_surfaces_common_node_revealed_binding()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();
        var bg = rl.CommonNodeRevealedLayer;

        Assert.Equal(0xC1473C21u, bg.TextureHandle);
        Assert.Equal(447106, bg.AtlasSno);
        Assert.True(bg.NativeWidth > 0);
        Assert.True(bg.NativeHeight > 0);
        // Authored rect: 100-pitch cell with 3-ref inset on each side.
        Assert.Equal(3, bg.Rect.Left);
        Assert.Equal(3, bg.Rect.Right);
        Assert.Equal(3, bg.Rect.Top);
        Assert.Equal(3, bg.Rect.Bottom);
        Assert.Equal(100, bg.Rect.Width);
        Assert.Equal(100, bg.Rect.Height);
    }

    /// <summary>Per-rarity layer scene-bindedness gate. Every layer
    /// in a per-rarity (rarity 0/2/3/4) <see cref="StateElements"/>
    /// row must be bound by some widget in scene 657304 — the
    /// per-rarity composite (including the selected-state perimeter
    /// ring) is always authored scene art, never a fabricated catalog
    /// reference. Cross-references each layer's
    /// <see cref="NodeElement.TextureHandle"/> against
    /// <see cref="Diablo4Storage.ReadParagonRenderModel"/>'s exhaustive
    /// per-widget bindings; a layer whose handle does not appear in
    /// the scene fails CI.</summary>
    [SkippableFact]
    public void ParagonRenderLayout_per_rarity_layers_are_scene_bound()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();
        var model = d4.ReadParagonRenderModel();
        var sceneBound = new HashSet<uint>(
            model.Scenes.First(s => s.SnoId == 657304)
                .Widgets.SelectMany(w => w.Layers)
                .Select(l => l.TextureHandle));

        var perRarity = rl.States.Where(s => s.RarityOverride >= 0).ToArray();
        Assert.NotEmpty(perRarity); // sanity: the gate actually ran

        var unbound = perRarity
            .SelectMany(s => s.Layers.Select(l => (Row: s, Layer: l)))
            .Where(x => !sceneBound.Contains(x.Layer.TextureHandle))
            .Select(x => $"r{x.Row.RarityOverride} {x.Row.State} " +
                         $"0x{x.Layer.TextureHandle:X8}")
            .ToArray();
        Assert.True(unbound.Length == 0,
            "per-rarity layer scene-bindedness gate: per-rarity layers " +
            "whose handle is not bound by any widget in scene 657304 " +
            "(per-rarity composites must be authored scene art, never " +
            "fabricated): " + string.Join(", ", unbound));
    }

    /// <summary>FR-C12 §4 — special-node (rarity-override -1) layer
    /// scene-bindedness gate. Parity with the per-rarity gate, but
    /// extended to socket / start / gate / overlay rows. Cross-references
    /// against the raw scene 657304 widget data via
    /// <see cref="Diablo4Storage.ReadUiScene"/>, not the
    /// icon-catalog-filtered <c>Scenes</c> view — the CL-31 → CL-32
    /// lesson, applied to special nodes.</summary>
    [SkippableFact]
    public void ParagonRenderLayout_special_node_layers_are_scene_bound()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();
        var scene = d4.ReadUiScene(657304);

        var raw = new HashSet<uint>();
        foreach (var w in scene.Widgets)
        {
            foreach (var f in w.Fields)
                if (f.HasValue && f.RawValue is not 0 and not 0xFFFFFFFFu)
                    raw.Add(f.RawValue);
            foreach (var v in w.ExtraLayerValues)
                if (v is not 0 and not 0xFFFFFFFFu) raw.Add(v);
        }

        var unbound = rl.States
            .Where(s => s.RarityOverride < 0)
            .SelectMany(s => s.Layers.Select(l => (Row: s, Layer: l)))
            .Where(x => !raw.Contains(x.Layer.TextureHandle))
            .Select(x => $"{x.Row.State} 0x{x.Layer.TextureHandle:X8}")
            .ToArray();
        Assert.True(unbound.Length == 0,
            "special-node layer scene-bindedness gate: layers whose " +
            "handle does not appear in the raw scene 657304 widget data: "
            + string.Join(", ", unbound));
    }

    /// <summary>The per-binding-record completeness gate (complement
    /// to <see cref="ParagonRenderModel_covers_every_bound_atlas_handle"/>).
    /// Every enumerated state in
    /// <see cref="ParagonRenderLayout.States"/> must carry at least one
    /// bound layer or be explicitly marked
    /// <see cref="StateElements.Unresolved"/> (a row enumerated for
    /// schema completeness whose art is composited inside another
    /// row's bindings — e.g. <c>overlay.selectionRing</c>'s red ring
    /// lives inside each per-rarity selected composite). The
    /// handle-level gate dedups by atlas handle, so a state row with
    /// <c>Layers=[0]</c> can stay green when its handle appears under
    /// another widget; this gate is shape-agnostic and catches that
    /// case.</summary>
    [SkippableFact]
    public void ParagonRenderLayout_every_enumerated_state_has_layers()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();
        Assert.NotEmpty(rl.States); // sanity: the gate actually ran

        var empty = rl.States
            .Where(s => s.Layers.Count == 0 && !s.Unresolved)
            .Select(s => $"r{s.RarityOverride} {s.State}")
            .ToArray();
        Assert.True(empty.Length == 0,
            "per-binding-record gate: enumerated state rows have " +
            "Layers=[0] without Unresolved=true: " +
            string.Join(", ", empty));

        // Belt-and-braces: Unresolved rows must in fact have empty Layers.
        var inconsistent = rl.States
            .Where(s => s.Unresolved && s.Layers.Count > 0)
            .Select(s => $"r{s.RarityOverride} {s.State}")
            .ToArray();
        Assert.True(inconsistent.Length == 0,
            "Unresolved=true rows must have empty Layers: " +
            string.Join(", ", inconsistent));
    }

    /// <summary>FR-C12 R2 — row-completeness gate. Parity with the
    /// per-rarity / special-node scene-bindedness gates above, but
    /// in the REVERSE direction: every scene-bound atlas handle on a
    /// row-bearing widget in scene 657304 must appear in some
    /// <see cref="ParagonRenderLayout.States"/> row's
    /// <see cref="StateElements.Layers"/>. Catches the CL-31→32-class
    /// gap surfaced by the FR-C12 R2 broad probe: scene 657304 bound
    /// the on-board socket composite layers (0xF6443089 outer disk,
    /// 0x23F487F3 red pulse) on the Usage_Slot_2 side-panel widget
    /// (not on a Glyph/Socket/Ring/Pulse-named widget), and the
    /// existing scene-bind gate ran in the existence direction only —
    /// it could not catch a row that omitted a scene-bound layer.
    /// This gate runs in the completeness direction.
    /// <br/><br/>
    /// "Row-bearing widget" = a widget whose name matches one of the
    /// known per-node / per-rarity / per-socket / per-overlay roles
    /// listed below; the engine composites these widgets' atlas
    /// handles into the per-node draw. Non-row-bearing widgets in
    /// scene 657304 (panel chrome, text containers, decorations
    /// outside the per-node draw) are excluded — they bind atlas
    /// handles for their own purposes, not the per-node composite.
    /// </summary>
    [SkippableFact]
    public void ParagonRenderLayout_row_layers_cover_every_scene_bound_row_widget_handle()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();
        var scene = d4.ReadUiScene(657304);

        // Widget-name prefixes / equalities that name a widget binding
        // a per-node composite layer (a "row-bearing widget"). Derived
        // empirically from the FR-C12 R2 broad probe (e:/tmp/scene-
        // probe), then narrowed to widgets the engine actually
        // composites into the per-node draw. Usage_Slot_2 is included
        // because the FR-C12 R2 owner atlas-frame oracle proved the
        // on-board socket composite reuses its 0x58-block handles.
        // Common_Node_Revealed is included because its 0xC1473C21 IS
        // surfaced as CommonNodeRevealedLayer (binding-only field;
        // role retracted per CL-39).
        bool IsRowBearingWidget(string n) =>
            n.StartsWith("Template_Node_", StringComparison.Ordinal) ||
            n == "Node_IconBase" || n == "Node_Purchased" ||
            n == "Node_Located" || n == "Node_EquipGlow" ||
            n == "GlyphNodeGlow_Revealed" ||
            n == "GlyphNodeGlow_Purchased" ||
            n == "Common_Node_Revealed" || n == "Common_Node_BG_Black" ||
            n == "NodeAvailableGlow" ||
            n.StartsWith("Connector_", StringComparison.Ordinal) ||
            n.StartsWith("Arrow_", StringComparison.Ordinal) ||
            n == "Usage_Slot_2"; // FR-C12 R2: side-panel widget whose 0x58 block scene-binds the on-board socket outer-disk + red-pulse handles

        // Handles that ARE scene-bound on row-bearing widgets but are
        // intentionally NOT in any row (the small Usage_Slot icon-gem
        // tile 0x3084D186 at 25² is the side-panel gem icon, not part
        // of the on-board socket composite — owner atlas-frame oracle).
        // Document each exclusion with its empirical basis.
        var documentedExclusions = new HashSet<uint>
        {
            0x3084D186u, // Usage_Slot_*'s 25² side-panel gem-icon tile (not on-board socket art)
        };

        // The 0x58 block also carries small non-handle ints (counters /
        // owner-class-ids). Per the FR-C8/C9 0x58 model only values
        // >= 0x10000 and != 0xFFFFFFFF are real atlas handles; the
        // catalog filter is the authoritative classifier.
        var rawHandles = new HashSet<uint>();
        foreach (var w in scene.Widgets)
        {
            if (!IsRowBearingWidget(w.Name)) continue;
            foreach (var f in w.Fields)
                if (f.HasValue && d4.IsParagonTextureHandle(f.RawValue))
                    rawHandles.Add(f.RawValue);
            foreach (var v in w.ExtraLayerValues)
                if (d4.IsParagonTextureHandle(v))
                    rawHandles.Add(v);
        }

        var rowHandles = new HashSet<uint>(
            rl.States.SelectMany(s => s.Layers).Select(l => l.TextureHandle));
        if (rl.CommonNodeRevealedLayer.TextureHandle != 0)
            rowHandles.Add(rl.CommonNodeRevealedLayer.TextureHandle);

        var unrowed = rawHandles
            .Where(h => !rowHandles.Contains(h) && !documentedExclusions.Contains(h))
            .Select(h => $"0x{h:X8}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
        Assert.True(unrowed.Length == 0,
            "row-completeness gate: scene-bound handles on " +
            "row-bearing widgets in scene 657304 that no §7.2 row " +
            "carries (and are not on the documented exclusion list — " +
            "see FR-C12 R2): " + string.Join(", ", unrowed));
    }

    /// <summary>FR-C12 R3 — row no-phantom gate (CL-35). Complement to
    /// the row-completeness gate. The earlier gates assert every
    /// scene-bound row-bearing-widget handle appears in SOME row
    /// (no-drop) and every row layer is scene-bound (no-fabrication).
    /// This gate adds: every row layer's source widget must be in
    /// the AUTHORIZED widget set for that row's state class — i.e.,
    /// the engine actually composites that widget for that state.
    /// A row layer whose handle is scene-bound only on a widget the
    /// engine doesn't dispatch for the row's state is a PHANTOM
    /// (decode artefact, not part of the recipe). FR-C12 R2 had the
    /// shared rarity-base 0x1D166DC7 incorrectly in the socket rows
    /// because the projection prepended it on the universal-base
    /// assumption; owner visual oracle on the rebuilt app proved
    /// the engine NEVER dispatches Node_IconBase for socket cells.
    /// CL-35 drops it and this gate prevents the regression.</summary>
    [SkippableFact]
    public void ParagonRenderLayout_socket_rows_have_no_phantom_layers()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var rl = d4.ReadParagonRenderLayout();
        var scene = d4.ReadUiScene(657304);

        // Build a per-widget handle index for scene 657304: which
        // widgets bind each catalog-resolvable atlas handle.
        var widgetsByHandle = new Dictionary<uint, List<string>>();
        foreach (var w in scene.Widgets)
        {
            void Note(uint h)
            {
                if (!d4.IsParagonTextureHandle(h)) return;
                if (!widgetsByHandle.TryGetValue(h, out var owners))
                    widgetsByHandle[h] = owners = new List<string>();
                owners.Add(w.Name);
            }
            foreach (var f in w.Fields)
                if (f.HasValue) Note(f.RawValue);
            foreach (var v in w.ExtraLayerValues) Note(v);
        }

        // Authorized widget set for socket.* states. The engine
        // dispatches these widgets when rendering a socket cell;
        // any layer in a socket row MUST be bound on one of these
        // (anything else = phantom). Owner visual-oracle confirmed
        // (CL-35).
        var socketAuthorized = new HashSet<string>(StringComparer.Ordinal)
        {
            "GlyphNodeGlow_Revealed",  // bead ring (unselected/selected)
            "GlyphNodeGlow_Purchased", // bead ring (socketed)
            "Usage_Slot_2",            // 0x58-block: outer disk + inner well + bead ring (the engine reuses these for the on-board render)
        };

        var phantoms = new List<string>();
        foreach (var row in rl.States.Where(s => s.State.StartsWith("socket.", StringComparison.Ordinal)))
        {
            foreach (var layer in row.Layers)
            {
                if (!widgetsByHandle.TryGetValue(layer.TextureHandle, out var owners))
                {
                    phantoms.Add($"{row.State} 0x{layer.TextureHandle:X8} (no widget binds this handle in scene 657304)");
                    continue;
                }
                if (!owners.Any(socketAuthorized.Contains))
                    phantoms.Add(
                        $"{row.State} 0x{layer.TextureHandle:X8} bound only on " +
                        $"[{string.Join(",", owners.Distinct())}] — not in socket-authorized set");
            }
        }
        Assert.True(phantoms.Count == 0,
            "socket no-phantom gate: socket-row layers whose source " +
            "widgets are not in the engine-dispatched socket set " +
            "(FR-C12 R3 / CL-35): " + string.Join("; ", phantoms));
    }

    /// <summary>Board-chrome scene-bindedness gate (parity with the
    /// per-rarity gate). The centre background must be scene-bound in
    /// 657304's catalog-resolvable per-widget bindings; every
    /// board-select chrome layer must be scene-bound in 964599's
    /// catalog-resolvable bindings; the 4 rim-side handles must
    /// appear in the raw scene-657304 widget data (their target
    /// resolves via a non-icon-catalog texture path, so SceneModel
    /// filters them out — the gate cross-references the raw scene
    /// instead). Catches a future projection that drops to a
    /// fabricated catalog handle.</summary>
    [SkippableFact]
    public void ReadParagonBoardChrome_layers_are_scene_bound()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var model = d4.ReadParagonRenderModel();
        HashSet<uint> Bound(int sno) => new(model.Scenes
            .First(s => s.SnoId == sno).Widgets
            .SelectMany(w => w.Layers).Select(l => l.TextureHandle));

        var bound657 = Bound(657304);
        var bound964 = Bound(964599);

        var bg = model.BoardChrome.BackgroundCenter;
        if (bg.TextureHandle != 0)
            Assert.Contains(bg.TextureHandle, bound657);

        foreach (var layer in model.BoardChrome.BoardSelectChrome)
            Assert.Contains(layer.TextureHandle, bound964);

        // Rim sides: handles target a non-icon-catalog texture path,
        // so SceneModel filters them out — verify against the raw
        // scene 657304 widget data via ReadUiScene.
        var scene = d4.ReadUiScene(657304);
        var raw657 = new HashSet<uint>();
        foreach (var w in scene.Widgets)
        {
            foreach (var f in w.Fields)
                if (f.HasValue && f.RawValue is not 0 and not 0xFFFFFFFFu)
                    raw657.Add(f.RawValue);
            foreach (var v in w.ExtraLayerValues)
                if (v is not 0 and not 0xFFFFFFFFu) raw657.Add(v);
        }
        foreach (var side in new[]
        {
            model.BoardChrome.BorderTop,
            model.BoardChrome.BorderRight,
            model.BoardChrome.BorderBottom,
            model.BoardChrome.BorderLeft,
        })
        {
            if (side.TextureHandle != 0)
                Assert.Contains(side.TextureHandle, raw657);
        }
    }
}
