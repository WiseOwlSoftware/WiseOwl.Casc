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
        // Layout: fixed fields 0..104 (incl. the @88 descriptor whose
        // dataOffset/dataSize live at payload 96/100), then the two 88-byte
        // attribute specifiers at 128, then the inline-formula text, then the
        // parallel per-attribute GBID array — none overlapping (mirrors the
        // real record, where array data trails all fixed fields).
        const int attrBase = 128;
        const int inlineAt = attrBase + 2 * 88;        // 304
        const int gbidAt = inlineAt + 4;               // 308 (after "2*x")
        var b = new Blob(gbidAt + 2 * 4);
        b.PI32(0, 678776);
        b.PU32(8, 0);                                  // hIcon
        b.PU32(12, 0x25DABFC0);                        // hIconMask
        b.PI32(16, 3);                                 // eNodeType = Magic
        b.PI32(20, 2);                                 // eRarityOverride = Magic
        b.PI32(24, 12345);                             // snoPassivePower
        b.PI32(80, 1);                                 // bHasSocket
        b.PI32(84, 0);                                 // bIsGate
        // ptAttributes @ payload 32: dataOffset@+8, dataSize@+12.
        b.PI32(40, attrBase);                          // dataOffset
        b.PI32(44, 2 * 88);                            // dataSize (2 specifiers)
        // parallel per-attribute GBID array @ payload 88: dataOffset@+8 (=96),
        // dataSize@+12 (=100).
        b.PI32(96, gbidAt);                            // dataOffset
        b.PI32(100, 2 * 4);                            // dataSize (2 u32s)
        b.PU32(gbidAt + 0, 0xAAAA0001);                // gbid[0]
        b.PU32(gbidAt + 4, 0xBBBB0002);                // gbid[1]
        // attr[0]: GBID-referenced
        b.PI32(attrBase + 0, 10);                      // eAttribute
        b.PI32(attrBase + 4, 7);                       // nParam
        b.PI32(attrBase + 12, 11);                     // +12
        b.PU32(attrBase + 48, 0x42C16A1B);             // gbidFormula
        // attr[1]: inline formula "2*x"
        b.PI32(attrBase + 88 + 0, 252);
        b.PI32(attrBase + 88 + 4, 0);
        b.PI32(attrBase + 88 + 12, 1031902);
        b.PU32(attrBase + 88 + 48, 0xFFFFFFFF);        // no gbid → inline
        b.PI32(attrBase + 88 + 24, inlineAt);          // inline offset (payload-rel)
        b.PI32(attrBase + 88 + 28, 3);                 // inline size
        b.PAscii(inlineAt, "2*x");

        var n = ParagonNodeDefinition.Parse(b.Bytes);
        Assert.Equal(678776, n.SnoId);
        Assert.Equal(3, n.NodeTypeRaw);
        Assert.Equal(ParagonNodeType.Magic, n.NodeType);
        Assert.False(n.IsStart);
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
        Assert.Equal(0xAAAA0001u, a0.AttributeGbid);

        var a1 = n.Attributes[1];
        Assert.Equal(252, a1.AttributeId);
        Assert.Equal(1031902, a1.ParamPlus12);
        Assert.True(a1.IsInline);
        Assert.Equal("2*x", a1.InlineFormula);
        Assert.Equal(0xBBBB0002u, a1.AttributeGbid);
    }

    [Fact]
    public void B2_node_decodes_bonus_passive_and_stat_tag_arrays()
    {
        // Rare-shaped layout: ptAttributes @32 (2 specifiers) + the two
        // bonus-mechanic descriptors (@48 size-1 DT_SNO; @64 size-N DT_SNO[])
        // + @88 GBID array. None overlapping; mirrors a real rare node where
        // the trailing array data is packed after all fixed fields.
        const int attrBase = 128;
        const int inlineAt = attrBase + 2 * 88;       // 304
        const int gbidAt = inlineAt + 4;               // 308
        const int bonusPowerAt = gbidAt + 2 * 4;       // 316
        const int bonusTagsAt = bonusPowerAt + 4;     // 320 — 3 tag SNOs
        var b = new Blob(bonusTagsAt + 3 * 4);
        b.PI32(0, 2451111);                            // Warlock_Rare_006-style
        b.PI32(16, 0);                                 // eNodeType = Normal
        b.PI32(20, 3);                                 // eRarityOverride = Rare
        b.PI32(24, -1);                                // snoPassivePower
        // ptAttributes @ payload 32.
        b.PI32(40, attrBase);
        b.PI32(44, 2 * 88);
        // bonus-passive-power slot @ 48 — size-1 DT_SNO, value 0.
        b.PI32(56, bonusPowerAt);                      // dataOffset
        b.PI32(60, 4);                                  // dataSize
        b.PU32(bonusPowerAt, 0);                       // empty slot (the
                                                       // observed-on-every-rare value)
        // bonus stat-threshold tag array @ 64 — 3 DT_SNOs.
        b.PI32(72, bonusTagsAt);
        b.PI32(76, 3 * 4);
        b.PU32(bonusTagsAt + 0, 1022854);              // Barb_Strength+Dexterity
        b.PU32(bonusTagsAt + 4, 1015360);              // DexteritySide2
        b.PU32(bonusTagsAt + 8, 1015342);              // StrengthSide2
        // @88 GBID array.
        b.PI32(96, gbidAt);
        b.PI32(100, 2 * 4);
        b.PU32(gbidAt + 0, 0xAAAA0001);
        b.PU32(gbidAt + 4, 0xBBBB0002);
        // 2 attribute specifiers (minimal — just an id each).
        b.PI32(attrBase + 0, 79);
        b.PU32(attrBase + 48, 0x42C16A1B);             // shared formula
        b.PI32(attrBase + 88 + 0, 142);
        b.PU32(attrBase + 88 + 48, 0x42C16A1C);

        var n = ParagonNodeDefinition.Parse(b.Bytes);
        Assert.Equal(0, n.BonusPassivePowerSno);       // rare-shape, no power
        Assert.Equal([1022854, 1015360, 1015342], n.BonusStatTagSnoIds);
    }

    [Fact]
    public void B2_node_without_bonus_descriptors_returns_empty_tags_and_minus_one_power()
    {
        // Non-rare layout — both bonus descriptors are unpopulated (all zeros),
        // mirroring observed Common/Magic/Start/Gate/Socket nodes.
        const int attrBase = 128;
        const int gbidAt = attrBase + 88;
        var b = new Blob(gbidAt + 4);
        b.PI32(0, 671247);                             // Generic_Magic_Armor
        b.PI32(16, 3);                                 // eNodeType = Magic
        b.PI32(20, 2);                                 // eRarityOverride = Magic
        b.PI32(40, attrBase);
        b.PI32(44, 1 * 88);
        // descriptors @ 48 and @ 64 left zero — empty arrays.
        b.PI32(96, gbidAt);
        b.PI32(100, 4);
        b.PU32(gbidAt, 0xCCCCC001);
        b.PI32(attrBase + 0, 481);                     // base attr id
        b.PU32(attrBase + 48, 0x42C16A1B);

        var n = ParagonNodeDefinition.Parse(b.Bytes);
        Assert.Equal(-1, n.BonusPassivePowerSno);      // descriptor missing
        Assert.Empty(n.BonusStatTagSnoIds);
    }

    [Fact]
    public void B7_stat_tag_decodes_formula_text()
    {
        // Group-124 StatTag layout: snoId@0; descriptor @64 → ASCII text;
        // optional NUL terminator counted by dataSize (engine emits one).
        const int textAt = 96;
        const string formula = "760 + (455 * ParagonBoardEquipIndex)";
        var b = new Blob(textAt + formula.Length + 1);
        b.PI32(0, 1068426);                            // WillpowerMain2
        b.PI32(72, textAt);                            // dataOffset
        b.PI32(76, formula.Length + 1);                // dataSize incl NUL
        b.PAscii(textAt, formula);

        var t = StatTagDefinition.Parse(b.Bytes);
        Assert.Equal(1068426, t.SnoId);
        Assert.Equal(formula, t.ThresholdFormulaText);
    }

    [Fact]
    public void B7_stat_tag_missing_descriptor_yields_empty_formula()
    {
        var b = new Blob(80);
        b.PI32(0, 9999);
        var t = StatTagDefinition.Parse(b.Bytes);
        Assert.Equal(9999, t.SnoId);
        Assert.Equal("", t.ThresholdFormulaText);
    }

    [Theory]
    // The seven empirical (constant × budget-multiplier) verifications from
    // ParagonPowerBudget's worked-validation list. Each pair is the formula
    // text shipped in the game and the displayed magnitude the owner read
    // in-game, cross-validated on build 3.0.2.71886.
    [InlineData("5", 5.0)]                                             // Normal core stat — plain constant
    [InlineData("0.75 * ParagonPowerBudgetMultiplierNodeMagicDefensive()", 7.5)]
    [InlineData("3 * ParagonPowerBudgetMultiplierNodeMagicOffensive()", 7.5)]
    [InlineData("0.75 * ParagonPowerBudgetMultiplierNodeRareMajorDefensive()", 3.0)]
    [InlineData("1 * ParagonPowerBudgetMultiplierNodeRareMajorDefensive()", 4.0)]
    [InlineData("2 * ParagonPowerBudgetMultiplierNodeRareMajorOffensive()", 10.0)]
    [InlineData("3.5 * ParagonPowerBudgetMultiplierNodeRareMajorOffensive()", 17.5)]
    [InlineData("3 * ParagonPowerBudgetMultiplierNodeRareMinorOffensive()", 15.0)]
    // Division precedence: 1.5/2 must bind tighter than the outer *.
    [InlineData("1.5/2 * ParagonPowerBudgetMultiplierNodeRareMajorDefensive()", 3.0)]
    public void B8_magnitude_formula_evaluates_to_expected_displayed_value(
        string formula, double expected)
    {
        var v = ParagonMagnitudeFormula.Evaluate(formula);
        Assert.Equal(expected, v, precision: 10);
    }

    [Fact]
    public void B8_magnitude_formula_unknown_intrinsic_yields_NaN()
    {
        // A future build adds a new multiplier the calibration table hasn't
        // picked up yet — short-circuit to NaN, don't fabricate.
        var v = ParagonMagnitudeFormula.Evaluate(
            "1 * ParagonPowerBudgetMultiplierNodeNotARealOne()");
        Assert.True(double.IsNaN(v));
    }

    [Fact]
    public void B8_power_budget_tryget_round_trips_all_six()
    {
        Assert.True(ParagonPowerBudget.TryGetMultiplier(
            "ParagonPowerBudgetMultiplierNodeMagicDefensive", out var md));
        Assert.Equal(ParagonPowerBudget.MagicDefensive, md);

        Assert.True(ParagonPowerBudget.TryGetMultiplier(
            "ParagonPowerBudgetMultiplierNodeMagicOffensive", out var mo));
        Assert.Equal(ParagonPowerBudget.MagicOffensive, mo);

        Assert.True(ParagonPowerBudget.TryGetMultiplier(
            "ParagonPowerBudgetMultiplierNodeRareMajorDefensive", out var rmaD));
        Assert.Equal(ParagonPowerBudget.RareMajorDefensive, rmaD);

        Assert.True(ParagonPowerBudget.TryGetMultiplier(
            "ParagonPowerBudgetMultiplierNodeRareMinorDefensive", out var rmiD));
        Assert.Equal(ParagonPowerBudget.RareMinorDefensive, rmiD);

        Assert.True(ParagonPowerBudget.TryGetMultiplier(
            "ParagonPowerBudgetMultiplierNodeRareMajorOffensive", out var rmaO));
        Assert.Equal(ParagonPowerBudget.RareMajorOffensive, rmaO);

        Assert.True(ParagonPowerBudget.TryGetMultiplier(
            "ParagonPowerBudgetMultiplierNodeRareMinorOffensive", out var rmiO));
        Assert.Equal(ParagonPowerBudget.RareMinorOffensive, rmiO);

        Assert.False(ParagonPowerBudget.TryGetMultiplier("Nope", out _));
    }

    [Theory]
    // Stat-name resolution from the Generic_<Rarity>_<Token> convention:
    // the trailing token is humanized via CamelCase split + abbreviation
    // expansion to the displayed in-game stat name.
    [InlineData("Generic_Magic_Armor", "Armor")]
    [InlineData("Generic_Magic_DamageToElite", "Damage to Elite")]
    [InlineData("Generic_Magic_ResistanceCold", "Resistance Cold")]
    [InlineData("Generic_Rare_Damage", "Damage")]
    [InlineData("Generic_Rare_CriticalDamage", "Critical Damage")]
    [InlineData("Generic_Magic_DamageReductionFromVulnerable",
        "Damage Reduction from Vulnerable")]
    [InlineData("Generic_Magic_DamageReductionWhileHealthy",
        "Damage Reduction while Healthy")]
    [InlineData("Generic_Magic_Str", "Strength")]
    [InlineData("Generic_Magic_HPFlat", "Max Life (Flat)")]
    [InlineData("Generic_Magic_HPPercent", "Max Life")]
    [InlineData("Generic_Magic_CDR", "Cooldown Reduction")]
    [InlineData("Generic_Magic_MoveSpeed", "Movement Speed")]
    [InlineData("Generic_Magic_AttackSpeedBasic", "Attack Speed (Basic Skills)")]
    public void B9_stat_name_resolves_from_node_name_token(string nodeName, string expected)
    {
        var token = ParagonNodeInfoBuilder.ExtractStatToken(nodeName);
        // attributeId=0 is the don't-care fallback; only used when token is null.
        Assert.Equal(expected, ParagonNodeInfoBuilder.ResolveStatName(token, 0));
    }

    [Fact]
    public void B9_stat_name_falls_back_to_attribute_id_for_non_generic_names()
    {
        // Class-specific rares (no Generic_ prefix) carry no encoded stat
        // token — the projection's fallback exposes the AttributeId.
        var token = ParagonNodeInfoBuilder.ExtractStatToken("Warlock_Rare_006");
        Assert.Null(token);
        Assert.Equal("Attribute 259", ParagonNodeInfoBuilder.ResolveStatName(token, 259));
    }

    [Theory]
    // Token-driven dispatch — pure-stat tokens + Resistance* + HPFlat
    // are Flat; budget-multiplied magnitudes are Percent. Bare-constant
    // formulas (Normal-rarity) are Flat too.
    [InlineData("Generic_Magic_Str", 9, "5", StatUnit.Flat)]
    [InlineData("Generic_Magic_HPFlat", 133, "0.75 * Foo()", StatUnit.Flat)]
    [InlineData("Generic_Magic_ResistanceCold", 79, "0.75 * Foo()", StatUnit.Flat)]
    [InlineData("Generic_Magic_ResistanceMaxCold", 79, "0.75 * Foo()", StatUnit.Percent)]
    [InlineData("Generic_Magic_Armor", 481, "0.75 * Foo()", StatUnit.Percent)]
    [InlineData("Generic_Magic_DamageToElite", 950, "3 * Foo()", StatUnit.Percent)]
    [InlineData("Generic_Magic_HPPercent", 142, "0.75 * Foo()", StatUnit.Percent)]
    [InlineData("Generic_Magic_CDR", 237, "0.75 * Foo()", StatUnit.Percent)]
    // Normal-rarity bare constant — Flat regardless of attribute id.
    [InlineData("Generic_Normal_Damage", 252, "5", StatUnit.Flat)]
    public void B9_stat_unit_inferred_from_token_and_attribute_id(
        string nodeName, int attributeId, string formulaText, StatUnit expected)
    {
        var token = ParagonNodeInfoBuilder.ExtractStatToken(nodeName);
        Assert.Equal(expected, ParagonNodeInfoBuilder.InferUnit(token, attributeId, formulaText));
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

        // Bonus mechanic (@48/@64) + StatTag (group 124):
        //  - Warlock_Rare_006 (2451111) has exactly one bonus tag = 1068426
        //    WillpowerMain2 whose threshold formula scales by EquipIndex.
        //  - Generic_Rare_001 (679732) has the 3 class-keyed alternatives.
        //  - A magic node (Generic_Magic_Armor 671247) has no bonus arrays.
        var rare = d4.ReadParagonNode(2451111);
        Assert.Equal(ParagonRarity.Rare, rare.Rarity);
        Assert.Equal(0, rare.BonusPassivePowerSno);     // rare-shape, empty slot
        Assert.Equal([1068426], rare.BonusStatTagSnoIds);
        var willpower = d4.ReadStatTag(1068426);
        Assert.Equal(1068426, willpower.SnoId);
        Assert.Equal(
            "760 + (455 * ParagonBoardEquipIndex)",
            willpower.ThresholdFormulaText);

        var multi = d4.ReadParagonNode(679732);
        Assert.Equal([1022854, 1015360, 1015342], multi.BonusStatTagSnoIds);

        var magic = d4.ReadParagonNode(671247);
        Assert.Equal(-1, magic.BonusPassivePowerSno);
        Assert.Empty(magic.BonusStatTagSnoIds);

        // FR-C21 magnitude evaluation: Generic_Magic_Armor (671247)'s formula
        // text from the shipped AttributeFormulas table (sno 201912) reduces
        // to the owner-verified +7.5% displayed magnitude.
        var armorAttr = magic.Attributes[0];   // single attr on a Magic node
        Assert.True(gb.TryGetNameByGbid(armorAttr.FormulaGbid, out var armorFn));
        Assert.True(gb.TryGetFormulaText(armorFn, out var armorTxt));
        Assert.Equal(7.5, ParagonMagnitudeFormula.Evaluate(armorTxt), precision: 6);

        // FR-C21 projection (CL-69) — Catalog.GetNodeInfo end-to-end against
        // a magic, a rare, a socket, and a class start.
        var armorInfo = d4.Catalog.GetNodeInfo(671247)!;
        Assert.Equal(671247, armorInfo.Sno);
        Assert.Equal("Generic_Magic_Armor", armorInfo.Name);
        Assert.Equal(ParagonNodeKind.Magic, armorInfo.Kind);
        Assert.Single(armorInfo.Stats);
        var armorStat = armorInfo.Stats[0];
        Assert.Equal("Armor", armorStat.StatName);
        Assert.Equal(StatUnit.Percent, armorStat.Unit);
        Assert.NotNull(armorStat.FlatValue);
        Assert.Equal(7.5, armorStat.FlatValue!.Value, precision: 6);

        var rareInfo = d4.Catalog.GetNodeInfo(2451111)!;
        Assert.Equal(ParagonNodeKind.Rare, rareInfo.Kind);
        Assert.Equal(2, rareInfo.Stats.Count);
        // Warlock_Rare_006 has no Generic_ prefix — StatName falls back to
        // "Attribute <id>"; magnitudes still resolve through the formula path.
        Assert.StartsWith("Attribute ", rareInfo.Stats[0].StatName);

        // Socket and Start nodes carry no stat grants.
        var socketInfo = d4.Catalog.GetNodeInfo(681756)!;
        Assert.Equal(ParagonNodeKind.Socket, socketInfo.Kind);
        Assert.Empty(socketInfo.Stats);

        // Cache check — repeat lookup returns the same instance (reference
        // equality is the Optimizer's perf guarantee).
        var armorInfo2 = d4.Catalog.GetNodeInfo(671247)!;
        Assert.Same(armorInfo, armorInfo2);

        // Missing SNO ⇒ null (and the cache memoizes that miss).
        Assert.Null(d4.Catalog.GetNodeInfo(999_999_999));

        // CL-71 inner UV decode (FR-C20 #32 codec tail) — the per-frame
        // 16-byte trailer is an inner / 9-slice-middle UV rect that
        // most frames mirror to the outer rect but some atlases inset.
        // Find at least one frame with a non-trivial inner rect.
        bool sawDistinct = false;
        foreach (var (atlasSno, td) in d4.TextureMeta.BySno)
        {
            foreach (var fr in td.Frames)
            {
                // The inner pixel rect always resolves (engineered to
                // stay non-empty even on degenerate-point cases).
                var (_, _, iw, ih) = fr.InnerPixelRect(td.Width, td.Height);
                Assert.True(iw >= 1 && ih >= 1);
                if (fr.HasDistinctInner) { sawDistinct = true; break; }
            }
            if (sawDistinct) break;
        }
        Assert.True(sawDistinct, "at least one frame should expose a real inner-rect inset across the install");

        // CL-72 Power → class facet (FR-C20 #32). The engine names
        // class-skill powers as <ClassSnoName>_<SkillName>; the facet
        // decodes that NameConvention without touching PowerDefinition.
        // Spot-check a handful of well-known class-skill powers.
        var bashName = d4.CoreToc.GetName(SnoGroup.Power, 200765)!;  // Barbarian_Bash
        Assert.Equal("Barbarian", d4.Catalog.TryGetPowerClassFromName(bashName));
        var whirlwindName = d4.CoreToc.GetName(SnoGroup.Power, 206435)!;  // Barbarian_Whirlwind
        Assert.Equal("Barbarian", d4.Catalog.TryGetPowerClassFromName(whirlwindName));
        Assert.Equal("Necromancer",
            d4.Catalog.TryGetPowerClassFromName("Necromancer_BloodLance"));
        Assert.Equal("Sorcerer",
            d4.Catalog.TryGetPowerClassFromName("Sorcerer_Fireball"));

        // Non-class names produce no facet — monsters / item-affix powers /
        // mid-word class tokens stay unfaceted (honesty).
        Assert.Null(d4.Catalog.TryGetPowerClassFromName("MorluCaster_Fireball"));
        Assert.Null(d4.Catalog.TryGetPowerClassFromName("1HAxe_Unique_Druid_100"));
        Assert.Null(d4.Catalog.TryGetPowerClassFromName(""));
        Assert.Null(d4.Catalog.TryGetPowerClassFromName("noUnderscore"));

        // FindByFacet hot-path: every Power tagged class=Sorcerer must
        // actually start with "Sorcerer_" (the round-trip integrity check
        // — bounded to a Take(50) so the live test stays under a second).
        var sorcerers = d4.Catalog
            .FindByFacet(AssetKind.Power, "class", "Sorcerer")
            .Take(50)
            .ToList();
        Assert.NotEmpty(sorcerers);
        Assert.All(sorcerers, r =>
            Assert.StartsWith("Sorcerer_", r.Name, StringComparison.Ordinal));

        // CL-70 hot path — GetBoardNodes on Paragon_Warlock_00 (2458674).
        // The board's 441-cell grid contains ~60+ placed nodes (sparse
        // grid), each pair carries (row, col) and the resolved info.
        var boardNodes = d4.Catalog.GetBoardNodes(2458674);
        Assert.True(boardNodes.Count is > 60 and < 441);
        // Each pair has in-range coordinates and a non-null node info.
        foreach (var (cell, info) in boardNodes)
        {
            Assert.InRange(cell.Row, 0, board.Width - 1);
            Assert.InRange(cell.Col, 0, board.Width - 1);
            Assert.True(info.Sno > 0);
        }
        // Cache identity — repeat lookup returns the same list reference
        // (the optimizer's perf guarantee).
        Assert.Same(boardNodes, d4.Catalog.GetBoardNodes(2458674));
        // Distinct definitions count matches the optimizer's expectation
        // (~17–21 distinct on Warlock_00; assert in a generous band).
        var distinctDefs = new System.Collections.Generic.HashSet<int>();
        foreach (var (_, info) in boardNodes) distinctDefs.Add(info.Sno);
        Assert.InRange(distinctDefs.Count, 10, 30);

        // Missing/undecodable board ⇒ empty list (memoized).
        var noBoard = d4.Catalog.GetBoardNodes(999_999_999);
        Assert.Empty(noBoard);

        // CL-70 EnumerateNodes — lazy, returns every paragon node in the
        // install. Sample a few via Take(); the global count is many
        // hundred but we only need the contract.
        var sample = d4.Catalog.EnumerateNodes().Take(5).ToList();
        Assert.Equal(5, sample.Count);
        foreach (var info in sample)
        {
            Assert.True(info.Sno > 0);
            Assert.NotNull(info.Name);
        }

        // EnumerateNodes honours AssetQuery.NameContains (Kind override is
        // transparent — pass NameContains, get only paragon-node hits).
        var armorNodes = d4.Catalog
            .EnumerateNodes(new AssetQuery { NameContains = "Generic_Magic_Armor" })
            .ToList();
        Assert.NotEmpty(armorNodes);
        Assert.All(armorNodes, n =>
            Assert.Contains("Armor", n.Name, StringComparison.OrdinalIgnoreCase));
    }
}
