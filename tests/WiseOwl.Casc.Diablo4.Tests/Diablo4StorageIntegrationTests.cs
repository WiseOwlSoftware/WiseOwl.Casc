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

    /// <summary>FR-C8 R6 (CL-24): the directional pointer is NOT
    /// pure-procedural — <c>Arrow_{Top,Right,Bottom,Left}</c> bind the
    /// pre-oriented arrow art + an authored rect (the FR-C7 0x22 path
    /// FR-C7 never projected). <c>overlay.pointerTriangle</c> now carries
    /// the four cardinal arrows; <c>connectorBar</c>/<c>selectionRing</c>
    /// stay empty (no bound art — engine/procedural, FR-C7 correct).</summary>
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
        // cause, CL-24) — the catalogued connector handles. Selection
        // ring has no scene widget → genuinely engine-drawn (empty).
        var conn = rl.States.First(s => s.State == "overlay.connectorBar").Layers
            .Select(e => e.TextureHandle).ToArray();
        Assert.NotEmpty(conn);
        Assert.All(conn, h => Assert.Contains(h, ConnectorHandles));
        Assert.Empty(rl.States.First(s => s.State == "overlay.selectionRing").Layers);

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
}
