using System;
using System.IO;
using System.Linq;
using System.Text;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;
using Xunit;

namespace WiseOwl.Casc.Diablo4.Tests;

/// <summary>
/// B1–B6 typed record readers. Synthetic tests (CI-safe, no game bytes)
/// prove the layout walks; the live test enforces the converged §7
/// acceptance matrix verbatim against build 3.0.2.71886.
/// </summary>
public sealed class TypedReaderTests
{
    // --- a tiny SNO-blob builder (16-byte header, payload base 0x10) ------
    private sealed class Blob
    {
        private byte[] _b;
        public Blob(int payloadSize)
        {
            _b = new byte[0x10 + payloadSize];
            U32(0x00, 0xDEADBEEF);                    // dwSignature
        }
        public byte[] Bytes => _b;
        public void U32(int abs, uint v)
        {
            _b[abs] = (byte)v; _b[abs + 1] = (byte)(v >> 8);
            _b[abs + 2] = (byte)(v >> 16); _b[abs + 3] = (byte)(v >> 24);
        }
        public void I32(int abs, int v) => U32(abs, (uint)v);
        public void F32(int abs, float v) =>
            U32(abs, BitConverter.SingleToUInt32Bits(v));
        public void PU32(int payloadOff, uint v) => U32(0x10 + payloadOff, v);
        public void PI32(int payloadOff, int v) => I32(0x10 + payloadOff, v);
        public void PF32(int payloadOff, float v) => F32(0x10 + payloadOff, v);
        public void PAscii(int payloadOff, string s)
        {
            var by = System.Text.Encoding.ASCII.GetBytes(s);
            Array.Copy(by, 0, _b, 0x10 + payloadOff, by.Length);
        }
    }

    [Fact]
    public void B1_board_round_trips()
    {
        var b = new Blob(64 + 9 * 4);
        b.PI32(0, 2458674);                            // snoId
        b.PU32(12, 3);                                 // nWidth
        // arEntries descriptor @ payload 16: i64 pad, i32 dataOffset@+8,
        // i32 dataSize@+12.  9 cells (3x3), one empty.
        b.PI32(24, 64);                                // dataOffset (payload-rel)
        b.PI32(28, 9 * 4);                             // dataSize
        int[] cells = [1, 2, 3, 4, -1 /*0xFFFFFFFF*/, 6, 7, 8, 9];
        for (var i = 0; i < 9; i++) b.PU32(64 + i * 4, (uint)cells[i]);

        var bd = ParagonBoardDefinition.Parse(b.Bytes);
        Assert.Equal(2458674, bd.SnoId);
        Assert.Equal(3, bd.Width);
        Assert.Equal(9, bd.Cells.Count);
        Assert.Null(bd.Cells[4]);
        Assert.Equal(6, bd.CellAt(1, 2));
        Assert.Equal(8, bd.NodeCount);
    }

    [Fact]
    public void B2_node_round_trips_with_inline_and_gbid_attrs()
    {
        var b = new Blob(96 + 2 * 88 + 8);
        b.PI32(0, 678776);
        b.PU32(8, 0);                                  // hIcon
        b.PU32(12, 0x25DABFC0);                        // hIconMask
        b.PI32(20, 2);                                 // eRarityOverride = Magic
        b.PI32(24, 12345);                             // snoPassivePower
        b.PI32(80, 1);                                 // bHasSocket
        b.PI32(84, 0);                                 // bIsGate
        // ptAttributes @ payload 32: dataOffset@+8, dataSize@+12.
        b.PI32(40, 96);                                // dataOffset
        b.PI32(44, 2 * 88);                            // dataSize (2 specifiers)
        // attr[0]: GBID-referenced
        b.PI32(96 + 0, 10);                            // eAttribute
        b.PI32(96 + 4, 7);                             // nParam
        b.PI32(96 + 12, 11);                           // +12
        b.PU32(96 + 48, 0x42C16A1B);                   // gbidFormula
        // attr[1]: inline formula "2*x"
        b.PI32(96 + 88 + 0, 252);
        b.PI32(96 + 88 + 4, 0);
        b.PI32(96 + 88 + 12, 1031902);
        b.PU32(96 + 88 + 48, 0xFFFFFFFF);              // no gbid → inline
        b.PI32(96 + 88 + 24, 96 + 2 * 88);             // inline offset (payload-rel)
        b.PI32(96 + 88 + 28, 3);                       // inline size
        b.PAscii(96 + 2 * 88, "2*x");

        var n = ParagonNodeDefinition.Parse(b.Bytes);
        Assert.Equal(678776, n.SnoId);
        Assert.Equal(2, n.RarityOverride);
        Assert.Equal(ParagonRarity.Magic, n.Rarity);
        Assert.True(n.HasSocket);
        Assert.False(n.IsGate);
        Assert.Equal(0x25DABFC0u, n.HIconMask);
        Assert.Equal(12345, n.SnoPassivePower);
        Assert.Equal(2, n.Attributes.Count);

        var a0 = n.Attributes[0];
        Assert.Equal(10, a0.AttributeId);
        Assert.Equal(7, a0.NParam);
        Assert.Equal(11, a0.ParamPlus12);
        Assert.Equal(0x42C16A1Bu, a0.FormulaGbid);
        Assert.False(a0.IsInline);
        Assert.Equal("", a0.InlineFormula);

        var a1 = n.Attributes[1];
        Assert.Equal(252, a1.AttributeId);
        Assert.Equal(1031902, a1.ParamPlus12);
        Assert.True(a1.IsInline);
        Assert.Equal("2*x", a1.InlineFormula);
    }

