using System.Diagnostics.CodeAnalysis;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A single stat effect of an item/aspect <see cref="AffixDefinition"/> — the
/// <c>(AttributeId, ParamPlus12)</c> pair identifying <b>which attribute the
/// affix modifies</b>, plus the resolved localized <see cref="AttributeName"/>.
/// One affix carries one <see cref="AffixEffect"/> per modifier (a
/// single-stat affix has one; a dual affix — e.g. a two-element resistance —
/// has one per element).
/// </summary>
/// <remarks>
/// <para>Decoded from the affix's <c>arModifiers</c>
/// <c>DT_VARIABLEARRAY</c> at payload <c>+0xB0</c> (descriptor
/// <c>dataOff@+0xB0</c> / <c>byteSize@+0xB4</c>), which is an array of
/// fixed 104-byte modifier records (<c>count = byteSize / 104</c>). Within
/// each record the modified attribute is at slot <c>idx4</c> (byte
/// <c>+16</c>) and its parameter at slot <c>idx7</c> (byte <c>+28</c>); the
/// remaining slots (the <c>~472..640</c> ids at <c>idx10/14/20/24</c> with
/// their <c>2/4/12</c> params and the family-shared GBID at <c>idx16</c>)
/// are the engine's magnitude-formula slots, shared across every affix of a
/// family and therefore <i>not</i> stat identity — see
/// <c>casc-diablo4-format.md §11.5</c> (Appendix A CL-92).</para>
/// <para><b>AttributeId space.</b> The <see cref="AttributeId"/> is the same
/// runtime <c>eAttribute</c> id resolved by
/// <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/> — e.g.
/// <c>275 → "Critical Strike Chance"</c>, <c>482 → "Armor"</c>,
/// <c>142 → "Maximum Life"</c>. Unlike the coarser power-budget category on
/// a <see cref="ParagonNodeDefinition"/>, the affix id is the <i>specific</i>
/// stat (<c>482 = Armor%</c> and <c>1125 = Damage Reduction</c> are distinct
/// affix ids even though nodes lump both under one budget category).</para>
/// <para><b>Magnitude / operation.</b> The rolled magnitude (min/max value
/// range) and the additive-vs-multiplicative operation are <i>not</i> literal
/// fields of the affix record for the bulk of stat affixes — they are
/// item-power-curve driven by the engine (the operation is implied by the
/// attribute identity, e.g. a <c>Multiplicative_*</c> / <c>_Percent</c>
/// attribute). Surfacing those stays with the consumer per the durable
/// library boundary (<c>casc-diablo4-format.md</c> Appendix C); this type
/// answers the "which attribute(s)" question (LIB-3 slice 1).</para>
/// </remarks>
/// <param name="AttributeId">The modified attribute id (slot <c>idx4</c>).
/// <b>Two namespaces, selected by the high bit</b> (verified CL-92): a
/// <b>positive</b> id is a runtime engine <c>eAttribute</c> — feed it to
/// <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/> for the
/// localized display name (e.g. <c>482 → "Armor"</c>). A <b>negative</b> id
/// (high bit <c>0x80000000</c> set) is a reference into the data-defined
/// <c>DataAttributes</c> designer table (SNO <c>1907204</c>) by
/// <b>ordinal</b> <c>AttributeId &amp; 0x7FFFFFFF</c> — these are the
/// conditional/seasonal/per-power attributes (e.g. ordinal <c>84 =
/// Barb_Berserking_AttackSpeed</c>); see
/// <see cref="IsDataDefinedAttribute"/>. The two namespaces are
/// <b>disjoint</b> — never take the absolute value (negative-208 is a
/// different attribute from positive-208). Data-defined ids do not resolve
/// through <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/>
/// (a different table); their <see cref="AttributeName"/> is left empty in
/// this slice — the raw token is available on the affix's own
/// <see cref="AffixDefinition.Description"/> placeholder, and full
/// DataAttributes name resolution is the FR-C27 registry frontier.</param>
/// <param name="ParamPlus12">The attribute parameter (slot <c>idx7</c>):
/// <see cref="NoParam"/> (<c>0xFFFFFFFF</c>) when the attribute is
/// parameter-agnostic; a small enum for parametric attributes (e.g. the
/// element on a single-resistance modifier — cold/lightning/poison); or a
/// skill-tag GBID on tag-conditional attributes (e.g.
/// <c>AttributeId 259 = Damage per Skill Tag</c>). Resolve the tag-specific
/// name via <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/>
/// and filter unset slots with <see cref="HasParam"/>.</param>
/// <param name="AttributeName">The resolved localized attribute display name
/// (via <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/>), or
/// <see cref="string.Empty"/> when the id is unresolved or the affix was
/// decoded byte-only via <see cref="AffixDefinition.Parse(System.ReadOnlySpan{byte})"/>.</param>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "\"Attribute\" is the established Diablo IV domain term " +
        "(the serialized eAttribute field). This is a data record struct, not " +
        "a System.Attribute; renaming would diverge the code from the canonical " +
        "byte-format vocabulary.")]
public readonly record struct AffixEffect(
    int AttributeId, uint ParamPlus12, string AttributeName)
{
    /// <summary>The <see cref="ParamPlus12"/> sentinel meaning "no
    /// associated parameter / skill-tag".</summary>
    public const uint NoParam = 0xFFFFFFFF;

    /// <summary>True when this effect carries a real parameter in
    /// <see cref="ParamPlus12"/> (a parametric or tag-conditional
    /// attribute). False when the slot is the <see cref="NoParam"/>
    /// sentinel.</summary>
    public bool HasParam => ParamPlus12 != NoParam;

    /// <summary>CL-92 — true when <see cref="AttributeId"/> is a reference
    /// into the data-defined <c>DataAttributes</c> designer table
    /// (SNO <c>1907204</c>) rather than the runtime engine
    /// <c>eAttribute</c> registry. Selected by the high bit
    /// (<c>0x80000000</c>); the table ordinal is then
    /// <see cref="DataAttributeOrdinal"/>. These are the
    /// conditional/seasonal attributes (Berserking, kill-streak, per-power
    /// bonuses); they do not resolve through
    /// <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/>
    /// (a disjoint namespace).</summary>
    public bool IsDataDefinedAttribute => AttributeId < 0;

    /// <summary>CL-92 — the <c>DataAttributes</c> (SNO <c>1907204</c>) table
    /// ordinal when <see cref="IsDataDefinedAttribute"/> is
    /// <see langword="true"/> (<c>AttributeId &amp; 0x7FFFFFFF</c>); otherwise
    /// the <see cref="AttributeId"/> itself (an engine attribute is already
    /// its own id). Provided so consumers can index the DataAttributes table
    /// without re-deriving the flag — and to make explicit that the id must
    /// never be <c>abs()</c>-ed (the two namespaces overlap numerically).</summary>
    public int DataAttributeOrdinal => AttributeId & 0x7FFFFFFF;
}
