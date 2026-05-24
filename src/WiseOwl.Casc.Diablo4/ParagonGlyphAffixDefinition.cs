using System;
using System.Collections.Generic;
using System.Linq;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>ParagonGlyphAffixDefinition</c> (<c>.gaf</c>, SNO
/// group 112). Raw fields plus the projected slice (formula identity,
/// affected-attribute pairs, tag GBID list, linked-power ref); magnitude
/// interpretation (operation semantics, level scaling, the per-class
/// threshold gate, legendary unlock level) stays with the consumer per
/// the durable library boundary in <c>casc-diablo4-format.md</c>
/// Appendix C.
/// </summary>
/// <remarks>
/// <para>Byte layout per the canonical reference
/// (<c>docs/casc-diablo4-format.md §7.4</c>, formatHash <c>0xB460195F</c>;
/// see Appendix A CL-84 for the slice-2b RE):</para>
/// <list type="bullet">
///   <item>payload base <c>0x10</c>; <c>snoId@0</c>.</item>
///   <item><c>eAffectedNodeRarity@24</c> (<see cref="AffectedRarity"/>):
///   universally <c>0</c> across all 314 live affixes (the
///   "any rarity" sentinel — see <see cref="AffectedRarityKind"/> for
///   the typed view).</item>
///   <item><c>eBonusOperation@48</c> (<see cref="Operation"/> / typed
///   <see cref="OperationKind"/>): <c>1</c>=<see cref="ParagonGlyphAffixOperation.Attribute"/>,
///   <c>2</c>=<see cref="ParagonGlyphAffixOperation.NodeAmplification"/>,
///   <c>4</c>=<see cref="ParagonGlyphAffixOperation.AttributeConversion"/>,
///   <c>5</c>=<see cref="ParagonGlyphAffixOperation.Power"/>.</item>
///   <item><c>flStartingBonusScalar@76</c> (<see cref="Base"/>),
///   <c>flAddedBonusScalarPerLevel@80</c> (<see cref="PerLevel"/>):
///   the per-level magnitude (zero on Op-5, whose magnitude lives in the
///   <see cref="LinkedPowerSnoId"/> Power record).</item>
///   <item><c>flDisplayFactor@84</c> (<see cref="DisplayFactor"/>): per-op
///   engine constant (Op-1=100, Op-2=500, Op-4=100, Op-5=1) — surfaced
///   verbatim; the consumer's display formula determines how to apply
///   it.</item>
///   <item><c>snoPower@88</c> (<see cref="LinkedPowerSnoId"/>): group-29
///   PowerDefinition ref on Op-5 affixes; the sentinel <c>-1</c> on
///   every other op.</item>
///   <item>The <see cref="AffectedAttributes"/> <c>DT_VARIABLEARRAY</c>
///   descriptor lives at an op-dependent payload offset:
///   <c>+16/+20</c> for Op-1, <c>+64/+68</c> for Op-2, <c>+104/+108</c>
///   for Op-4 (Op-5 has no per-attribute scaling). Element stride is
///   <c>8</c> bytes — a packed <c>(int AttributeId, uint ParamPlus12)</c>
///   pair, mirrored in <see cref="GlyphAffixAttributeRef"/>.</item>
///   <item>The <see cref="Tags"/> <c>DT_VARIABLEARRAY[DT_UINT]</c>
///   descriptor lives at <c>+120/+124</c>; element stride is <c>4</c>
///   bytes (raw GBIDs).</item>
/// </list>
/// </remarks>
public sealed class ParagonGlyphAffixDefinition
{
    private const int AttributeRefStride = 8;

    /// <summary>The <c>eAffectedNodeRarity</c> sentinel meaning "any
    /// rarity" (<see cref="AffectedRarityKind"/> returns
    /// <see langword="null"/>).</summary>
    private const int AnyRaritySentinel = 0;

    /// <summary>The <c>snoPower</c> sentinel meaning "no linked
    /// power".</summary>
    private const int NoLinkedPower = -1;

    private readonly GlyphAffixAttributeRef[] _affectedAttributes;
    private readonly uint[] _tags;

    private ParagonGlyphAffixDefinition(
        int snoId, int affectedRarity, int operation,
        float @base, float perLevel, float displayFactor,
        int? linkedPowerSnoId,
        GlyphAffixAttributeRef[] affectedAttributes, uint[] tags)
    {
        SnoId = snoId;
        AffectedRarity = affectedRarity;
        Operation = operation;
        Base = @base;
        PerLevel = perLevel;
        DisplayFactor = displayFactor;
        LinkedPowerSnoId = linkedPowerSnoId;
        _affectedAttributes = affectedAttributes;
        _tags = tags;
    }

    /// <summary>The affix's own SNO id.</summary>
    public int SnoId { get; }

    /// <summary><c>eAffectedNodeRarity</c> (payload <c>+24</c>) — the raw
    /// rarity gate int. Universally <c>0</c> across every live affix
    /// in <c>3.0.2.71886</c> (the implicit "any" case); see
    /// <see cref="AffectedRarityKind"/> for the typed view.</summary>
    public int AffectedRarity { get; }

    /// <summary>FR-C24 (CL-84) — <see cref="AffectedRarity"/> as a typed
    /// nullable: <see langword="null"/> when the raw value is <c>0</c>
    /// (the "any rarity" sentinel), otherwise the corresponding
    /// <see cref="ParagonRarity"/>. The current live build only emits
    /// <c>0</c>; the typed projection is forward-compat for any future
    /// season that authors a rarity-specific affix.</summary>
    public ParagonRarity? AffectedRarityKind =>
        AffectedRarity == AnyRaritySentinel
            ? null
            : (ParagonRarity)AffectedRarity;

    /// <summary><c>eBonusOperation</c> (payload <c>+48</c>) — the raw op
    /// int (1/2/4/5). See <see cref="OperationKind"/> for the named
    /// enum.</summary>
    public int Operation { get; }

    /// <summary>FR-C24 (CL-84) — <see cref="Operation"/> as a typed
    /// enum.</summary>
    public ParagonGlyphAffixOperation OperationKind =>
        (ParagonGlyphAffixOperation)Operation;

    /// <summary><c>flStartingBonusScalar</c> (payload <c>+76</c>) — the
    /// level-1 magnitude scalar. Zero on Op-5 (the magnitude lives in
    /// the <see cref="LinkedPowerSnoId"/> Power record).</summary>
    public float Base { get; }

    /// <summary><c>flAddedBonusScalarPerLevel</c> (payload <c>+80</c>) —
    /// per-level magnitude increment. Zero on Op-5 (see
    /// <see cref="Base"/>).</summary>
    public float PerLevel { get; }

    /// <summary>FR-C24 (CL-84) — <c>flDisplayFactor</c> (payload <c>+84</c>).
    /// Surfaced verbatim as the engine encodes it. Across the live build
    /// this is a per-op constant (Op-1/Op-4=<c>100</c>, Op-2=<c>500</c>,
    /// Op-5=<c>1</c>) rather than a per-affix value; the consumer
    /// determines how it participates in the display formula (the
    /// existing assumption of "always 100" is correct only for Op-1 and
    /// Op-4). Decoded verbatim because the field is an engine-authored
    /// scalar and forward-compat: a future season can author per-affix
    /// values without breaking this surface.</summary>
    public double DisplayFactor { get; }

    /// <summary>FR-C24 (CL-84) — On <see cref="OperationKind"/> ==
    /// <see cref="ParagonGlyphAffixOperation.Power"/>, the SNO id of the
    /// linked <c>PowerDefinition</c> (group <see cref="SnoGroup.Power"/>=29)
    /// that defines the threshold chain / power-cast behavior the affix
    /// triggers. <see langword="null"/> for non-Op-5 affixes (where the
    /// underlying field carries the sentinel <c>-1</c>). The threshold
    /// magnitude itself (e.g. the per-class <c>+40 Willpower</c> gate
    /// printed on every Warlock glyph) is engine-coupled and not encoded
    /// in the <c>.gaf</c> record — see Appendix C for the boundary
    /// principle.</summary>
    public int? LinkedPowerSnoId { get; }

    /// <summary>FR-C24 (CL-84) — The AttributeIds this affix grants /
    /// modifies, paired with their <c>ParamPlus12</c> skill-tag GBIDs
    /// (the <c>ptAttributes</c> array — same shape as on a
    /// <see cref="ParagonNodeDefinition"/>, but with the trimmed
    /// 8-byte-per-entry encoding the <c>.gaf</c> record uses). Decoded
    /// from the op-specific descriptor slot (see remarks on the class).
    /// Empty on Op-5 affixes (the magnitude lives in the linked
    /// <see cref="LinkedPowerSnoId"/> Power record, not in attribute
    /// grants).</summary>
    public IReadOnlyList<GlyphAffixAttributeRef> AffectedAttributes =>
        _affectedAttributes;

    /// <summary>FR-C24 (CL-84) — Raw GBID list from the
    /// <c>+120/+124</c> <c>DT_VARIABLEARRAY[DT_UINT]</c> descriptor. The
    /// list contains the affix's classification anchors (an
    /// always-present <see cref="SnoGroup.ParagonGlyphAffix"/>-root GBID
    /// <c>0xD4A1BC54</c> on every Op-2 record; a class-attribute anchor;
    /// the per-skill-tag selector — Abyss / Archfiend / Demonology /
    /// Hellfire / Occult / etc.). The skill-tag selector is the
    /// non-anchor entry; cracked names land in
    /// <c>docs/d4-hash-dictionary.md</c> as they're recovered. Consumers
    /// can call <see cref="Diablo4.FormatFieldHash(uint)"/> on each entry
    /// to render the raw <c>0xNNNNNNNN</c> when uncracked.</summary>
    public IReadOnlyList<uint> Tags => _tags;

    /// <summary>FR-C24 (CL-79) — the affix's localized description
    /// template (e.g. <c>"For every 5 Intelligence purchased within
    /// range, you deal {c_number}+[{GlyphAffixScalar}|1%|]{/c}
    /// increased damage while {c_important}{u}Healthy{/u}{/c}."</c>)
    /// resolved via the §6.7 sibling-StringList convention
    /// (<c>ParagonGlyphAffix_&lt;AffixSnoName&gt;</c>, label
    /// <c>Desc</c>). Returned as the raw template — color tags
    /// (<c>{c_…}{/c}</c>), underline tags (<c>{u}{/u}</c>), value
    /// placeholders (<c>[{GlyphAffixScalar}|1%|]</c>), and the
    /// markup tokens the consumer renders (<c>[x]</c>, <c>[+]</c>,
    /// <c>&lt;Keyword&gt;</c>) all preserved. Empty when the
    /// sibling table is missing or the affix was decoded via the
    /// byte-only <see cref="Parse(ReadOnlySpan{byte})"/>. Populated
    /// by <see cref="Diablo4Storage.ReadParagonGlyphAffix(int, string)"/>.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Attach the localized description (internal — set by
    /// <see cref="Diablo4Storage.ReadParagonGlyphAffix(int)"/>).</summary>
    internal void SetDescription(string description) =>
        Description = description;

    /// <summary>Decode a ParagonGlyphAffix from its raw SNO blob.</summary>
    public static ParagonGlyphAffixDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        int operation = r.I32(48);
        int linkedPower = r.I32(88);
        int affectedAttrDescriptorOffset = AffectedAttributesDescriptorOffset(operation);

        return new ParagonGlyphAffixDefinition(
            snoId: r.SnoId,
            affectedRarity: r.I32(24),
            operation: operation,
            @base: r.F32(76),
            perLevel: r.F32(80),
            displayFactor: r.F32(84),
            linkedPowerSnoId: operation == (int)ParagonGlyphAffixOperation.Power
                && linkedPower != NoLinkedPower
                ? linkedPower
                : null,
            affectedAttributes: ReadAttributeRefs(r, affectedAttrDescriptorOffset),
            tags: ReadTags(r, descriptorOffset: 120));
    }

    /// <summary>The payload offset of the <c>AffectedAttributes</c>
    /// <c>DT_VARIABLEARRAY[GlyphAffixAttributeRef]</c> descriptor — per
    /// op. Op-5 (<see cref="ParagonGlyphAffixOperation.Power"/>) has no
    /// per-attribute scaling; this method returns <c>-1</c> for it.</summary>
    private static int AffectedAttributesDescriptorOffset(int operation) =>
        operation switch
        {
            (int)ParagonGlyphAffixOperation.Attribute => 16,
            (int)ParagonGlyphAffixOperation.NodeAmplification => 64,
            (int)ParagonGlyphAffixOperation.AttributeConversion => 104,
            _ => -1,
        };

    private static GlyphAffixAttributeRef[] ReadAttributeRefs(SnoRecord r, int descriptorOffset)
    {
        if (descriptorOffset < 0) return [];
        if (r.PayloadBase + descriptorOffset + 8 > r.Length) return [];
        int dataOff = r.I32(descriptorOffset);
        int dataSize = r.I32(descriptorOffset + 4);
        if (dataOff <= 0 || dataSize <= 0 || dataSize % AttributeRefStride != 0) return [];
        int count = dataSize / AttributeRefStride;
        if (r.PayloadBase + dataOff + dataSize > r.Length) return [];
        var result = new GlyphAffixAttributeRef[count];
        for (int i = 0; i < count; i++)
        {
            int entryOff = dataOff + i * AttributeRefStride;
            result[i] = new GlyphAffixAttributeRef(
                AttributeId: r.I32(entryOff),
                ParamPlus12: r.U32(entryOff + 4));
        }
        return result;
    }

    private static uint[] ReadTags(SnoRecord r, int descriptorOffset)
    {
        if (r.PayloadBase + descriptorOffset + 8 > r.Length) return [];
        int dataOff = r.I32(descriptorOffset);
        int dataSize = r.I32(descriptorOffset + 4);
        if (dataOff <= 0 || dataSize <= 0 || dataSize % 4 != 0) return [];
        int count = dataSize / 4;
        if (r.PayloadBase + dataOff + dataSize > r.Length) return [];
        var result = new uint[count];
        for (int i = 0; i < count; i++)
            result[i] = r.U32(dataOff + i * 4);
        return result;
    }
}
