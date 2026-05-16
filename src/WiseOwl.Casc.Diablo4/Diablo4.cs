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
    /// <c>docs/casc-format.md</c>); this hash is the one piece that is
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
}
