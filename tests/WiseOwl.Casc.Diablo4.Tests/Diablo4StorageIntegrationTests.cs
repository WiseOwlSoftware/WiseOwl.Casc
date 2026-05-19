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

        // Refinements (decode-true): ornate/symbol/socket-ring fill the
        // 100-ref node box ⇒ ÷ disc(86) ≈ 1.163. The grey rim ring is
        // app-drawn (absent from scene) ⇒ 0 (the truthful answer, not a
        // gap). Per-rarity Tint / pulse AnimSpec are NOT bound (fixed
        // shader recipe / engine-driven, §2.3) ⇒ null is correct.
        Assert.Equal(100.0 / 86.0, rl.Ratios.OrnateOverDisc, 3);
        Assert.Equal(100.0 / 86.0, rl.Ratios.SymbolOverDisc, 3);
        Assert.Equal(100.0 / 86.0, rl.Ratios.SocketRingOverDisc, 3);
        Assert.Equal(0d, rl.Ratios.GreyRingOverDisc);
        Assert.All(rl.States, s => Assert.Null(s.Tint));
        Assert.All(rl.States, s => Assert.Null(s.Animation));

        // §7.5 gate 1: the §7.2 rows + the FR-C8 R9 availableGlow row
        // (19; contract amended pre-publish — CL-25), verbatim keys.
        var expected = new (int r, string s)[]
        {
            (0,"unselected"),(0,"selected"),(2,"unselected"),(2,"selected"),
            (3,"unselected"),(3,"selected"),(4,"unselected"),(4,"selected"),
            (-1,"socket.unselected"),(-1,"socket.selected"),(-1,"socket.socketed"),
            (-1,"gate.unselected"),(-1,"gate.selected"),
            (-1,"start.unselected"),(-1,"start.selected"),
            (-1,"overlay.selectionRing"),(-1,"overlay.connectorBar"),
            (-1,"overlay.pointerTriangle"),(-1,"overlay.availableGlow"),
        };
        Assert.Equal(19, rl.States.Count);
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

        // Magic (r2): grey base + 0xFEC31E48 (135² blue interior fill,
        // owner-confirmed); selected adds 0x72C29402 (Template_Node_Magic's
        // 0x58-block 154² blue disc + perimeter ring composite — the
        // game-correct selected art, matching the Rare/Legendary pattern
        // of a scene-bound selected-variant composite).
        var magicUn = Row(2, "unselected").Layers;
        Assert.Equal(2, magicUn.Count);
        Assert.Equal(0x1D166DC7u, magicUn[0].TextureHandle);
        Assert.Equal(0xFEC31E48u, magicUn[1].TextureHandle);

        var magicSel = Row(2, "selected").Layers;
        Assert.Equal(3, magicSel.Count);
        Assert.Equal(0xFEC31E48u, magicSel[1].TextureHandle);
        Assert.Equal(0x72C29402u, magicSel[2].TextureHandle);

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

    /// <summary>FR-C11 (CL-31) — the paragon board-chrome render
    /// model. Scene 657304's main board background is bound on
    /// <c>Template_Board_Background_Center</c> (handle <c>0x2954DF0C</c>,
    /// 1200² in <c>2DUI_Paragon</c>). Scene 964599's board-select
    /// panel chrome carries the preview-frame backing
    /// (<c>Board_BG</c>'s two block handles) + the filigree band
    /// (<c>Board_Icon_Filigrees</c>). The animated fire-border art is
    /// engine-internal — its candidate atlas frames live in
    /// <c>2DUI_Paragon</c> but no scene widget binds them, so they
    /// are NOT surfaced in this typed model (CL-28 / CL-30
    /// no-fabrication discipline).</summary>
    [SkippableFact]
    public void ReadParagonBoardChrome_surfaces_scene_bound_chrome()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var chrome = d4.ReadParagonRenderModel().BoardChrome;

        // Main board background — handle, atlas SNO, native px all
        // present. Authored rect is all-zero (engine convention).
        Assert.Equal(0x2954DF0Cu, chrome.MainBoardBackground.TextureHandle);
        Assert.Equal(447106, chrome.MainBoardBackground.AtlasSno);
        Assert.True(chrome.MainBoardBackground.NativeWidth > 1000);
        Assert.True(chrome.MainBoardBackground.NativeHeight > 1000);
        Assert.Equal(default, chrome.MainBoardBackground.Rect);

        // Board-select chrome — every layer is non-zero-handle and
        // carries an atlas SNO + native size.
        Assert.NotEmpty(chrome.BoardSelectChrome);
        foreach (var layer in chrome.BoardSelectChrome)
        {
            Assert.NotEqual(0u, layer.TextureHandle);
            Assert.NotEqual(0, layer.AtlasSno);
            Assert.True(layer.NativeWidth > 0);
            Assert.True(layer.NativeHeight > 0);
        }

        // Fire-border discipline: the typed model does not surface
        // engine-internal candidates. None of the fire-border atlas
        // frames listed in FR-C11 R1 are scene-bound, so none appear
        // in either chrome list.
        var fireBorderCatalog = new[]
        {
            0x6CFA1668u, 0x749F8139u, 0xAA7571ABu,
        };
        Assert.DoesNotContain(chrome.MainBoardBackground.TextureHandle,
            fireBorderCatalog);
        Assert.All(chrome.BoardSelectChrome,
            l => Assert.DoesNotContain(l.TextureHandle, fireBorderCatalog));
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

    /// <summary>Board-chrome scene-bindedness gate (parity with the
    /// per-rarity gate). The main board background must be scene-bound
    /// in 657304; every board-select chrome layer must be scene-bound
    /// in 964599. Catches a future board-chrome projection that drops
    /// to fabricated catalog handles.</summary>
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

        var bg = model.BoardChrome.MainBoardBackground;
        if (bg.TextureHandle != 0)
            Assert.Contains(bg.TextureHandle, bound657);

        foreach (var layer in model.BoardChrome.BoardSelectChrome)
            Assert.Contains(layer.TextureHandle, bound964);
    }
}
