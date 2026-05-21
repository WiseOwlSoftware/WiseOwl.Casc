using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// Diablo IV game-wide helpers that are not tied to an open storage.
/// </summary>
public static class Diablo4
{
    /// <summary>
    /// The Diablo IV <b>GBID</b> hash: a case-insensitive DJB2 over the
    /// name's ASCII bytes (<c>h = 0; h = h*33 + tolower(c)</c>, unsigned
    /// 32-bit, stopping at the first NUL). This is the canonical hash D4
    /// uses to reference shared values across GameBalance / affix / skill /
    /// formula tables.
    /// </summary>
    /// <remarks>
    /// Verified: <c>GbidHash("ParagonNodeCoreStat_Normal") == 0x42C16A1B</c>
    /// (upstream <c>d4-binary-formats.md §7.1</c>). Parsing the tables that
    /// these GBIDs key into is consumer domain logic (see the boundary in
    /// <c>docs/casc-diablo4-format.md</c> Appendix C); this hash is the one piece that is
    /// generic and stable enough to be the library's canonical home.
    /// </remarks>
    /// <param name="name">The reference name (ASCII).</param>
    public static uint GbidHash(string name)
    {
        uint h = 0;
        foreach (var ch in name)
        {
            if (ch == '\0') break;
            // tolower for ASCII A–Z; other chars pass through unchanged.
            var c = ch is >= 'A' and <= 'Z' ? (uint)(ch + 32) : ch;
            unchecked { h = h * 33 + c; }
        }
        return h;
    }

    /// <summary>
    /// The Diablo IV <b>type hash</b>: the same DJB2 core as
    /// <see cref="GbidHash"/> (<c>h = 0; h = h*33 + c</c>, unsigned
    /// 32-bit, stopping at the first NUL) but <b>case-sensitive</b> (no
    /// lower-casing). D4 identifies serialized type / class / struct
    /// names with this — e.g. <c>DT_INT</c>, <c>DT_BINDABLEPROPERTY</c>,
    /// and the UI widget class ids in the <c>0xE4825AB8</c> UI-scene
    /// format.
    /// </summary>
    /// <remarks>
    /// Same algorithm family as <see cref="GbidHash"/> (which lower-cases
    /// first) and <see cref="FieldHash"/> (which masks to 28 bits); all
    /// three are <b>seed-0</b> DJB2, not the textbook seed 5381. See
    /// <c>docs/casc-diablo4-format.md §10.2</c>. Verified:
    /// <c>TypeHash("DT_INT") == 0xA4C42E02</c>,
    /// <c>TypeHash("DT_BINDABLEPROPERTY") == 0x1332C78D</c>.
    /// </remarks>
    /// <param name="name">The type / struct name (ASCII, case-sensitive).</param>
    public static uint TypeHash(string name)
    {
        uint h = 0;
        foreach (var ch in name)
        {
            if (ch == '\0') break;
            unchecked { h = h * 33 + ch; }
        }
        return h;
    }

    /// <summary>
    /// The Diablo IV <b>field hash</b>: <see cref="TypeHash"/> masked to
    /// 28 bits (<c>&amp; 0x0FFFFFFF</c>). D4 identifies serialized struct
    /// <i>field</i> names with this; the 28-bit mask is why field ids in
    /// SNO meta cluster below <c>0x10000000</c>.
    /// </summary>
    /// <remarks>
    /// See <c>docs/casc-diablo4-format.md §10.2</c>. Verified against the
    /// UI-scene schema, e.g. <c>FieldHash("nWidth") == 0x06F9158E</c>,
    /// <c>FieldHash("nLeft") == 0x07F1EF79</c>,
    /// <c>FieldHash("rgbaTint") == 0x09A3F17B</c>.
    /// </remarks>
    /// <param name="name">The field name (ASCII, case-sensitive).</param>
    public static uint FieldHash(string name) => TypeHash(name) & 0x0FFFFFFFu;

