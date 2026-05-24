using System.Diagnostics.CodeAnalysis;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A single <c>(AttributeId, ParamPlus12)</c> entry from a glyph affix's
/// <see cref="ParagonGlyphAffixDefinition.AffectedAttributes"/> list. The
/// shape parallels the <c>ptAttributes</c> pair carried on a
/// <see cref="ParagonNodeDefinition"/> — an <c>eAttribute</c> int and a
/// <c>uParamPlus12</c> GBID — but the glyph-affix encoding is the trimmed
/// 8-byte pair only (no inline formula, no parallel attribute-GBID, no
/// <c>NParam</c>), so it is surfaced as a distinct record struct rather
/// than reusing <see cref="NodeAttribute"/>'s 88-byte node-attribute
/// shape.
/// </summary>
/// <param name="AttributeId">The <c>eAttribute</c> int (matches the
/// id used by <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/>
/// — names are resolved via that overload
/// — the compound-key overload that handles the tag-conditional cases the
/// raw id cannot disambiguate; the AttributeId is itself a power-budget
/// category, not a stat key — per the FR-C21 finding recorded in
/// <c>casc-diablo4-format.md §7.6</c>).</param>
/// <param name="ParamPlus12">The associated GBID (<c>0xFFFFFFFF</c> when
/// the attribute is tag-agnostic). On tag-conditional attribute ids — e.g.
/// <c>AttributeId 259</c> (<c>DamageBonusTag</c>) — this GBID identifies
/// the skill-tag the affix scales against (Abyss / Archfiend /
/// Demonology / etc.); the consumer can call
/// <see cref="Diablo4Storage.GetAttributeName(int, uint, string)"/> to
/// resolve the per-tag display string (the compound-key lookup in
/// <see cref="AttributeNames.LabelByCompoundKey"/>) and
/// <see cref="GlyphAffixAttributeRef.HasParam"/>
/// to filter out the unset slots.</param>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "\"Attribute\" is the established Diablo IV domain term " +
        "(the serialized eAttribute field). This is a data record struct, not " +
        "a System.Attribute; renaming would diverge the code from the canonical " +
        "byte-format vocabulary.")]
public readonly record struct GlyphAffixAttributeRef(int AttributeId, uint ParamPlus12)
{
    /// <summary>The <see cref="ParamPlus12"/> sentinel meaning "no
    /// associated skill-tag / no parameter".</summary>
    public const uint NoParam = 0xFFFFFFFF;

    /// <summary>True when this entry carries a real GBID in
    /// <see cref="ParamPlus12"/> (i.e. it is tag-conditional). False
    /// when the slot is the <see cref="NoParam"/> sentinel.</summary>
    public bool HasParam => ParamPlus12 != NoParam;
}
