using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>PlayerClassDefinition</c> (<c>.prd</c>, SNO group
/// <see cref="SnoGroup.PlayerClass"/> = 74) — the playable character class
/// record. Exposes identity (<see cref="SnoId"/>/<see cref="EClass"/>) plus the
/// per-class Character-Sheet stat-conversion map (FR-C29): which core attribute
/// feeds each derived stat. (The class roster + localized names are
/// <see cref="Diablo4Storage.ReadCharacterClasses"/>.)
/// </summary>
/// <remarks>
/// Byte layout (clean-room, <c>docs/casc-diablo4-format.md §11.1</c> / §12,
/// Appendix A CL-21): payload base <c>0x10</c>; <c>snoId</c> at payload
/// <c>0</c>; <c>eClass</c> (the game's internal class enum ordinal) at payload
/// <c>16</c>. The <c>eClass</c> ordinal is sparse but stable (Sorcerer 0,
/// Barbarian 1, Rogue 3, Druid 5, Necromancer 6, Spiritborn 7, Paladin 9,
/// Warlock 10) and is the ordering behind the glyph <c>fUsableByClass</c> rank
/// (§7.3 / FR-D3).
/// <para>
/// The stat-conversion map is three <c>DT_VARIABLEARRAY</c> descriptors at
/// payload <c>+0x40</c>/<c>+0x50</c>/<c>+0x60</c>, each a single
/// <c>(coreIndex:int32, weight:float32, …)</c> element. In slot order they name
/// the core that feeds <b>Skill Damage</b> (the primary, weight 1.25),
/// <b>Critical Strike Chance</b>, and <b>Resource Generation</b> (weight 1.0).
/// The mapping is per-class and read straight from the record — verified
/// against live core-stat tooltips for all four primary-attribute archetypes
/// (Warlock/Rogue/Necromancer/Barbarian). The per-point coefficients are
/// universal engine constants in <see cref="CharacterStatModel"/>.
/// </para>
/// </remarks>
public sealed class PlayerClassDefinition
{
    private PlayerClassDefinition(
        int snoId,
        int eClass,
        CoreStat? primary,
        CoreStat? critical,
        CoreStat? resource,
        IReadOnlyList<CoreStatConversion> conversions)
    {
        SnoId = snoId;
        EClass = eClass;
        PrimaryAttribute = primary;
        CriticalStrikeAttribute = critical;
        ResourceGenerationAttribute = resource;
        StatConversions = conversions;
    }

    /// <summary>The class's own SNO id (== the CoreTOC id; the stable
    /// per-class key shared with <see cref="CharacterClass.SnoId"/> and
    /// <see cref="ParagonBoardDefinition.ClassSnoId"/>).</summary>
    public int SnoId { get; }

    /// <summary>The game's internal class enum ordinal (<c>eClass</c>,
    /// payload <c>+16</c>). Sparse but stable; ranking the real-class
    /// roster by this value yields the glyph class-array slot order
    /// (§7.3).</summary>
    public int EClass { get; }

    /// <summary>The core attribute that grants this class its Skill Damage —
    /// the class's <em>primary</em> stat (Warlock Willpower, Rogue Dexterity,
    /// Necromancer/Sorcerer Intelligence, Barbarian/Paladin Strength, …).
    /// <see langword="null"/> for a malformed/placeholder record (e.g.
    /// <c>Axe Bad Data</c>).</summary>
    public CoreStat? PrimaryAttribute { get; }

    /// <summary>The core attribute that grants this class its Critical Strike
    /// Chance (per-class; e.g. Warlock Strength, Rogue Intelligence).
    /// <see langword="null"/> for a malformed record.</summary>
    public CoreStat? CriticalStrikeAttribute { get; }

    /// <summary>The core attribute that grants this class its Resource
    /// Generation (per-class; e.g. Warlock Intelligence, Rogue Strength).
    /// <see langword="null"/> for a malformed record.</summary>
    public CoreStat? ResourceGenerationAttribute { get; }

    /// <summary>The full per-class Character-Sheet conversion table (FR-C29):
    /// the four universal signatures (Str→Armor, Int→Resist, Will→Healing,
    /// Dex→Dodge) plus the three per-class mobile bonuses (Skill Damage / Crit
    /// / Resource Generation) mapped onto their class cores, each with its
    /// universal per-point coefficient. Empty for a malformed record.
    /// The consumer composes actual stat values from these + the core totals +
    /// the base constants in <see cref="CharacterStatModel"/>.</summary>
    public IReadOnlyList<CoreStatConversion> StatConversions { get; }

    /// <summary>Descriptor offsets (payload-relative) of the three
    /// stat-conversion arrays: [SkillDamage/primary, Crit, ResourceGen].</summary>
    private static readonly int[] ConversionArrayOffsets = [0x40, 0x50, 0x60];

    /// <summary>Decode a PlayerClass from its raw SNO blob.</summary>
    public static PlayerClassDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);

        var primary = ReadConversionCore(r, ConversionArrayOffsets[0]);
        var critical = ReadConversionCore(r, ConversionArrayOffsets[1]);
        var resource = ReadConversionCore(r, ConversionArrayOffsets[2]);

        var conversions = BuildConversions(primary, critical, resource);

        return new PlayerClassDefinition(r.SnoId, r.I32(16), primary, critical, resource, conversions);
    }

    /// <summary>Read the core index (element 0) of a stat-conversion array,
    /// or <see langword="null"/> if the descriptor is absent/out of range
    /// (a malformed/placeholder class record).</summary>
    private static CoreStat? ReadConversionCore(SnoRecord r, int descriptorOffset)
    {
        var span = r.VariableArray(descriptorOffset);
        if (span.Length < 4) return null;
        int index = BitConverter.ToInt32(span);
        return index is >= 0 and <= 3 ? (CoreStat)index : null;
    }

    /// <summary>Assemble the class conversion table: the four fixed signatures
    /// (present for every well-formed class) + the three per-class mobile
    /// bonuses (only when their core decoded). Returns empty when the record
    /// carried no valid mapping.</summary>
    private static List<CoreStatConversion> BuildConversions(
        CoreStat? primary, CoreStat? critical, CoreStat? resource)
    {
        if (primary is null && critical is null && resource is null)
            return [];

        var list = new List<CoreStatConversion>(7)
        {
            new(CoreStat.Strength, DerivedStat.Armor, CharacterStatModel.ArmorPerStrength, ConversionUnit.Flat),
            new(CoreStat.Intelligence, DerivedStat.ResistanceAllElements, CharacterStatModel.ResistanceAllElementsPerIntelligence, ConversionUnit.Flat),
            new(CoreStat.Willpower, DerivedStat.HealingReceived, CharacterStatModel.HealingReceivedPercentPerWillpower, ConversionUnit.Percent),
            new(CoreStat.Dexterity, DerivedStat.DodgeChance, CharacterStatModel.DodgeChancePercentPerDexterity, ConversionUnit.Percent),
        };

        if (primary is { } p)
            list.Add(new(p, DerivedStat.SkillDamage, CharacterStatModel.SkillDamagePercentPerPrimary, ConversionUnit.Percent));
        if (critical is { } c)
            list.Add(new(c, DerivedStat.CriticalStrikeChance, CharacterStatModel.CriticalStrikeChancePercentPerPoint, ConversionUnit.Percent));
        if (resource is { } rg)
            list.Add(new(rg, DerivedStat.ResourceGeneration, CharacterStatModel.ResourceGenerationPercentPerPoint, ConversionUnit.Percent));

        return list;
    }
}