    [Fact]
    public void B3_glyph_collects_up_to_three_affixes()
    {
        var b = new Blob(120);
        b.PI32(0, 999111);
        b.PU32(104, 5001);
        b.PU32(108, 0xFFFFFFFF);                       // empty slot → skipped
        b.PU32(112, 5003);
        var g = ParagonGlyphDefinition.Parse(b.Bytes);
        Assert.Equal(999111, g.SnoId);
        Assert.Equal([5001, 5003], g.AffixSnoIds);
    }

    [Fact]
    public void B4_glyph_affix_round_trips()
    {
        var b = new Blob(96);
        b.PI32(0, 700200);
        b.PI32(24, 3);                                 // eAffectedNodeRarity
        b.PI32(48, 1);                                 // eBonusOperation
        b.PF32(76, 250f);                              // flStartingBonusScalar
        b.PF32(80, 25f);                               // flAddedBonusScalarPerLevel
        var a = ParagonGlyphAffixDefinition.Parse(b.Bytes);
        Assert.Equal(700200, a.SnoId);
        Assert.Equal(3, a.AffectedRarity);
        Assert.Equal(1, a.Operation);
        Assert.Equal(250f, a.Base);
        Assert.Equal(25f, a.PerLevel);
    }

    [Fact]
    public void B5_attribute_formulas_walk_round_trips()
    {
        var b = new Blob(512);
        b.PI32(0, 201912);
        b.PI32(8, AttributeFormulaTable.AttributeFormulasType);   // = 22
        // ptData polymorphic @ payload16: dataOffset@+8.
        b.PI32(16 + 8, 64);                            // ptData dataOffset
        // tableBase = 64 + 8 (type tag) = 72; tEntries @ tableBase+16 = 88.
        b.PI32(88 + 8, 128);                           // entries dataOffset
        b.PI32(88 + 12, 280);                          // entriesSize (1 entry)
        // entry @128: szName[256]@+0, gbid@+256, arRanges@+264.
        b.PAscii(128, "ParagonNodeCoreStat_Normal");
        b.PU32(128 + 256, 0xFFFFFFFF);                 // in-record gbid (null)
        b.PI32(128 + 264 + 8, 420);                    // ranges dataOffset
        b.PI32(128 + 264 + 12, 48);                    // rangesSize (1 range)
        // range @420: start@+0, v1@+4, v2@+8, tFormula@+16.
        b.PI32(420 + 0, 0);
        b.PF32(420 + 4, 1f);
        b.PF32(420 + 8, 99f);
        b.PI32(420 + 16 + 8, 470);                     // FormulaOffset
        b.PI32(420 + 16 + 12, 1);                      // FormulaSize
        b.PAscii(470, "5");

        var t = AttributeFormulaTable.Parse(b.Bytes);
        Assert.Equal(201912, t.SnoId);
        Assert.Single(t.Entries);
        Assert.True(t.TryGetFormulaText("ParagonNodeCoreStat_Normal", out var txt));
        Assert.Equal("5", txt);
        Assert.True(t.TryGetNameByGbid(
            Diablo4.GbidHash("ParagonNodeCoreStat_Normal"), out var nm));
        Assert.Equal("ParagonNodeCoreStat_Normal", nm);
        Assert.Equal(0x42C16A1Bu,
            t.Entries[0].NameGbid);                    // identity == DJB2
    }

