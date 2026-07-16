namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A Diablo IV core attribute — the four primary stats every character has.
/// The integer value is the game's internal core-attribute index (the value
/// stored in the <see cref="PlayerClassDefinition"/> stat-conversion arrays).
/// </summary>
public enum CoreStat
{
    /// <summary>Strength (core index 0).</summary>
    Strength = 0,

    /// <summary>Intelligence (core index 1).</summary>
    Intelligence = 1,

    /// <summary>Willpower (core index 2).</summary>
    Willpower = 2,

    /// <summary>Dexterity (core index 3).</summary>
    Dexterity = 3,
}

/// <summary>
/// A Character-Sheet stat that a <see cref="CoreStat"/> contributes to
/// via a per-point conversion. The four "signature" stats
/// (<see cref="Armor"/>/<see cref="ResistanceAllElements"/>/<see cref="HealingReceived"/>/<see cref="DodgeChance"/>)
/// are bound to a fixed core for every class; the three "mobile" stats
/// (<see cref="SkillDamage"/>/<see cref="CriticalStrikeChance"/>/<see cref="ResourceGeneration"/>)
/// are bound to a per-class core named by the class record (see
/// <see cref="PlayerClassDefinition.StatConversions"/>).
/// </summary>
public enum DerivedStat
{
    /// <summary>Armor — always from <see cref="CoreStat.Strength"/>.</summary>
    Armor,

    /// <summary>Resistance to All Elements — always from <see cref="CoreStat.Intelligence"/>.</summary>
    ResistanceAllElements,

    /// <summary>Healing Received — always from <see cref="CoreStat.Willpower"/>.</summary>
    HealingReceived,

    /// <summary>Dodge Chance — always from <see cref="CoreStat.Dexterity"/>.</summary>
    DodgeChance,

    /// <summary>Skill Damage — from the class's <see cref="PlayerClassDefinition.PrimaryAttribute"/>.</summary>
    SkillDamage,

    /// <summary>Critical Strike Chance — from the class's <see cref="PlayerClassDefinition.CriticalStrikeAttribute"/>.</summary>
    CriticalStrikeChance,

    /// <summary>Resource Generation — from the class's <see cref="PlayerClassDefinition.ResourceGenerationAttribute"/>.</summary>
    ResourceGeneration,
}

/// <summary>The unit of a <see cref="CoreStatConversion.PerPoint"/> coefficient.</summary>
public enum ConversionUnit
{
    /// <summary>A flat additive amount (e.g. Armor, Resistance points).</summary>
    Flat,

    /// <summary>A percentage-point amount (e.g. Skill Damage %, Dodge %).</summary>
    Percent,
}

/// <summary>
/// One core-attribute → derived-stat conversion: how many units of
/// <see cref="Stat"/> each point of <see cref="Core"/> grants. The
/// coefficient is a universal engine constant (identical for every class —
/// only the core→stat <em>mapping</em> is per-class).
/// </summary>
/// <param name="Core">The source core attribute.</param>
/// <param name="Stat">The derived stat it feeds.</param>
/// <param name="PerPoint">Units of <paramref name="Stat"/> per point of
/// <paramref name="Core"/> (interpret with <paramref name="Unit"/>).</param>
/// <param name="Unit">Whether <paramref name="PerPoint"/> is flat or a percentage.</param>
public sealed record CoreStatConversion(CoreStat Core, DerivedStat Stat, double PerPoint, ConversionUnit Unit);

/// <summary>
/// Universal (class-independent) constants for the Diablo IV Character-Sheet
/// stat model (FR-C29). The per-point core→stat conversion coefficients and the
/// inherent base stats were <b>not located in any searched SNO source</b> (a
/// thorough data-mine across every candidate GameBalance/class source found no
/// coefficient home — see <c>docs/casc-diablo4-format.md §12</c>): they are
/// either engine-side or in a global config not yet identified. They are
/// <b>universal</b> — identical for every class, so a newly-added class reuses
/// them and supplies only its own data-driven core→stat map (which is why they
/// need not be per-class data). They are baked here as owner-oracle-validated
/// constants (the engine-constants pattern; precedent CL-68 / CL-83), pinned
/// against live core-stat tooltips for four classes spanning all four
/// primary-attribute archetypes (Warlock, Rogue, Necromancer, Barbarian),
/// including a high-Paragon capture that fixed the small-magnitude rates to
/// three significant figures.
/// </summary>
/// <remarks>
/// The per-class core→stat <em>mapping</em> (which core feeds Skill Damage /
/// Critical Strike Chance / Resource Generation) <b>is</b> data-driven and is
/// decoded structurally per class by <see cref="PlayerClassDefinition"/> — no
/// class table is hard-coded. Re-verify trigger (Appendix D): a new game build
/// whose core-stat tooltips disagree with these rates.
/// </remarks>
public static class CharacterStatModel
{
    /// <summary>Armor granted per point of Strength (flat).</summary>
    public const double ArmorPerStrength = 2.0;

    /// <summary>Resistance to All Elements granted per point of Intelligence (flat).</summary>
    public const double ResistanceAllElementsPerIntelligence = 0.4;

    /// <summary>Skill Damage granted per point of the primary attribute (percent).</summary>
    public const double SkillDamagePercentPerPrimary = 0.125;

    /// <summary>Critical Strike Chance granted per point of the crit attribute (percent).</summary>
    public const double CriticalStrikeChancePercentPerPoint = 0.0025;

    /// <summary>Resource Generation granted per point of the resource attribute (percent).</summary>
    public const double ResourceGenerationPercentPerPoint = 0.005;

    /// <summary>Healing Received granted per point of Willpower (percent).</summary>
    public const double HealingReceivedPercentPerWillpower = 0.035;

    /// <summary>Dodge Chance granted per point of Dexterity (percent).</summary>
    public const double DodgeChancePercentPerDexterity = 0.006;

    /// <summary>Inherent base Critical Strike Chance before any attribute/gear/Paragon (percent).</summary>
    public const double BaseCriticalStrikeChancePercent = 5.0;

    /// <summary>Inherent base Critical Strike Damage (percent).</summary>
    public const double BaseCriticalStrikeDamagePercent = 50.0;

    /// <summary>Inherent base Vulnerable Damage (percent).</summary>
    public const double BaseVulnerableDamagePercent = 20.0;

    /// <summary>Inherent base Movement Speed (percent).</summary>
    public const double BaseMovementSpeedPercent = 100.0;
}
