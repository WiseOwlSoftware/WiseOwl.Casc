namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// One Diablo IV playable character class, decoded first-party from the
/// game's own class-definition data (FR-D2). A class is a first-class D4
/// concept independent of paragon; this type carries only the raw decoded
/// roster identity + localized name — no policy, no imaging.
/// </summary>
/// <remarks>
/// Recovered clean-room (see <c>docs/casc-diablo4-format.md §6.5</c>,
/// Appendix A CL-17): the roster is SNO group
/// <see cref="SnoGroup.PlayerClass"/> (74); the localized display name is
/// the <c>General</c> StringList table (SNO 4118) under label
/// <c>"PlayerClass" + SnoName + "Male"</c> (the markup-free display form;
/// the gendered variants are identical strings, while the base
/// <c>PlayerClass&lt;SnoName&gt;</c> label carries D4 <c>|5sing:plur</c>
/// pluralization markup). A group-74 entry is a real playable class iff
/// that label exists — this is the data-driven filter that excludes
/// non-class junk entries (e.g. <c>Axe Bad Data</c>) with no hardcoded
/// exclusion list.
/// </remarks>
/// <param name="SnoId">The <see cref="SnoGroup.PlayerClass"/> SNO id — the
/// stable per-class key (survives classes being added/reordered; never an
/// array position). E.g. Warlock = 2207749 on build 3.0.2.71886.</param>
/// <param name="SnoName">The CoreTOC SNO name — the stable internal token
/// (e.g. <c>Warlock</c>, <c>Sorcerer</c>, <c>Spiritborn</c>). Treat as an
/// opaque id; do not parse its substructure.</param>
/// <param name="DisplayName">The localized class display name for the
/// requested locale (e.g. <c>Warlock</c>; localized per locale). Raw
/// decoded value — D4 markup, if any, is left intact (consumer policy).</param>
public sealed record CharacterClass(int SnoId, string SnoName, string DisplayName);
