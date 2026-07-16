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
/// prove the layout walks; the live acceptance tests assert the decode's
/// <b>structure and invariants</b> against the live install (robust to
/// game-content churn). Exact game-authored values that change per build
/// (registry sizes, atlas frame counts, registry-ordinal AttributeIds)
/// are isolated in <see cref="Season_content_anchors_pinned_to_build_3_1_1"/>
/// (<c>Trait kind=content-snapshot</c>) so a season bump surfaces as one
/// obvious, filterable re-baseline cluster — not scattered acceptance
/// failures. Current live build: 3.1.1.72836 / Season 14.
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
    // CL-78 — AttributeNames template-strip helper. Pulls the bare
    // display name out of the AttributeDescriptions template.
    [InlineData("[{VALUE}|~|] Strength", "Strength")]
    [InlineData("[{VALUE}|~|] Maximum Life", "Maximum Life")]
    [InlineData("+[{VALUE}] Armor", "Armor")]
    [InlineData("+[{VALUE}*100|1%|] Damage to Elites", "Damage to Elites")]
    [InlineData("+[{VALUE2}*100|%|] {VALUE1} Damage", "Damage")]
    [InlineData("+[{VALUE2}] {VALUE1} Resistance", "Resistance")]
    [InlineData("+[{VALUE}*100|%|] Movement Speed", "Movement Speed")]
    [InlineData("{c_label}Lucky Hit:{/c} Up to a +[{VALUE}*100|1%|] Chance to Knockback",
        "Lucky Hit: Up to a Chance to Knockback")]
    public void B10_attribute_names_strip_template_pulls_display_name(
        string template, string expected)
    {
        Assert.Equal(expected, AttributeNames.StripTemplate(template));
    }

    [SkippableTheory]
    [Trait("kind", "content-snapshot")]
    // CL-78 / FR-C27 (CL-88) — Diablo4Storage.GetAttributeName end-to-end
    // (live data, sno 4080 template lookup + strip). content-snapshot: the
    // ids are the CURRENT-build (3.1.1.72836 / Season 14) registry ordinals;
    // they renumber each season (Armor 481→482, Elites 950→953, high-health
    // 1120→1123, near/far 1102/1104→1105/1107) and the runtime resolver
    // tracks them — this pins the exact current ids, while the structural
    // coverage test below is the season-robust guarantee. A failure here on
    // a game update = expected id drift; re-baseline from `SnoScan attrcover`.
    [InlineData(9,    "Strength")]
    [InlineData(10,   "Intelligence")]
    [InlineData(11,   "Willpower")]
    [InlineData(12,   "Dexterity")]
    [InlineData(133,  "Maximum Life")]
    [InlineData(482,  "Armor")]
    [InlineData(953,  "Damage to Elites")]
    [InlineData(275,  "Critical Strike Chance")]
    [InlineData(288,  "Critical Strike Damage")]
    [InlineData(208,  "Movement Speed")]
    [InlineData(221,  "Attack Speed")]
    [InlineData(237,  "Cooldown Reduction")]
    [InlineData(373,  "Thorns")]
    // Shifted conditional-damage tail — the entries a hardcoded id map lost.
    [InlineData(1123, "Damage while Healthy")]
    [InlineData(1105, "Damage to Close Enemies")]
    [InlineData(1107, "Damage to Distant Enemies")]
    [InlineData(1119, "Damage to Healthy Enemies")]
    [InlineData(736,  "Vulnerable Damage")]
    [InlineData(748,  "Damage while Fortified")]
    public void B10_get_attribute_name_resolves_via_attribute_descriptions(
        int attributeId, string expected)
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);
        Assert.Equal(expected, d4.GetAttributeName(attributeId));
    }

    /// <summary>FR-C27 (CL-88) — the season-robust guarantee: every
    /// <c>AttributeId</c> a live <c>Generic_&lt;Rarity&gt;_&lt;Token&gt;</c>
    /// node carries resolves through <see cref="Diablo4Storage.GetAttributeName(int, string)"/>
    /// to a non-empty name, with <b>no</b> hardcoded ids — so the assertion
    /// holds across seasons even as the engine renumbers the registry. Proves
    /// the runtime id→token scan retired the curated id-map's fragility.</summary>
    [SkippableFact]
    public void GetAttributeName_resolves_every_live_generic_node_attribute()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        // Walk every Generic_<Rarity>_<Token> node (same source the resolver
        // scans). For each attribute whose token we curate a label for, the
        // resolved name must be non-empty — WHATEVER id the engine assigned
        // it this season. No id is hardcoded, so this holds across seasons.
        int checkedTokens = 0;
        foreach (var e in d4.CoreToc.EntriesInGroup(SnoGroup.ParagonNode))
        {
            if (!e.Name.StartsWith("Generic_", StringComparison.Ordinal)) continue;
            int sep = e.Name.IndexOf('_', "Generic_".Length);   // '_' after the rarity
            if (sep < 0 || sep + 1 >= e.Name.Length) continue;
            var token = e.Name[(sep + 1)..];
            if (!AttributeNames.LabelByToken.ContainsKey(token)) continue;
            foreach (var attr in d4.ReadParagonNode(e.Id).Attributes)
            {
                if (attr.AttributeId < 0) continue;
                var name = d4.GetAttributeName(attr.AttributeId);
                if (!string.IsNullOrEmpty(name)) checkedTokens++;
                else
                    Assert.Fail($"{e.Name} (id {attr.AttributeId}, token '{token}') resolved to null.");
            }
        }
        Assert.True(checkedTokens >= 15,
            $"expected the scan to cover many curated tokens; only {checkedTokens}.");
    }

    [SkippableFact]
    public void B10_get_attribute_name_returns_null_for_unmapped_id()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);
        // 99999 isn't in the curated map → honest null (consumer
        // composes from the existing fallback chain).
        Assert.Null(d4.GetAttributeName(99999));
    }

    [Theory]
    // CL-76 — the canonical AttributeId map wins over the node-name
    // token. Critical for multi-attribute nodes like Gate where every
    // row would otherwise inherit the "Gate" structural token.
    [InlineData("Gate", 9, "Strength")]
    [InlineData("Gate", 10, "Intelligence")]
    [InlineData("Gate", 11, "Willpower")]
    [InlineData("Gate", 12, "Dexterity")]
    // Single-row Generic_Normal_* nodes already produced the right
    // name via the token humanizer — the AttributeId map gives the
    // same answer, so behaviour stays stable.
    [InlineData("Str", 9, "Strength")]
    [InlineData("Int", 10, "Intelligence")]
    [InlineData("Will", 11, "Willpower")]
    [InlineData("Dex", 12, "Dexterity")]
    // Class-specific rares carry no Generic_ token AND the rare-attr
    // ids aren't in the canonical map — honest "Attribute <id>" fallback.
    [InlineData(null, 259, "Attribute 259")]
    [InlineData(null, 288, "Attribute 288")]
    // Budget-category id (481) — multiple stats share the id; the
    // node-name token disambiguates. The canonical map returns null
    // for 481, so the humanized token wins.
    [InlineData("Armor", 481, "Armor")]
    [InlineData("DamageReductionFromVulnerable", 481,
        "Damage Reduction from Vulnerable")]
    public void B9_stat_name_prefers_attribute_id_over_node_token(
        string? token, int attributeId, string expected)
    {
        Assert.Equal(expected, ParagonNodeInfoBuilder.ResolveStatName(token, attributeId));
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
        // Op-1 (Attribute): the AffectedAttributes VLA descriptor lives
        // at payload +16/+20 — point it at 2 packed (i32 AttributeId,
        // u32 ParamPlus12) entries to prove the per-op descriptor walk.
        var b = new Blob(160);
        b.PI32(0, 700200);
        b.PI32(24, 3);                                 // eAffectedNodeRarity (synthetic non-zero)
        b.PI32(48, 1);                                 // eBonusOperation = Attribute
        b.PF32(76, 250f);                              // flStartingBonusScalar
        b.PF32(80, 25f);                               // flAddedBonusScalarPerLevel
        b.PF32(84, 100f);                              // flDisplayFactor
        b.PI32(88, -1);                                // snoPower (no linked power on Op-1)
        // VLA at +16/+20 → data @128, 16 bytes (2 entries).
        b.PI32(16, 128);
        b.PI32(20, 16);
        b.PI32(128, 9);                                // AttributeId = 9 (Strength)
        b.PU32(132, 0xFFFFFFFF);                       // ParamPlus12 = NoParam
        b.PI32(136, 259);                              // AttributeId = 259 (DamageBonusTag)
        b.PU32(140, 0x6A1F0A80);                       // ParamPlus12 = Abyss skill-tag GBID
        var a = ParagonGlyphAffixDefinition.Parse(b.Bytes);
        Assert.Equal(700200, a.SnoId);
        Assert.Equal(3, a.AffectedRarity);
        Assert.Equal(ParagonRarity.Rare, a.AffectedRarityKind);
        Assert.Equal(1, a.Operation);
        Assert.Equal(ParagonGlyphAffixOperation.Attribute, a.OperationKind);
        Assert.Equal(250f, a.Base);
        Assert.Equal(25f, a.PerLevel);
        Assert.Equal(100.0, a.DisplayFactor);
        Assert.Null(a.LinkedPowerSnoId);
        Assert.Equal(2, a.AffectedAttributes.Count);
        Assert.Equal(9, a.AffectedAttributes[0].AttributeId);
        Assert.False(a.AffectedAttributes[0].HasParam);
        Assert.Equal(259, a.AffectedAttributes[1].AttributeId);
        Assert.True(a.AffectedAttributes[1].HasParam);
        Assert.Equal(0x6A1F0A80u, a.AffectedAttributes[1].ParamPlus12);
    }

    [Fact]
    public void B4_glyph_affix_op5_surfaces_linked_power()
    {
        // Op-5 (Power): no per-attribute scaling, but snoPower @88 is a
        // group-29 PowerDefinition ref (the linked power that defines
        // the threshold chain).
        var b = new Blob(112);
        b.PI32(0, 800300);
        b.PI32(48, 5);                                 // eBonusOperation = Power
        b.PF32(84, 1f);                                // flDisplayFactor (always 1 on Op-5)
        b.PI32(88, 2072755);                           // snoPower = ParagonGlyph_DamageElite
        var a = ParagonGlyphAffixDefinition.Parse(b.Bytes);
        Assert.Equal(ParagonGlyphAffixOperation.Power, a.OperationKind);
        Assert.Equal(2072755, a.LinkedPowerSnoId);
        Assert.Empty(a.AffectedAttributes);            // op-5 has no per-attribute VLA
        Assert.Null(a.AffectedRarityKind);             // raw 0 → null
        Assert.Equal(1.0, a.DisplayFactor);
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

    // Hoisted out of the assertion below to satisfy CA1861 (no constant
    // array argument re-allocated on each call).
    private static readonly int[] ExpectedGlyphRadiusUpgradeLevels = [25, 50];

    // LIB-1 expected gear-type membership (hoisted to satisfy CA1861).
    private static readonly string[] ExpectedJewelryTypes = ["Amulet", "Ring"];
    private static readonly string[] ExpectedArmorTypes = ["Boots", "ChestArmor", "Gloves", "Helm", "Legs"];

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

        // GameBalance 201912 AttributeFormulas — §7 acceptance. Structural:
        // the registry parses to a sane size (the exact count is a content
        // snapshot — see Season_content_anchors_pinned_to_build_3_1_1).
        var gb = d4.ReadAttributeFormulas();
        Assert.Equal(201912, gb.SnoId);
        Assert.True(gb.Entries.Count >= 1000,
            $"AttributeFormulas parsed only {gb.Entries.Count} entries (expected ≥1000).");
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
        // Warlock_Rare_006's first stat is attr 259 (DamageBonusTag,
        // tag-conditional). Pre-CL-85 fell back to "Attribute 259"
        // (no Generic_ token, no curated id label); CL-85's
        // compound-key map resolves it to "Demonology Damage" — the
        // FR-C28 anchor. Magnitudes still resolve through the formula
        // path (CL-76 validated).
        Assert.Equal("Demonology Damage", rareInfo.Stats[0].StatName);

        // Socket and Start nodes carry no stat grants.
        var socketInfo = d4.Catalog.GetNodeInfo(681756)!;
        Assert.Equal(ParagonNodeKind.Socket, socketInfo.Kind);
        Assert.Empty(socketInfo.Stats);

        // CL-74 — Gate ("Board Attachment Gate") nodes DO carry stats
        // (owner game-oracle 2026-05-23 reverting CL-69's over-drop):
        // each Gate grants +5 to each of the four basic stats
        // (Strength / Intelligence / Willpower / Dexterity = AttributeIds
        // 9 / 10 / 11 / 12), at Flat unit, from a bare-constant "5"
        // formula. IsGate stays true (the structural marker is content-
        // independent).
        var gateInfo = d4.Catalog.GetNodeInfo(994337)!;  // Generic_Gate
        Assert.Equal(ParagonNodeKind.Gate, gateInfo.Kind);
        Assert.True(gateInfo.IsGate);
        Assert.Equal(4, gateInfo.Stats.Count);
        var byAttr = gateInfo.Stats.ToDictionary(s => s.AttributeId);
        // CL-76 — per-row StatName via the canonical AttributeId map.
        Assert.Equal("Strength",     byAttr[9].StatName);
        Assert.Equal("Intelligence", byAttr[10].StatName);
        Assert.Equal("Willpower",    byAttr[11].StatName);
        Assert.Equal("Dexterity",    byAttr[12].StatName);
        Assert.All(gateInfo.Stats, s =>
        {
            Assert.Equal(5.0, s.FlatValue);
            Assert.Equal(StatUnit.Flat, s.Unit);
        });

        // CL-75 / FR-C22 — LocalizedTitle from the §6.7 sibling
        // StringList convention. Two anchors validated by owner
        // game-oracle (in-game tooltip headers, 2026-05-23):
        //   Gate          -> "Board Attachment Gate"
        //   Start (Barb)  -> "Paragon Starting Node"
        // Stat nodes (Generic_Magic_DamageToElite, Warlock_Rare_006)
        // have no sibling StringList — LocalizedTitle is empty (honest
        // sentinel; consumer composes from Stats/Kind).
        Assert.Equal("Board Attachment Gate", gateInfo.LocalizedTitle);

        var startBarbInfo = d4.Catalog.GetNodeInfo(830650)!;  // StartNodeBarb
        Assert.Equal(ParagonNodeKind.Start, startBarbInfo.Kind);
        Assert.Equal("Paragon Starting Node", startBarbInfo.LocalizedTitle);
        Assert.Empty(startBarbInfo.Stats);  // CL-66 confirmed: Start has 0 attrs

        // Generic stat nodes (Generic_<Rarity>_<Token>) have no sibling —
        // LocalizedTitle is empty (consumer composes from Stats/Kind).
        Assert.Equal(string.Empty, armorInfo.LocalizedTitle);

        // Named rare nodes DO have a sibling — the engine's authored
        // rare-node title sits there ("Binding", "Fathomless", "Pyrosis", …).
        // Warlock_Rare_006 → "Binding" (the in-game-displayed title for that
        // class-specific rare).
        Assert.Equal("Binding", rareInfo.LocalizedTitle);

        // TryReadParagonNodeTitle low-level surface stays symmetric:
        // present for structural nodes, absent for stat nodes.
        Assert.True(d4.TryReadParagonNodeTitle(994337, out var gateTitle));
        Assert.Equal("Board Attachment Gate", gateTitle);
        Assert.False(d4.TryReadParagonNodeTitle(671247, out var armorTitle));
        Assert.Equal(string.Empty, armorTitle);

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

        // CL-73 item NameConvention facets (FR-C20 #32) — three patterns
        // resolved by Catalog.ParseItemConvention (the dispatch behind
        // the Item case in Catalog.Facets).
        Assert.Equal(
            ("1HAxe", "Unique", "Druid"),
            Catalog.ParseItemConvention("1HAxe_Unique_Druid_100"));
        Assert.Equal(
            ("1HFocus", "Unique", "Necromancer"),
            Catalog.ParseItemConvention("1HFocus_Unique_Necro_100"));  // Necro alias
        Assert.Equal(
            ("Helm", "Rare", "Barbarian"),
            Catalog.ParseItemConvention("Helm_Rare_Barb_Crafted_47"));
        // Generic = engine's "no class" sentinel — type + rarity emit; class null.
        Assert.Equal(
            ("1HAxe", "Magic", (string?)null),
            Catalog.ParseItemConvention("1HAxe_Magic_Generic_001"));
        // Cosmetics: <Type>_<Class>_<Name>, no rarity slot.
        Assert.Equal(
            ("Cosmetic", (string?)null, "Barbarian"),
            Catalog.ParseItemConvention("Cosmetic_Barbarian_FooBar"));
        Assert.Equal(
            ("Cosmetic", (string?)null, "Necromancer"),
            Catalog.ParseItemConvention("Cosmetic_Necro_FooBar"));     // alias
        // Fallback — unmatched conventions yield only the type token.
        Assert.Equal(
            ("QST", (string?)null, (string?)null),
            Catalog.ParseItemConvention("QST_Frac_Underworld_04"));
        Assert.Equal(default,
            Catalog.ParseItemConvention(""));
        Assert.Equal(
            ("noUnderscore", (string?)null, (string?)null),
            Catalog.ParseItemConvention("noUnderscore"));

        // FindByFacet round-trip — every Item tagged class=Druid carries
        // _Druid_ somewhere in its name (the alias map keeps Necro/
        // Necromancer collapsed to the same SnoName facet, so this Druid
        // spot-check is unambiguous).
        var druidItems = d4.Catalog
            .FindByFacet(AssetKind.Item, "class", "Druid")
            .Take(50)
            .ToList();
        Assert.NotEmpty(druidItems);
        Assert.All(druidItems, r =>
            Assert.Contains("_Druid", r.Name, StringComparison.Ordinal));

        // CL-83 / FR-C24 — glyph engine constants
        // (BaseRadius / RadiusUpgradeLevels / MaxLevel). The .gph
        // record carries no per-glyph variance on the live build, so
        // these are engine constants (cross-validated against the
        // Optimizer's Warlock-21 oracle).
        var anyGlyph = d4.ReadParagonGlyph(1023194);
        Assert.Equal(3, anyGlyph.BaseRadius);
        Assert.Equal(ExpectedGlyphRadiusUpgradeLevels, anyGlyph.RadiusUpgradeLevels);
        Assert.Equal(150, anyGlyph.MaxLevel);

        // CL-79 / CL-86 / FR-C24 — ParagonGlyphDefinition.LocalizedTitle
        // (sibling ParagonGlyph_<SnoName>, label Name; CL-86 swapped the
        // CL-79 Item_-prefixed table for the non-prefixed table that
        // covers every glyph including the Rare_<Stat>_Generic shape).
        // Anchor: glyph 1023194 'Rare_011_Intelligence_Side' →
        // "Guzzler" (the Optimizer's Warlock oracle, row 13).
        var guzzler = d4.ReadParagonGlyph(1023194);
        Assert.Equal("Guzzler", guzzler.LocalizedTitle);
        Assert.Equal(ParagonRarity.Rare, guzzler.Rarity);
        Assert.NotEmpty(guzzler.UsableByClassSnoIds);  // CL-18 stayed populated

        // CL-86 / FR-C24 Headhunter counter-round — glyph 2117207
        // 'Rare_Will_Generic' is the 21st Warlock glyph whose
        // LocalizedTitle CL-79 returned empty because no
        // Item_ParagonGlyph_Rare_Will_Generic sibling exists. The
        // non-prefixed sibling ParagonGlyph_Rare_Will_Generic (sno
        // 2117206) carries label Name = "Headhunter" — the in-game
        // oracle title for that glyph.
        var headhunter = d4.ReadParagonGlyph(2117207);
        Assert.Equal("Headhunter", headhunter.LocalizedTitle);
        Assert.Equal(ParagonRarity.Rare, headhunter.Rarity);

        // CL-79 / FR-C24 — ParagonGlyphAffixDefinition.Description
        // (sibling ParagonGlyphAffix_<SnoName>, label Desc; raw template
        // text with engine markup preserved). Anchor: affix 1068542 on
        // glyph Guzzler — the Optimizer's probe sno.
        var affix = d4.ReadParagonGlyphAffix(1068542);
        Assert.NotEmpty(affix.Description);
        // Engine markup tokens preserved (color tags, value placeholder).
        Assert.Contains("[{GlyphAffixScalar}", affix.Description, StringComparison.Ordinal);

        // CL-84 / FR-C24 slice 2b — DamageWhileHealthy_Intelligence_Side
        // is an Op-2 (NodeAmplification) affix. AffectedAttributes VLA
        // sits at +64/+68 → 2 entries; Tags VLA sits at +120/+124 → 3
        // GBIDs; DisplayFactor is the per-op constant 500; no linked
        // power (Op-2 carries scaling in Base/PerLevel, not a Power ref);
        // AffectedRarityKind is null (every live affix has +24 == 0).
        Assert.Equal(2, affix.Operation);
        Assert.Equal(ParagonGlyphAffixOperation.NodeAmplification, affix.OperationKind);
        Assert.Equal(500.0, affix.DisplayFactor);
        Assert.Null(affix.LinkedPowerSnoId);
        Assert.Null(affix.AffectedRarityKind);
        Assert.Equal(2, affix.AffectedAttributes.Count);
        // Structural: first entry carries no tag param, both ids decode as
        // valid registry indices. The exact AttributeIds are a content
        // snapshot — they are registry-ordinal and shift as DataAttributes
        // grows (1120 → 1123 in Season 14) — asserted in
        // Season_content_anchors_pinned_to_build_3_1_1 (see FR-C27).
        Assert.False(affix.AffectedAttributes[0].HasParam);
        Assert.True(affix.AffectedAttributes[0].AttributeId >= 0);
        Assert.True(affix.AffectedAttributes[1].AttributeId >= 0);
        Assert.Equal(3, affix.Tags.Count);
        // The trailing entry is the universal "ParagonGlyphAffix root"
        // anchor 0xD4A1BC54 that appears on every Op-2 affix.
        Assert.Equal(0xD4A1BC54u, affix.Tags[2]);

        // CL-84 — Op-1 affix (Nodes_BonusToMinion): the AffectedAttributes
        // VLA descriptor lives at +16/+20 instead, and carries 27 flat
        // (AttributeId, ParamPlus12) entries.
        var nodesBonus = d4.ReadParagonGlyphAffix(1031882);
        Assert.Equal(ParagonGlyphAffixOperation.Attribute, nodesBonus.OperationKind);
        Assert.Equal(100.0, nodesBonus.DisplayFactor);
        Assert.Null(nodesBonus.LinkedPowerSnoId);
        Assert.Equal(27, nodesBonus.AffectedAttributes.Count);

        // CL-84 — Op-4 affix (MultCritDmgPercent_Legendary): VLA at +104/+108,
        // 1 entry. Base/PerLevel encode fractions (×100 → percent display).
        var multCritLegendary = d4.ReadParagonGlyphAffix(2111927);
        Assert.Equal(ParagonGlyphAffixOperation.AttributeConversion,
            multCritLegendary.OperationKind);
        Assert.Equal(100.0, multCritLegendary.DisplayFactor);
        Assert.Null(multCritLegendary.LinkedPowerSnoId);
        Assert.Single(multCritLegendary.AffectedAttributes);

        // CL-84 — Op-5 affix (DamageElite__Strength_Legendary): no scalars,
        // no per-attribute VLA, linked PowerDefinition at snoPower (+88).
        var damageElite = d4.ReadParagonGlyphAffix(2098405);
        Assert.Equal(ParagonGlyphAffixOperation.Power, damageElite.OperationKind);
        Assert.Equal(1.0, damageElite.DisplayFactor);
        Assert.Equal(0f, damageElite.Base);
        Assert.Equal(0f, damageElite.PerLevel);
        Assert.Empty(damageElite.AffectedAttributes);
        Assert.NotNull(damageElite.LinkedPowerSnoId);
        Assert.Equal(2072755, damageElite.LinkedPowerSnoId);
        // The linked power lives in group 29 (PowerDefinition).
        Assert.Equal("ParagonGlyph_DamageElite",
            d4.CoreToc.GetName(SnoGroup.Power, damageElite.LinkedPowerSnoId!.Value));

        // CL-85 / FR-C28 — tag-conditional (AttributeId, ParamPlus12)
        // attribute name resolution. Anchor: Warlock_Rare_006 (sno
        // 2451111) attribute id 259 with ParamPlus12 = 0x32ABA6FB
        // (Skill_Demonology, cracked via the FR-C28 brute-force pass).
        // The compound-key map surfaces "Demonology Damage" — the
        // FR's expected example.
        Assert.Equal("Demonology Damage", d4.GetAttributeName(259, 0x32ABA6FBu));

        // CL-85 — basic-four still resolve through the single-id fast
        // path when ParamPlus12 is the no-param sentinel.
        Assert.Equal("Strength", d4.GetAttributeName(9, 0xFFFFFFFFu));
        // A compound miss falls through to the single-id lookup —
        // attr 9 + bogus GBID still returns "Strength" rather than null.
        Assert.Equal("Strength", d4.GetAttributeName(9, 0xDEADBEEFu));

        // CL-85 — the ParagonNodeStat pipeline picks up the compound
        // name on Warlock_Rare_006 (the FR-C28 anchor). 17.5% magnitude
        // already validated by CL-76; CL-85 adds the resolved StatName.
        var warlockRare = d4.Catalog.GetNodeInfo(2451111)!;
        var demonologyStat = warlockRare.Stats.First(s => s.AttributeId == 259);
        Assert.Equal("Demonology Damage", demonologyStat.StatName);
        Assert.Equal(17.5, demonologyStat.FlatValue!.Value, precision: 6);

        // CL-85 — element-keyed compound lookups (attr 254 +
        // element enum):
        Assert.Equal("Physical Damage",  d4.GetAttributeName(254, 0u));
        Assert.Equal("Fire Damage",      d4.GetAttributeName(254, 1u));
        Assert.Equal("Lightning Damage", d4.GetAttributeName(254, 2u));
        Assert.Equal("Cold Damage",      d4.GetAttributeName(254, 3u));
        Assert.Equal("Poison Damage",    d4.GetAttributeName(254, 4u));
        Assert.Equal("Shadow Damage",    d4.GetAttributeName(254, 5u));
        Assert.Equal("Holy Damage",      d4.GetAttributeName(254, 6u));

        // CL-85 — resource-keyed compound lookups (attr 161 +
        // resource enum):
        Assert.Equal("Maximum Fury",      d4.GetAttributeName(161, 1u));
        Assert.Equal("Maximum Spirit",    d4.GetAttributeName(161, 5u));
        Assert.Equal("Maximum Essence",   d4.GetAttributeName(161, 6u));

        // CL-77 / FR-C23 Option A — tooltip chrome inventory.
        // CL-80 — extended with the full multi-layer composite
        // (BaseLayer + OrnateFrame + variants + banners).
        var chrome = d4.Catalog.GetParagonTooltipChrome();
        Assert.NotNull(chrome);

        // CL-80 — the universal base + ornate frame layers populated.
        Assert.Equal(602266, chrome.BaseLayer.Sno);
        Assert.Equal("TooltipBaseBackground", chrome.BaseLayer.Name);
        Assert.Equal(602013, chrome.OrnateFrame.Sno);
        Assert.Equal("TooltipFrame", chrome.OrnateFrame.Name);
        Assert.Equal(603057, chrome.OrnateFrameLight.Sno);
        Assert.Equal("TooltipFrameLight", chrome.OrnateFrameLight.Name);
        Assert.Equal(478952, chrome.DefaultFrame.Sno);
        Assert.Equal(478948, chrome.TextFrame.Sno);

        // CL-80 — every composite layer round-trips through the
        // existing TiledStyle decoder.
        foreach (var layer in new[]
        {
            chrome.BaseLayer, chrome.OrnateFrame,
            chrome.OrnateFrameLight, chrome.DefaultFrame,
            chrome.TextFrame,
        })
        {
            Assert.True(d4.Catalog.TryGet<TiledStyleDefinition>(layer, out var td));
            Assert.NotNull(td);
        }

        // CL-80 — both banner variants present.
        Assert.Equal(2, chrome.BannerByPlacement.Count);
        Assert.Contains("Map", chrome.BannerByPlacement.Keys);
        Assert.Contains("Town", chrome.BannerByPlacement.Keys);

        // CL-81 — the inline skill-tag icon atlas surfaced + decodable.
        Assert.Equal(AssetKind.TextureAtlas, chrome.SkillIconAtlas.Kind);
        Assert.Equal(2119840, chrome.SkillIconAtlas.Sno);
        Assert.Equal("2DUI_Tooltip_Icons", chrome.SkillIconAtlas.Name);
        Assert.True(d4.Catalog.TryGet<TextureDefinition>(
            chrome.SkillIconAtlas, out var skillIconTd));
        // Structural: the atlas decodes a non-trivial frame set. The exact
        // frame count is a content snapshot (Season_content_anchors...).
        Assert.True(skillIconTd.Frames.Count >= 50,
            $"SkillIconAtlas decoded only {skillIconTd.Frames.Count} frames.");

        // CL-82 — the Center_Divider_White divider TiledStyle (the
        // Optimizer-validated structural pick on #38).
        Assert.Equal(AssetKind.TiledStyle, chrome.Divider.Kind);
        Assert.Equal(1559055, chrome.Divider.Sno);
        Assert.Equal("Center_Divider_White", chrome.Divider.Name);
        Assert.True(d4.Catalog.TryGet<TiledStyleDefinition>(
            chrome.Divider, out var dividerTd));
        Assert.NotNull(dividerTd);

        // All four paragon rarities populated on the live build,
        // each pointing at TooltipBackgroundRarity_<Rarity>.
        Assert.Equal(4, chrome.PanelByRarity.Count);
        var paragonExpected = new (ParagonRarity Rarity, int Sno, string Name)[]
        {
            (ParagonRarity.Common,    602975, "TooltipBackgroundRarity_Common"),
            (ParagonRarity.Magic,     602972, "TooltipBackgroundRarity_Magic"),
            (ParagonRarity.Rare,      602274, "TooltipBackgroundRarity_Rare"),
            (ParagonRarity.Legendary, 602942, "TooltipBackgroundRarity_Legendary"),
        };
        foreach (var (rarity, sno, name) in paragonExpected)
        {
            Assert.True(chrome.PanelByRarity.TryGetValue(rarity, out var assetRef));
            Assert.Equal(AssetKind.TiledStyle, assetRef.Kind);
            Assert.Equal(SnoGroup.UiStyle, assetRef.Group);
            Assert.Equal(sno, assetRef.Sno);
            Assert.Equal(name, assetRef.Name);
            // Round-trip decode through the existing TiledStyle reader.
            Assert.True(d4.Catalog.TryGet<TiledStyleDefinition>(assetRef, out var td));
            Assert.NotNull(td);
        }

        // Item-side rarities — future-proofing handle, all 4 present.
        Assert.Equal(4, chrome.ItemSidePanelByRarityName.Count);
        Assert.Contains("Unique", chrome.ItemSidePanelByRarityName.Keys);
        Assert.Contains("Set", chrome.ItemSidePanelByRarityName.Keys);
        Assert.Contains("Mythic", chrome.ItemSidePanelByRarityName.Keys);
        Assert.Contains("Season", chrome.ItemSidePanelByRarityName.Keys);
        Assert.All(chrome.ItemSidePanelByRarityName.Values, r =>
        {
            Assert.Equal(AssetKind.TiledStyle, r.Kind);
            Assert.Equal(SnoGroup.UiStyle, r.Group);
            Assert.StartsWith("TooltipBackgroundRarity_", r.Name, StringComparison.Ordinal);
        });

        // Cache identity — repeat call returns the same reference (the
        // Optimizer hot path).
        Assert.Same(chrome, d4.Catalog.GetParagonTooltipChrome());
    }

    /// <summary>
    /// Season-versioned CONTENT anchors — exact values Blizzard authors and
    /// re-authors each game build (registry sizes, atlas frame counts,
    /// registry-ordinal AttributeIds). A failure here is <b>expected</b> on
    /// a game update and means "content drifted → byte-verify, then
    /// re-baseline", NOT a decoder regression: the structural decode is
    /// covered by the invariant assertions in the acceptance tests. Grouped
    /// under <c>Trait kind=content-snapshot</c> (filter with
    /// <c>--filter kind=content-snapshot</c>) so a season bump surfaces as
    /// one obvious cluster. Pinned to build 3.1.1.72836 / Season 14.
    /// </summary>
    [SkippableFact]
    [Trait("kind", "content-snapshot")]
    public void Season_content_anchors_pinned_to_build_3_1_1()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        // Registry sizes (grow as the engine adds attributes/formulas).
        Assert.Equal(650, d4.GetStrings().Table(4080)!.Entries.Count);   // AttributeDescriptions
        Assert.Equal(1040, d4.ReadAttributeFormulas().Entries.Count);    // AttributeFormulas

        // Tooltip skill-tag icon atlas frame count.
        var chrome = d4.Catalog.GetParagonTooltipChrome();
        Assert.True(d4.Catalog.TryGet<TextureDefinition>(chrome.SkillIconAtlas, out var iconTd));
        Assert.Equal(62, iconTd.Frames.Count);

        // Glyph-affix 1068542 (DamageWhileHealthy_Intelligence_Side)
        // AffectedAttributes ids — AttributeId is registry-ordinal, so the
        // high id shifts as DataAttributes grows (1120 → 1123 in Season 14;
        // FR-C27's registry decode is what fixes that instability).
        var affix = d4.ReadParagonGlyphAffix(1068542);
        Assert.Equal(1123, affix.AffectedAttributes[0].AttributeId);
        Assert.Equal(10, affix.AffectedAttributes[1].AttributeId);
    }

    // --- FR-C29 per-class Character-Sheet stat model ----------------------

    /// <summary>
    /// FR-C29 structural invariants: every real class decodes to a valid
    /// core→bonus map (a primary/crit/resource core) and exactly seven
    /// conversions — the four universal signatures on their fixed cores plus
    /// the three mobile bonuses on the decoded per-class cores, each carrying
    /// its universal coefficient. Placeholder records carry no map. This does
    /// not assert any class's <em>specific</em> mapping (that's the
    /// content-snapshot below), so a class redesign surfaces there, not here.
    /// </summary>
    [SkippableFact]
    public void FR_C29_class_stat_conversion_map_is_structural()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var classes = d4.ReadCharacterClasses();
        Assert.True(classes.Count >= 5, $"expected the class roster, got {classes.Count}");

        foreach (var c in classes)
        {
            var pc = d4.ReadPlayerClass(c.SnoId);

            Assert.NotNull(pc.PrimaryAttribute);
            Assert.NotNull(pc.CriticalStrikeAttribute);
            Assert.NotNull(pc.ResourceGenerationAttribute);
            Assert.Equal(7, pc.StatConversions.Count);

            // Four universal signatures on their fixed cores.
            AssertConversion(pc, CoreStat.Strength, DerivedStat.Armor,
                CharacterStatModel.ArmorPerStrength, ConversionUnit.Flat);
            AssertConversion(pc, CoreStat.Intelligence, DerivedStat.ResistanceAllElements,
                CharacterStatModel.ResistanceAllElementsPerIntelligence, ConversionUnit.Flat);
            AssertConversion(pc, CoreStat.Willpower, DerivedStat.HealingReceived,
                CharacterStatModel.HealingReceivedPercentPerWillpower, ConversionUnit.Percent);
            AssertConversion(pc, CoreStat.Dexterity, DerivedStat.DodgeChance,
                CharacterStatModel.DodgeChancePercentPerDexterity, ConversionUnit.Percent);

            // Three mobile bonuses on the decoded per-class cores.
            AssertConversion(pc, pc.PrimaryAttribute!.Value, DerivedStat.SkillDamage,
                CharacterStatModel.SkillDamagePercentPerPrimary, ConversionUnit.Percent);
            AssertConversion(pc, pc.CriticalStrikeAttribute!.Value, DerivedStat.CriticalStrikeChance,
                CharacterStatModel.CriticalStrikeChancePercentPerPoint, ConversionUnit.Percent);
            AssertConversion(pc, pc.ResourceGenerationAttribute!.Value, DerivedStat.ResourceGeneration,
                CharacterStatModel.ResourceGenerationPercentPerPoint, ConversionUnit.Percent);
        }

        // Placeholder "Axe Bad Data" record carries no valid map.
        var junk = d4.ReadPlayerClass(159433);
        Assert.Null(junk.PrimaryAttribute);
        Assert.Empty(junk.StatConversions);

        static void AssertConversion(PlayerClassDefinition pc, CoreStat core,
            DerivedStat stat, double perPoint, ConversionUnit unit)
        {
            var cv = Assert.Single(pc.StatConversions, x => x.Stat == stat);
            Assert.Equal(core, cv.Core);
            Assert.Equal(perPoint, cv.PerPoint);
            Assert.Equal(unit, cv.Unit);
        }
    }

    /// <summary>
    /// FR-C29 content anchors (owner core-stat tooltips, 2026-07). The exact
    /// per-class core→bonus maps for the four primary-attribute archetypes,
    /// plus coefficient round-trips against the observed values (naked +
    /// high-Paragon Warlock). A class rebalance or a rate change surfaces here.
    /// </summary>
    [SkippableFact]
    [Trait("kind", "content-snapshot")]
    public void FR_C29_class_maps_and_coefficients_pinned_to_build_3_1_1()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        // One class per primary-attribute archetype (owner-oracle validated).
        AssertMap(d4, 2207749, CoreStat.Willpower, CoreStat.Strength, CoreStat.Intelligence);   // Warlock
        AssertMap(d4, 199275, CoreStat.Dexterity, CoreStat.Intelligence, CoreStat.Strength);    // Rogue
        AssertMap(d4, 199277, CoreStat.Intelligence, CoreStat.Dexterity, CoreStat.Willpower);   // Necromancer
        AssertMap(d4, 169776, CoreStat.Strength, CoreStat.Dexterity, CoreStat.Willpower);       // Barbarian

        // Coefficients reproduce the owner oracles (display rounds to 1 dp).
        Assert.Equal(152.0, CharacterStatModel.ArmorPerStrength * 76, 3);                  // Str 76 → Armor 152
        Assert.Equal(372.0, CharacterStatModel.ResistanceAllElementsPerIntelligence * 930, 3); // Int 930 → 372
        Assert.InRange(CharacterStatModel.SkillDamagePercentPerPrimary * 79, 9.8, 9.95);   // Will 79 → 9.9
        Assert.InRange(CharacterStatModel.SkillDamagePercentPerPrimary * 1876, 234.0, 235.0); // Paragon Will 1876 → 234.6
        Assert.InRange(CharacterStatModel.DodgeChancePercentPerDexterity * 616, 3.6, 3.8); // Dex 616 → 3.7
        Assert.InRange(CharacterStatModel.CriticalStrikeChancePercentPerPoint * 801, 1.9, 2.1); // Str 801 → 2.0
        Assert.InRange(CharacterStatModel.ResourceGenerationPercentPerPoint * 930, 4.5, 4.8); // Int 930 → 4.7

        static void AssertMap(Diablo4Storage d4, int sno,
            CoreStat primary, CoreStat crit, CoreStat resource)
        {
            var pc = d4.ReadPlayerClass(sno);
            Assert.Equal(primary, pc.PrimaryAttribute);
            Assert.Equal(crit, pc.CriticalStrikeAttribute);
            Assert.Equal(resource, pc.ResourceGenerationAttribute);
        }
    }

    // --- LIB-1 gear/item taxonomy -----------------------------------------

    /// <summary>
    /// LIB-1 structural invariants: item base types classify into weapon /
    /// armor / jewelry / charm from the record fields; the item→type link
    /// resolves; and the category enumeration is self-consistent. Exact
    /// per-category membership counts are the content-snapshot below.
    /// </summary>
    [SkippableFact]
    public void LIB1_item_type_classification_is_structural()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        // Known base types classify structurally (from the record, not the name).
        Assert.Equal(ItemClass.Weapon, d4.ReadItemType(446796).Class);   // Sword
        Assert.Equal(ItemClass.Weapon, d4.ReadItemType(446801).Class);   // Axe
        Assert.Equal(ItemClass.Weapon, d4.ReadItemType(446823).Class);   // Bow
        Assert.Equal(ItemClass.Armor, d4.ReadItemType(446829).Class);    // ChestArmor
        Assert.Equal(ItemClass.Armor, d4.ReadItemType(446830).Class);    // Helm
        Assert.Equal(ItemClass.Jewelry, d4.ReadItemType(446837).Class);  // Amulet
        Assert.Equal(ItemClass.Jewelry, d4.ReadItemType(446836).Class);  // Ring
        Assert.Equal(ItemClass.Charm, d4.ReadItemType(2288901).Class);   // Charm
        Assert.Equal(ItemClass.Other, d4.ReadItemType(446845).Class);    // HealthPotion
        Assert.Equal(ItemClass.Other, d4.ReadItemType(446846).Class);    // Gold

        var sword = d4.ReadItemType(446796);
        Assert.Equal("Sword", sword.Name);
        Assert.True(sword.IsEquippable);
        Assert.True(sword.WeaponFamily >= 0);
        Assert.Equal(-1, d4.ReadItemType(446837).WeaponFamily);          // Amulet: not a weapon

        // Enumeration invariants.
        var types = d4.EnumerateItemTypes().ToList();
        Assert.True(types.Count > 100);
        Assert.All(types.Where(t => t.Class != ItemClass.Other), t => Assert.True(t.IsEquippable));
        var jewelry = types.Where(t => t.Class == ItemClass.Jewelry).Select(t => t.Name).OrderBy(n => n);
        Assert.Equal(ExpectedJewelryTypes, jewelry);
        var armor = types.Where(t => t.Class == ItemClass.Armor).Select(t => t.Name).ToList();
        foreach (var a in ExpectedArmorTypes)
            Assert.Contains(a, armor);

        // Item→type link + classifying a real item.
        var chest = d4.ReadItem(52095);                                  // Chest_Normal_Generic_001
        Assert.Equal(446829, chest.ItemTypeSnoId);
        Assert.Equal(ItemClass.Armor, d4.ReadItemType(chest.ItemTypeSnoId).Class);
        Assert.Equal(446796, d4.ReadItem(591450).ItemTypeSnoId);         // 1HSword_Legendary → Sword

        // EnumerateItems(category): the first weapon really resolves to a weapon.
        var firstWeapon = d4.EnumerateItems(ItemClass.Weapon).FirstOrDefault();
        Assert.NotNull(firstWeapon);
        Assert.Equal(ItemClass.Weapon, d4.ReadItemType(firstWeapon!.ItemTypeSnoId).Class);
    }

    /// <summary>
    /// LIB-1 content anchors: exact per-category base-type counts. A count
    /// change means Blizzard added/removed an item type — re-baseline, not a
    /// decoder regression. Pinned to build 3.1.1.72836.
    /// </summary>
    [SkippableFact]
    [Trait("kind", "content-snapshot")]
    public void LIB1_item_type_category_counts_pinned_to_build_3_1_1()
    {
        var install = Install();
        Skip.If(install is null, "No Diablo IV install available.");
        using var d4 = Diablo4Storage.Open(install!);

        var byClass = d4.EnumerateItemTypes().GroupBy(t => t.Class)
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(28, byClass[ItemClass.Weapon]);
        Assert.Equal(5, byClass[ItemClass.Armor]);
        Assert.Equal(2, byClass[ItemClass.Jewelry]);
        Assert.Equal(1, byClass[ItemClass.Charm]);
    }

    // --- LIB-2 install auto-detection -------------------------------------

    /// <summary>
    /// LIB-2: <see cref="Diablo4Storage.TryLocateInstall"/> finds a real CASC
    /// install (via the env override or the Windows registry) and the no-arg
    /// <see cref="Diablo4Storage.Open()"/> opens it end-to-end. Skips when no
    /// install is auto-detectable (e.g. CI).
    /// </summary>
    [SkippableFact]
    public void LIB2_auto_detects_install_and_opens()
    {
        Skip.If(!Diablo4Storage.TryLocateInstall(out var path),
            "No auto-detectable Diablo IV install.");
        Assert.True(File.Exists(Path.Combine(path!, ".build.info")));

        using var d4 = Diablo4Storage.Open();          // no-arg auto-detect
        Assert.True(d4.CoreToc.Entries.Count > 100000);
    }
}
