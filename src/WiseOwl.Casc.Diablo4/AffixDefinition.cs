using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>AffixDefinition</c> (<c>.aff</c>, SNO group
/// <see cref="SnoGroup.Affix"/> = 104) — an item/aspect/charm affix.
/// Identity, localized <see cref="Name"/>/<see cref="Description"/>, and the
/// <see cref="Effects"/> — the attribute(s) the affix modifies. The rolled
/// magnitude (min/max value range) and the additive-vs-multiplicative
/// operation are not literal fields of the record for the bulk of stat
/// affixes (they are item-power-curve driven by the engine); modeling those
/// stays the consumer's domain per <c>casc-diablo4-format.md</c> Appendix C.
/// </summary>
/// <remarks>
/// <para><see cref="SnoId"/> is the binary field (payload <c>0</c>). The
/// localized <see cref="Name"/> and <see cref="Description"/> are both
/// resolved from the affix's <b>sibling StringList table</b>
/// (<c>docs/casc-diablo4-format.md §11.3</c>, Appendix A CL-87 / CL-22 /
/// CL-20): group-42 SNO <c>"Affix_" + snoName</c>, labels <c>Name</c>
/// (the display name, e.g. <c>"of Limitless Rage"</c>) and <c>Desc</c>
/// (the rules text, carrying D4 markup like <c>[Affix_Value_1|%|]</c>).
/// Each field is <see cref="string.Empty"/> (honest sentinel) when decoded
/// byte-only, when there is no sibling table, or when that specific label
/// is absent — many system/internal affixes carry a <c>Desc</c> but no
/// <c>Name</c>. The consumer owns any fallback and any <c>"Aspect"</c>
/// composition around the raw display name.</para>
/// <para><b>Effects (CL-92).</b> <see cref="Effects"/> is decoded from the
/// <c>arModifiers</c> <c>DT_VARIABLEARRAY</c> at payload <c>+0xB0</c> — an
/// array of fixed 104-byte modifier records (see <see cref="AffixEffect"/>
/// for the per-record layout). Each element names one modified attribute
/// (<see cref="AffixEffect.AttributeId"/> + <see cref="AffixEffect.ParamPlus12"/>);
/// the resolved <see cref="AffixEffect.AttributeName"/> is populated by
/// <see cref="Diablo4Storage.ReadAffix(int, string)"/> and empty on the
/// byte-only <see cref="Parse(ReadOnlySpan{byte})"/>. Empty (never
/// <see langword="null"/>) when the modifier array is absent or malformed
/// (confidence-gated: the descriptor must be well-formed and its byte size
/// an exact multiple of the 104-byte stride).</para>
/// </remarks>
public sealed class AffixDefinition
{
    /// <summary>Payload offset of the <c>arModifiers</c>
    /// <c>DT_VARIABLEARRAY</c> descriptor (<c>dataOff@+0xB0</c> /
    /// <c>byteSize@+0xB4</c>).</summary>
    private const int ModifierDescriptorOffset = 0xB0;

    /// <summary>Byte size of one modifier record within the array.</summary>
    private const int ModifierStride = 104;

    /// <summary>Byte offset of the modified <c>eAttribute</c> id (slot
    /// <c>idx4</c>) within a modifier record.</summary>
    private const int ModifierAttributeIdOffset = 16;

    /// <summary>Byte offset of the attribute parameter (slot <c>idx7</c>)
    /// within a modifier record.</summary>
    private const int ModifierParamOffset = 28;

    private readonly AffixEffect[] _effects;

    private AffixDefinition(int snoId, AffixEffect[] effects)
    {
        SnoId = snoId;
        _effects = effects;
    }

    /// <summary>The affix's own SNO id (== the CoreTOC id).</summary>
    public int SnoId { get; }

    /// <summary>Localized affix display name (sibling label <c>Name</c>;
    /// the raw authored fragment, e.g. <c>"Bear Clan Berserker's"</c>,
    /// <c>"of Limitless Rage"</c>, <c>"Devilish"</c>), or
    /// <see cref="string.Empty"/> when the affix has no sibling <c>Name</c>
    /// (system/internal affixes). The consumer owns any <c>"Aspect …"</c>
    /// composition.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Localized affix description (sibling label <c>Desc</c>;
    /// raw D4 markup intact), or <see cref="string.Empty"/>.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>CL-92 (LIB-3) — the attribute(s) this affix modifies, one
    /// <see cref="AffixEffect"/> per <c>arModifiers</c> entry (a single-stat
    /// affix has one; a dual affix such as a two-element resistance has one
    /// per element). Each carries the <c>eAttribute</c> id + parameter and,
    /// after a full <see cref="Diablo4Storage.ReadAffix(int, string)"/>, the
    /// resolved localized <see cref="AffixEffect.AttributeName"/>. Empty
    /// (never <see langword="null"/>) when the affix authors no modifier
    /// array or the descriptor is malformed. See <see cref="AffixEffect"/>
    /// for the byte layout and the magnitude/operation boundary.</summary>
    public IReadOnlyList<AffixEffect> Effects => _effects;

    /// <summary>Decode an Affix from its raw SNO blob (identity + the
    /// structural <see cref="Effects"/>; the localized fields need
    /// <see cref="CoreToc"/> — use
    /// <see cref="Diablo4Storage.ReadAffix(int,string)"/>). On the byte-only
    /// path each effect's <see cref="AffixEffect.AttributeName"/> is
    /// <see cref="string.Empty"/>.</summary>
    public static AffixDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        return new AffixDefinition(r.SnoId, ReadEffects(r));
    }

    private static AffixEffect[] ReadEffects(SnoRecord r)
    {
        if (r.PayloadBase + ModifierDescriptorOffset + 8 > r.Length) return [];
        int dataOff = r.I32(ModifierDescriptorOffset);
        int byteSize = r.I32(ModifierDescriptorOffset + 4);
        if (dataOff <= 0 || byteSize <= 0 || byteSize % ModifierStride != 0) return [];
        if (r.PayloadBase + dataOff + byteSize > r.Length) return [];

        int count = byteSize / ModifierStride;
        var effects = new List<AffixEffect>(count);
        for (int i = 0; i < count; i++)
        {
            int baseOff = dataOff + i * ModifierStride;
            int attributeId = r.I32(baseOff + ModifierAttributeIdOffset);
            // idx4 sentinels carry no modified attribute: 0 is an empty/padding
            // slot; -1 (0xFFFFFFFF) is the explicit "no attribute" marker (e.g.
            // a socket-restriction marker modifier). Skip both; every real
            // effect references a positive engine AttributeId or a negative
            // (high-bit-flagged) DataAttributes ordinal — see AffixEffect.
            if (attributeId is 0 or -1) continue;
            uint param = r.U32(baseOff + ModifierParamOffset);
            effects.Add(new AffixEffect(attributeId, param, string.Empty));
        }
        return effects.Count == 0 ? [] : effects.ToArray();
    }

    internal void SetName(string name) =>
        Name = name;

    internal void SetDescription(string description) =>
        Description = description;

    /// <summary>Attach the resolved localized attribute names to each
    /// <see cref="AffixEffect"/> (internal — invoked by
    /// <see cref="Diablo4Storage.ReadAffix(int, string)"/> with a
    /// <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/>-backed
    /// resolver). A <see langword="null"/> resolution collapses to
    /// <see cref="string.Empty"/> (honest sentinel).</summary>
    internal void ResolveEffectNames(Func<int, uint, string?> resolver)
    {
        for (int i = 0; i < _effects.Length; i++)
        {
            var e = _effects[i];
            _effects[i] = e with
            {
                AttributeName = resolver(e.AttributeId, e.ParamPlus12) ?? string.Empty,
            };
        }
    }
}
