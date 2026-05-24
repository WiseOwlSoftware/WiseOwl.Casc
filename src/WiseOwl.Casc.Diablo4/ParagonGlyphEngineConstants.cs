using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// FR-C24 (CL-83) — the small set of engine constants that govern
/// glyph leveling + radius growth, surfaced for the
/// <see cref="ParagonGlyphDefinition"/> projection. These values
/// aren't in the <c>.gph</c> SNO record itself — the engine applies
/// them universally across every glyph — but they're load-bearing
/// for the consumer's tooltip rendering, so they're baked here
/// (same pattern as <see cref="ParagonPowerBudget"/> for the
/// budget-multiplier intrinsics in CL-68).
/// </summary>
/// <remarks>
/// Cross-validated against the Optimizer's Warlock-21 oracle on
/// `casc-fr#36` — all 21 glyphs show identical radius / cap
/// behaviour, confirming these are engine constants, not per-glyph
/// data fields. Appendix D re-verify trigger applies: if a future
/// season ships per-glyph variation in these values, the constants
/// here need to migrate to a decoded-from-record property and the
/// re-verify checks updated.
/// </remarks>
internal static class ParagonGlyphEngineConstants
{
    /// <summary>The player levels at which the glyph's
    /// socket-effect radius grows by one. Engine constant.</summary>
    public static IReadOnlyList<int> RadiusUpgradeLevels { get; }
        = new int[] { 25, 50 };
}
