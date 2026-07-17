using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// Evaluate a paragon stat-node magnitude formula text to its displayed
/// numeric value (FR-C21). The grammar is a strict subset of the
/// engine's formula DSL: a numeric literal, a zero-arg call to one of
/// the six budget-multiplier intrinsics from <see cref="ParagonPowerBudget"/>,
/// binary <c>+ - * /</c>, and parentheses.
/// </summary>
/// <remarks>
/// <para>
/// Examples that round-trip exactly to the in-game displayed magnitude
/// (see <see cref="ParagonPowerBudget"/> for the full validation matrix):
/// </para>
/// <list type="bullet">
///   <item><c>"5"</c> ⇒ <c>5.0</c> (Normal-rarity core stat — no multiplier)</item>
///   <item><c>"0.75 * ParagonPowerBudgetMultiplierNodeMagicDefensive()"</c> ⇒ <c>7.5</c></item>
///   <item><c>"3 * ParagonPowerBudgetMultiplierNodeMagicOffensive()"</c> ⇒ <c>7.5</c></item>
///   <item><c>"1.5/2 * ParagonPowerBudgetMultiplierNodeRareMajorDefensive()"</c> ⇒ <c>3.0</c></item>
///   <item><c>"3.5 * ParagonPowerBudgetMultiplierNodeRareMajorOffensive()"</c> ⇒ <c>17.5</c></item>
/// </list>
/// <para>
/// The evaluator returns <see cref="double.NaN"/> when the expression
/// references an unknown identifier (typically: a future build adds a
/// new budget multiplier — the calibration table needs extending), and
/// throws when the expression is syntactically invalid (signals a
/// caller bug or a malformed shipped record, not a normal data
/// condition).
/// </para>
/// <para>
/// <b>Threshold formulas</b> (e.g. <c>StatTagDefinition.ThresholdFormulaText</c>
/// — <c>"760 + (455 * ParagonBoardEquipIndex)"</c>) bind a runtime
/// variable (<c>ParagonBoardEquipIndex</c>) rather than a function call,
/// and are <b>not</b> evaluated here. The consumer supplies that
/// binding to the bonus-threshold resolution path (a separate FR-C21
/// surface).
/// </para>
/// </remarks>
public static class ParagonMagnitudeFormula
{
    /// <summary>Evaluate <paramref name="formulaText"/> as a paragon
    /// magnitude formula and return the displayed numeric value.
    /// Returns <see cref="double.NaN"/> when the expression references
    /// an unknown budget-multiplier intrinsic; throws when the
    /// expression is syntactically invalid.</summary>
    public static double Evaluate(string formulaText)
    {
        ArgumentNullException.ThrowIfNull(formulaText);
        // Paragon magnitude formulas never reference SF_N slots — supply a
        // lookup that signals an out-of-band SF_N as NaN to propagate.
        // Magic numbers in tests: the slotLookup is only invoked if SF_N
        // appears in the text, which never happens for paragon magnitudes.
        var result = PowerScriptFormulaEvaluator.Evaluate(
            formulaText,
            slotLookup: static _ => double.NaN,
            functionResolver: ResolveBudgetMultiplier);
        return result.Value;
    }

    /// <summary>
    /// FR-C33 (CL-98) — evaluate <paramref name="formulaText"/> and
    /// <b>distinguish an unsupported engine function from a value that
    /// legitimately computes to <see cref="double.NaN"/></b> (the gap
    /// <see cref="Evaluate"/> can't express — it returns a silent
    /// <see cref="double.NaN"/> for both).
    /// </summary>
    /// <remarks>The only calls this evaluator implements are the six
    /// zero-arg budget-multiplier intrinsics (<see cref="ResolveBudgetMultiplier"/>).
    /// Any other engine function — the affix roll functions
    /// (<c>FloatRandomRangeWithInterval</c>, <c>RandomInt</c>, <c>IPower</c>,
    /// <c>ROUND</c>, …) or a player-state accessor — is <b>unsupported</b>:
    /// <see cref="TryEvaluate"/> returns <see langword="false"/> so the
    /// consumer branches deliberately (e.g. renders the raw formula text)
    /// rather than propagating a silent NaN into a displayed number. A pure
    /// arithmetic / budget-multiplier expression returns <see langword="true"/>
    /// with its value (including a genuine NaN from e.g. <c>0/0</c>, which is
    /// a computed result, not an unsupported call). Throws on syntactically
    /// invalid text, as <see cref="Evaluate"/> does.</remarks>
    /// <param name="formulaText">The formula source text.</param>
    /// <param name="value">The computed value, or <see cref="double.NaN"/>
    /// when an unsupported function short-circuited the evaluation.</param>
    /// <returns><see langword="true"/> iff the expression contains no
    /// unsupported function call (its <paramref name="value"/> is a genuine
    /// computed result).</returns>
    public static bool TryEvaluate(string formulaText, out double value)
    {
        ArgumentNullException.ThrowIfNull(formulaText);
        var result = PowerScriptFormulaEvaluator.Evaluate(
            formulaText,
            slotLookup: static _ => double.NaN,
            functionResolver: ResolveBudgetMultiplier);
        foreach (var fn in result.FunctionRefs)
            if (ResolveBudgetMultiplier(fn.Name, fn.Args) is null)
            {
                value = double.NaN;
                return false;   // an unsupported engine function
            }
        value = result.Value;
        return true;
    }

    /// <summary>Function-call resolver invoked by the shared evaluator
    /// for each <c>Identifier(...)</c> node. Zero-arg calls into the
    /// six budget-multiplier intrinsics are resolved from
    /// <see cref="ParagonPowerBudget.TryGetMultiplier"/>; everything
    /// else short-circuits the evaluation to NaN via
    /// <see langword="null"/>.</summary>
    private static double? ResolveBudgetMultiplier(
        string name, IReadOnlyList<double> args)
    {
        if (args.Count != 0) return null;
        return ParagonPowerBudget.TryGetMultiplier(name, out var v) ? v : null;
    }
}
