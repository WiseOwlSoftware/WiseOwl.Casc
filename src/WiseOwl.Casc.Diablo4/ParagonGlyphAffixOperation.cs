namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// The kind of bonus mechanism a
/// <see cref="ParagonGlyphAffixDefinition"/> applies — the typed view of
/// the raw <c>eBonusOperation</c> int
/// (<see cref="ParagonGlyphAffixDefinition.Operation"/>) at payload
/// <c>+0x30</c>. Values match the live <c>3.0.2.71886</c> build.
/// </summary>
public enum ParagonGlyphAffixOperation
{
    /// <summary>An operation value not observed on the live build. Reserved
    /// for forward-compat (a future season ships a new op type).</summary>
    Unknown = 0,

    /// <summary>Op 1 — <c>Attribute</c>: flat attribute grants. The affix
    /// adds <see cref="ParagonGlyphAffixDefinition.Base"/> +
    /// <c>(Level-1)</c> × <see cref="ParagonGlyphAffixDefinition.PerLevel"/>
    /// to each <see cref="ParagonGlyphAffixDefinition.AffectedAttributes"/>
    /// entry. Used by node-bonus affixes (e.g.
    /// <c>Nodes_BonusToMinion</c>). The AffectedAttributes VLA descriptor
    /// for Op-1 lives at payload <c>+16/+20</c>.</summary>
    Attribute = 1,

    /// <summary>Op 2 — <c>NodeAmplification</c>: percent-scaling affix
    /// against a tagged scope (Abyss / Archfiend / Bleeding / etc.). Both
    /// <c>_Main</c> ("per 5 &lt;Attribute&gt; → +X% &lt;Tag&gt; Skill") and
    /// <c>_Side</c> ("+X%[x] vs Tag") affixes carry this op. The
    /// AffectedAttributes VLA descriptor for Op-2 lives at payload
    /// <c>+64/+68</c>.</summary>
    NodeAmplification = 2,

    /// <summary>Op 4 — <c>AttributeConversion</c>: legendary
    /// <c>Mult*_Legendary</c> additive percentage that unlocks at glyph
    /// Level 50 (engine-side gate). Base / PerLevel are stored as
    /// fractions (×100 → percent). The AffectedAttributes VLA descriptor
    /// for Op-4 lives at payload <c>+104/+108</c>.</summary>
    AttributeConversion = 4,

    /// <summary>Op 5 — <c>Power</c>: the affix references a
    /// <see cref="ParagonGlyphAffixDefinition.LinkedPowerSnoId"/> (group 29
    /// PowerDefinition) that defines the threshold chain / skill-cast
    /// behavior; <see cref="ParagonGlyphAffixDefinition.Base"/> and
    /// <see cref="ParagonGlyphAffixDefinition.PerLevel"/> are zero
    /// (the magnitude lives in the linked power, not on the
    /// affix).</summary>
    Power = 5,
}
