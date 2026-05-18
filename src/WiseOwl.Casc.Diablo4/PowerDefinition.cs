using System;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>PowerDefinition</c> (<c>.pow</c>, SNO group
/// <see cref="SnoGroup.Power"/> = 29) — a skill/passive/legendary power.
/// Identity + localized text only; the power's gameplay mechanics (a large
/// engine record) stay the consumer's stat-effect model per the library
/// boundary (Appendix C).
/// </summary>
/// <remarks>
/// <see cref="SnoId"/> is the binary field (payload <c>0</c>). The
/// localized <see cref="Name"/> / <see cref="Description"/> are resolved
/// from the power's <b>sibling StringList table</b> via the generalized
/// sibling-string convention (<c>docs/casc-diablo4-format.md §11.2</c>,
/// Appendix A CL-22 / CL-20): group-42 SNO <c>"Power_" + snoName</c>,
/// labels <c>name</c> / <c>desc</c>. Empty (honest sentinel) when decoded
/// byte-only (no <see cref="CoreToc"/>) or when the power has no sibling
/// table; the consumer owns any fallback. Raw decoded text — D4 markup
/// kept intact.
/// </remarks>
public sealed class PowerDefinition
{
    private PowerDefinition(int snoId)
    {
        SnoId = snoId;
    }

    /// <summary>The power's own SNO id (== the CoreTOC id).</summary>
    public int SnoId { get; }

    /// <summary>Localized power name (sibling label <c>name</c>), or
    /// <see cref="string.Empty"/> if unresolved. See the type remarks.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Localized power description (sibling label <c>desc</c>;
    /// carries D4 markup/substitution tokens), or
    /// <see cref="string.Empty"/>.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Decode a Power from its raw SNO blob. Identity only — the
    /// localized fields need <see cref="CoreToc"/>; use
    /// <see cref="Diablo4Storage.ReadPower(int,string)"/>.</summary>
    public static PowerDefinition Parse(ReadOnlySpan<byte> blob) =>
        new(new SnoRecord(blob).SnoId);

    internal void SetStrings(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
