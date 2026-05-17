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
}
