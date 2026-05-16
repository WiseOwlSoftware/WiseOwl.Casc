using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WiseOwl.Casc.Configuration;

/// <summary>
/// A parsed CASC <b>build configuration</b> file (found at
/// <c>Data/config/&lt;k0k1&gt;/&lt;k2k3&gt;/&lt;buildKey&gt;</c>). It is a
/// simple <c>key = value [value …]</c> text file (<c>#</c> comments) naming
/// the content/encoding keys of the storage's core manifests: the
/// <c>encoding</c> table, the <c>root</c>, and the <c>vfs-root</c> /
/// <c>vfs-N</c> TVFS manifests.
/// </summary>
public sealed class BuildConfiguration
{
    private readonly IReadOnlyDictionary<string, string[]> _entries;

    private BuildConfiguration(IReadOnlyDictionary<string, string[]> entries) =>
        _entries = entries;

    /// <summary>Raw multi-valued entries, by key.</summary>
    public IReadOnlyDictionary<string, string[]> Entries => _entries;

    /// <summary>All values for <paramref name="key"/> (empty if absent).</summary>
    public string[] Values(string key) =>
        _entries.TryGetValue(key, out var v) ? v : [];

    /// <summary>The content key of the <c>encoding</c> table (first value of
    /// the <c>encoding</c> entry — <c>CKey EKey</c>).</summary>
    public ContentKey EncodingContentKey => ContentKey.Parse(Values("encoding")[0]);

    /// <summary>The encoding key of the <c>encoding</c> table (second value;
    /// you can read it directly without consulting the encoding table).</summary>
    public EncodingKey EncodingEncodingKey => EncodingKey.Parse(Values("encoding")[1]);

    /// <summary>The <c>root</c> content key. For Diablo IV this is all
    /// zeroes — the file system is delivered via the <c>vfs-root</c> TVFS
    /// manifest instead.</summary>
    public ContentKey RootContentKey =>
        ContentKey.TryParse(Values("root").FirstOrDefault() ?? "", out var c)
            ? c : default;

    /// <summary>The <c>vfs-root</c> entry (<c>CKey EKey</c>), the TVFS
    /// manifest root used by Diablo IV. Empty if this storage has no TVFS.</summary>
    public (ContentKey Content, EncodingKey Encoding)? VfsRoot
    {
        get
        {
            var v = Values("vfs-root");
            return v.Length >= 2
                ? (ContentKey.Parse(v[0]), EncodingKey.Parse(v[1]))
                : null;
        }
    }

    /// <summary>The human build name (<c>build-name</c>), e.g.
    /// <c>71886_Win64Client_3_0_2</c>.</summary>
    public string BuildName => Values("build-name").FirstOrDefault() ?? string.Empty;

    /// <summary>Resolve the on-disk path of a config/manifest file from its
    /// hex key: <c>&lt;basePath&gt;/Data/config/&lt;k0k1&gt;/&lt;k2k3&gt;/&lt;key&gt;</c>.
    /// </summary>
    public static string LocalConfigPath(string installPath, string hexKey) =>
        Path.Combine(installPath, "Data", "config",
            hexKey.Substring(0, 2), hexKey.Substring(2, 2), hexKey);

    /// <summary>Load and parse the build configuration named by a
    /// <see cref="BuildInfo"/>'s <see cref="BuildInfo.BuildKey"/>.</summary>
    public static BuildConfiguration Load(string installPath, BuildInfo info) =>
        LoadFromFile(LocalConfigPath(installPath, info.BuildKey));

    /// <summary>Parse a build/CDN configuration file from disk.</summary>
    public static BuildConfiguration LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new CascContentNotFoundException($"Build config not found: '{path}'.");
        return Parse(File.ReadAllLines(path));
    }

    /// <summary>Parse build-configuration lines.</summary>
    public static BuildConfiguration Parse(IEnumerable<string> lines)
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line.Substring(0, eq).Trim();
            var vals = line.Substring(eq + 1)
                .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            map[key] = vals;
        }
        return new BuildConfiguration(map);
    }
}