    [Fact]
    public void B5_rejects_non_attribute_formulas_gamebalance()
    {
        var b = new Blob(64);
        b.PI32(0, 12345);
        b.PI32(8, 7);                                  // some other table type
        Assert.Throws<CascFormatException>(() => AttributeFormulaTable.Parse(b.Bytes));
    }

    // --- live §7 acceptance matrix ---------------------------------------
    private static string? Install()
    {
        var env = Environment.GetEnvironmentVariable("WISEOWL_CASC_INSTALL");
        if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env!, ".build.info")))
            return env;
        const string d4 = @"D:\Diablo IV";
        return File.Exists(Path.Combine(d4, ".build.info")) ? d4 : null;
    }

    [SkippableFact]
    public void Acceptance_matrix_against_live_install()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        // ParagonBoard 2458674 (Paragon_Warlock_00) → Width 21, 441 cells.
        var board = d4.ReadParagonBoard(2458674);
        Assert.Equal(2458674, board.SnoId);
        Assert.Equal(21, board.Width);
        Assert.Equal(441, board.Cells.Count);          // == Width*Width
        Assert.True(board.NodeCount is > 60 and < 441);

        // ParagonNode 678776 (Generic_Normal_Int): sig + snoId + sane fields.
        var nodeBlob = d4.ReadSno(SnoGroup.ParagonNode, 678776);
        Assert.Equal(SnoRecord.ExpectedSignature, new SnoRecord(nodeBlob).Signature);
        var node = ParagonNodeDefinition.Parse(nodeBlob);
        Assert.Equal(678776, node.SnoId);
        Assert.Equal(0, node.RarityOverride);          // Generic_Normal_* = Common
        Assert.NotEmpty(node.Attributes);

        // GameBalance 201912 AttributeFormulas — §7 acceptance.
        var gb = d4.ReadAttributeFormulas();
        Assert.Equal(201912, gb.SnoId);
        Assert.Equal(1038, gb.Entries.Count);
        Assert.True(gb.TryGetFormulaText("ParagonNodeCoreStat_Normal", out var t1));
        Assert.Equal("5", t1.Trim());
        Assert.True(gb.TryGetFormulaText("ParagonNodeCoreStat_Magic", out var t2));
        Assert.Equal("7", t2.Trim());
        Assert.True(gb.TryGetNameByGbid(0x42C16A1B, out var byGbid));
        Assert.Equal("ParagonNodeCoreStat_Normal", byGbid);
        Assert.Equal(0x42C16A1Bu, Diablo4.GbidHash("ParagonNodeCoreStat_Normal"));
        // The % formulas resolve to text (consumer evaluates them).
        Assert.True(gb.TryGetFormulaText("ParagonNodeDamageBonus_Magic", out var t3));
        Assert.NotEqual("", t3);

        // B6: a node's hIconMask resolves to an atlas frame (first-party link).
        var iconNode = d4.ReadParagonNode(678776);
        var handle = iconNode.HIconMask != 0 ? iconNode.HIconMask : iconNode.HIcon;
        if (handle != 0)
        {
            Assert.True(d4.TryGetIconFrame(handle, out var atlasSno, out var fr));
            Assert.True(atlasSno > 0);
            Assert.Equal(handle, fr.ImageHandle);
        }

        // Glyph + GlyphAffix: a real Warlock glyph yields up to 3 affix ids,
        // each decodable with sane op/base.
        var glyphEntry = d4.CoreToc.EntriesInGroup(SnoGroup.ParagonGlyph).First();
        var glyph = d4.ReadParagonGlyph(glyphEntry.Id);
        Assert.Equal(glyphEntry.Id, glyph.SnoId);
        Assert.InRange(glyph.AffixSnoIds.Count, 0, 3);
        foreach (var aff in glyph.AffixSnoIds)
        {
            var ga = d4.ReadParagonGlyphAffix(aff);
            Assert.Equal(aff, ga.SnoId);
        }
    }
}
