using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WiseOwl.Casc.Configuration;

/// <summary>
/// The parsed <c>.build.info</c> file at the root of a local Blizzard
/// installation. It is a pipe-delimited table: a header row of
/// <c>Name!TYPE:len</c> column descriptors, then one row per installed
/// build. WiseOwl.Casc reads the active row and exposes the few fields the
/// transport needs (build/CDN config keys, version, tags).
/// </summary>
/// <remarks>
/// Format documented at <see href="https://wowdev.wiki/TACT"/>; verified
/// against a live Diablo IV (<c>fenris</c>) install.
/// </remarks>
public sealed class BuildInfo
{
    private readonly IReadOnlyDictionary<string, string> _columns;

    private BuildInfo(IReadOnlyDictionary<string, string> columns) => _columns = columns;

    /// <summary>Hex content key of the <b>build configuration</b> file
    /// (the <c>Build Key</c> column) — found under <c>Data/config</c>.</summary>
    public string BuildKey => Column("Build Key");

    /// <summary>Hex key of the CDN configuration file (the <c>CDN Key</c>
    /// column). Present even for fully-local installs.</summary>
    public string CdnKey => Column("CDN Key");

    /// <summary>The human build/version string (e.g. <c>3.0.2.71886</c>).</summary>
    public string Version => Column("Version");

    /// <summary>The branch/region (e.g. <c>us</c>).</summary>
    public string Branch => Column("Branch");

    /// <summary>The install tag string (locales/platforms this install has).</summary>
    public string Tags => Column("Tags");

    /// <summary>All columns of the selected build row, by header name.</summary>
    public IReadOnlyDictionary<string, string> Columns => _columns;

    /// <summary>Read a column by header name, or <c>""</c> if absent.</summary>
    public string Column(string name) =>
        _columns.TryGetValue(name, out var v) ? v : string.Empty;

    /// <summary>Load and parse <c>&lt;installPath&gt;/.build.info</c>,
    /// selecting the active build row (the row whose <c>Active</c> column is
    /// <c>1</c>, else the first data row).</summary>
    /// <param name="installPath">The game install root directory.</param>
    public static BuildInfo Load(string installPath)
    {
        var path = Path.Combine(installPath, ".build.info");
        if (!File.Exists(path))
            throw new CascContentNotFoundException(
                $"No .build.info found at '{path}'. Is this a Blizzard install root?");

        var lines = File.ReadAllLines(path)
            .Where(l => l.Length > 0)
            .ToArray();
        if (lines.Length < 2)
            throw new CascFormatException(".build.info has no data rows.");

        // Header cells look like "Build Key!HEX:16" — we only want the name.
        var headers = lines[0].Split('|')
            .Select(h => h.Split('!')[0].Trim())
            .ToArray();

        // Pick the active row; fall back to the first data row.
        var rows = lines.Skip(1).Select(l => l.Split('|')).ToArray();
        var activeIdx = Array.FindIndex(headers, h =>
            h.Equals("Active", StringComparison.OrdinalIgnoreCase));
        var row = activeIdx >= 0
            ? rows.FirstOrDefault(r => activeIdx < r.Length && r[activeIdx].Trim() == "1")
              ?? rows[0]
            : rows[0];

        var cols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length && i < row.Length; i++)
            cols[headers[i]] = row[i].Trim();

        return new BuildInfo(cols);
    }
}