    /// <summary>
    /// Cracked-hash registry — a cumulative dictionary of
    /// <see cref="FieldHash"/> values whose source name has been
    /// recovered (via first-party brute force or community schema
    /// lookups, e.g. <c>blizzhackers/d4data</c>'s
    /// <c>!!D4FieldChecksums.yml</c>). Per the
    /// <c>feedback_cumulative-hash-decode</c> principle, every newly
    /// cracked hash is added here so all prior scene/SNO blobs become
    /// retroactively interpretable. Curated and authoritative — only
    /// cracks verified to round-trip through <see cref="FieldHash"/>
    /// are added.
    /// </summary>
    /// <remarks>
    /// See <c>docs/d4-hash-dictionary.md</c> for the persistent
    /// registry (including type-hash + class-hash + uncracked
    /// high-priority targets, which this in-source table doesn't
    /// duplicate). The dictionary is intentionally a public surface so
    /// consumers can pretty-print field hashes encountered during
    /// scene decoding.
    /// </remarks>
    public static IReadOnlyDictionary<uint, string> KnownFieldNames { get; } =
        new Dictionary<uint, string>
        {
            // Coordinate / rect fields (DT_INT)
            [0x07F1EF79] = "nLeft",
            [0x069EA64C] = "nRight",
            [0x003DC5C1] = "nTop",
            [0x0594CC83] = "nBottom",
            [0x06F9158E] = "nWidth",
            [0x02D88AE7] = "nHeight",
            // Color / tint (DT_RGBACOLOR)
            [0x09A3F17B] = "rgbaTint",
            [0x00957CB7] = "rgbaForeground",
            // State / handle (DT_BOOL / DT_HANDLE)
            [0x06AB76DE] = "bActive",
            [0x0789C1CD] = "hText",
            [0x0204DBB8] = "hTooltipText",   // FR-C16 R12 (EXE symbol extract)
            // Per-state image-slot family (FR-C16 R11): the engine encodes
            // a widget's interaction state by the field-name suffix. Two
            // tiers — the widget background image and its icon image, each
            // with normal/pressed/mouse-over/disable variants.
            [0x0D75128C] = "hImageFramePressed",
            [0x0B63D29B] = "hImageFrameMouseOver",
            [0x0DAEFCAA] = "hImageFrameDisable",
            [0x02330CBF] = "hImageFrameIcon",
            [0x056F24F5] = "hImageFrameIconPressed",
            [0x05A90F13] = "hImageFrameIconDisable",
            // Enum / SNO refs — FR-C14 R8 critical cracks
            [0x07DB38D3] = "snoTiledStyle",      // → TiledStyleDefinition
            [0x093CBAA8] = "eGroupType",
            [0x03D55658] = "eVerticalAnchoring",
            // NSlice / TiledStyle struct fields — FR-C14 R10
            // (from the blizzhackers/d4data !NSlice schema). Computed
            // via FieldHash so they round-trip through the canonical
            // hasher rather than hard-coding magic numbers.
            [FieldHash("flImageScale")] = "flImageScale",
            [FieldHash("nPadding")] = "nPadding",
            [FieldHash("hSourceImage")] = "hSourceImage",
            [FieldHash("eSliceStyle")] = "eSliceStyle",
            [FieldHash("fTileCenter")] = "fTileCenter",
            [FieldHash("fTileHorizontalBorders")] = "fTileHorizontalBorders",
            [FieldHash("fTileVerticalBorders")] = "fTileVerticalBorders",
            // Node-widget fields — FR-C12 R-redecode (brute force):
            // every paragon node state-widget binds its disc handle via
            // hImageFrame and its opacity via dwAlpha.
            [FieldHash("hImageFrame")] = "hImageFrame",
            [FieldHash("dwAlpha")] = "dwAlpha",
        };

    /// <summary>Cracked-hash registry for <see cref="TypeHash"/>
    /// (full 32-bit). Companion to
    /// <see cref="KnownFieldNames"/>; see remarks there.</summary>
    public static IReadOnlyDictionary<uint, string> KnownTypeNames { get; } =
        new Dictionary<uint, string>
        {
            [0xA4C42E02] = "DT_INT",
            [0xA4C45887] = "DT_SNO",
            [0x3D47BD2C] = "DT_ENUM",
            [0x3D4646AB] = "DT_BYTE",
            [0x1332C78D] = "DT_BINDABLEPROPERTY",
            // UI widget / TiledStyle type tags — FR-C14 R10
            // (blizzhackers/d4data !!D4Checksums.yml).
            [0x6B1C5D9C] = "UIImageHandleReference",
            [0xBC0D579E] = "NSlice",
            [0x02E46583] = "TiledWindowPieces",
            [0x02F5672C] = "TiledStyleDefinition",
            [0x5943238D] = "HorizontalTiledWindowPieces",
            [0x6BFED904] = "VertTiledWindowPieces",
            [0x8E00F391] = "WindowPieces",
            [0xC5A830EC] = "WindowPiecesBase",
            // UI widget *class* style ids — FR-C16/C17 R3
            // (blizzhackers/d4data !!D4Checksums.yml). These are the
            // `UiWidget.ClassId` values seen across the paragon scenes.
            [0x1E3077C7] = "UIWindowStyle",          // the drawable rect widget
            [0x112661D5] = "UIStackPanelStyle",      // layout/stack container
            [0x093D303F] = "UIParagonBoardStyle",    // the ParagonNodes grid container
            [0x145F2056] = "UIBlinkerStyle",         // pulsing glow (Node*Glow, AvailableGlow)
            [0x98D4E83A] = "UIRActorStyle",          // 3D/VFX canvas
            [0x079C2454] = "UITextStyle",
            [0x64A23287] = "UIScrollBoxStyle",       // Glyph_Grid
            [0x8A5932F4] = "UIListBoxStyle",         // ParagonStats
            [0xC81DED6B] = "UIButtonStyle",
            [0x4873BE59] = "UIWrapPanelStyle",       // Glyph_WrapPanel
            [0x999CA9A3] = "UIHotkeyStyle",
            [0x0E1C5710] = "UIControlStyle",         // base control style
        };

    /// <summary>Pretty-print a field hash as <c>"name (0xHHHHHHHH)"</c>
    /// when <see cref="KnownFieldNames"/> covers it; otherwise
    /// <c>"0xHHHHHHHH"</c>. Convenience for debug output and
    /// dictionary-driven scene dumps.</summary>
    public static string FormatFieldHash(uint hash) =>
        KnownFieldNames.TryGetValue(hash, out var name)
            ? $"{name} (0x{hash:X8})" : $"0x{hash:X8}";

    /// <summary>Pretty-print a type hash. See
    /// <see cref="FormatFieldHash"/>.</summary>
    public static string FormatTypeHash(uint hash) =>
        KnownTypeNames.TryGetValue(hash, out var name)
            ? $"{name} (0x{hash:X8})" : $"0x{hash:X8}";
}
