using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>AffixDefinition</c> (<c>.aff</c>, SNO group
/// <see cref="SnoGroup.Affix"/> = 104) — an item/charm affix. Identity +
/// localized name and description only; affix magnitude/operation modeling
/// stays the consumer's (the glyph-affix magnitudes are
/// <see cref="ParagonGlyphAffixDefinition"/>; general item-affix
/// stat-effect modeling is a consumer domain spec — Appendix C).
/// </summary>
/// <remarks>
/// <see cref="SnoId"/> is the binary field (payload <c>0</c>). The
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
/// composition around the raw display name.
/// </remarks>
public sealed class AffixDefinition
{
    private AffixDefinition(int snoId)
    {
        SnoId = snoId;
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

    /// <summary>Decode an Affix from its raw SNO blob (identity only — the
    /// localized field needs <see cref="CoreToc"/>; use
    /// <see cref="Diablo4Storage.ReadAffix(int,string)"/>).</summary>
    public static AffixDefinition Parse(ReadOnlySpan<byte> blob) =>
        new(new SnoRecord(blob).SnoId);

    internal void SetName(string name) =>
        Name = name;

    internal void SetDescription(string description) =>
        Description = description;
}
