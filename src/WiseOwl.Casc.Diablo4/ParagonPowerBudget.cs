using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// The six budget-multiplier intrinsics that paragon-node magnitude
/// formulas multiply against. Empirically pinned (clean-room) from
/// owner-validated in-game readings on build <c>3.0.2.71886</c>; the
/// engine implements these as formula-DSL intrinsic functions
/// (<c>ParagonPowerBudgetMultiplierNode&lt;Rarity&gt;{Major|Minor}&lt;Off|Def&gt;()</c>)
/// that are <b>absent from every shipped GameBalance data table</b> —
/// not in <see cref="AttributeFormulaTable"/> (sno <c>201912</c>), not in
/// any <c>PowerFormulaTables</c>, and no SNO carries "Budget" in its
/// name. So the library bakes them as a calibration table.
/// </summary>
/// <remarks>
/// <para>
/// The magnitude model (FR-C21, <c>docs/casc-diablo4-format.md</c> §7.2
/// + Appendix C): a paragon stat node's displayed magnitude is
/// <c>formula-constant × budget-multiplier</c>. Worked verifications
/// against in-game readings:
/// </para>
/// <list type="bullet">
///   <item><c>Generic_Magic_Armor</c>: <c>0.75 × MagicDefensive (10) = 7.5%</c> ✓</item>
///   <item><c>Generic_Magic_DamageToElite</c>: <c>3 × MagicOffensive (2.5) = 7.5%</c> ✓</item>
///   <item><c>Generic_Rare_AllResistance</c>: <c>0.75 × RareMajorDefensive (4) = 3.0%</c> ✓</item>
///   <item><c>Generic_Rare_MaxLife</c>: <c>1 × RareMajorDefensive (4) = 4.0%</c> ✓</item>
///   <item><c>Generic_Rare_Damage</c>: <c>2 × RareMajorOffensive (5) = 10%</c> ✓</item>
///   <item><c>Warlock_Rare_006</c> (Demonology tag): <c>3.5 × RareMajorOffensive (5) = 17.5%</c> ✓</item>
///   <item><c>Generic_Rare_CriticalDamage</c>: <c>3 × RareMinorOffensive (5) = 15%</c> ✓</item>
/// </list>
/// <para>
/// <b>Re-verification.</b> If a future Diablo IV build appears to
/// disagree with displayed magnitudes, the cause is one of: (a) the
/// per-node formula-constant changed (re-decode the
/// <see cref="NodeAttribute"/> / <see cref="AttributeFormulaTable"/>
/// entry), or (b) the engine retuned a multiplier. The library has no
/// way to read the multipliers from data — pin them empirically and
/// document the build they were last validated against here.
/// </para>
/// <para>
/// <b>Normal-rarity nodes</b> (<see cref="ParagonRarity.Common"/>) use
/// plain numeric constants — their formulas do not invoke a budget
/// multiplier intrinsic at all (e.g. <c>"5"</c> for
/// <c>ParagonNodeCoreStat_Normal</c>). The evaluator returns the
/// constant directly in that case.
/// </para>
/// </remarks>
public static class ParagonPowerBudget
{
    /// <summary>Magic-rarity defensive budget multiplier.</summary>
    public const double MagicDefensive = 10.0;

    /// <summary>Magic-rarity offensive budget multiplier.</summary>
    public const double MagicOffensive = 2.5;

    /// <summary>Rare-rarity major-stat defensive budget multiplier.</summary>
    public const double RareMajorDefensive = 4.0;

    /// <summary>Rare-rarity minor-stat defensive budget multiplier.</summary>
    public const double RareMinorDefensive = 4.0;

    /// <summary>Rare-rarity major-stat offensive budget multiplier.</summary>
    public const double RareMajorOffensive = 5.0;

    /// <summary>Rare-rarity minor-stat offensive budget multiplier.</summary>
    public const double RareMinorOffensive = 5.0;

    /// <summary>Look up a budget-multiplier intrinsic by its canonical
    /// engine name (case-sensitive). The names follow the engine's
    /// formula-DSL convention:
    /// <c>ParagonPowerBudgetMultiplierNode&lt;Rarity&gt;{Major|Minor}&lt;Off|Def&gt;</c>
    /// — <c>NodeMagicDefensive</c>, <c>NodeRareMajorOffensive</c>, etc.
    /// (six total).</summary>
    /// <param name="intrinsicName">The intrinsic name as it appears in
    /// the formula text — bare identifier, no trailing parentheses.</param>
    /// <param name="value">The pinned multiplier on success.</param>
    /// <returns><see langword="true"/> when the name is one of the six
    /// known intrinsics; <see langword="false"/> otherwise.</returns>
    public static bool TryGetMultiplier(string intrinsicName, out double value)
    {
        ArgumentNullException.ThrowIfNull(intrinsicName);
        switch (intrinsicName)
        {
            case "ParagonPowerBudgetMultiplierNodeMagicDefensive":
                value = MagicDefensive; return true;
            case "ParagonPowerBudgetMultiplierNodeMagicOffensive":
                value = MagicOffensive; return true;
            case "ParagonPowerBudgetMultiplierNodeRareMajorDefensive":
                value = RareMajorDefensive; return true;
            case "ParagonPowerBudgetMultiplierNodeRareMinorDefensive":
                value = RareMinorDefensive; return true;
            case "ParagonPowerBudgetMultiplierNodeRareMajorOffensive":
                value = RareMajorOffensive; return true;
            case "ParagonPowerBudgetMultiplierNodeRareMinorOffensive":
                value = RareMinorOffensive; return true;
            default:
                value = 0;
                return false;
        }
    }
}
