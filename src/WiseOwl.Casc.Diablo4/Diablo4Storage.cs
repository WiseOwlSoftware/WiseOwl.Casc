using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WiseOwl.Casc.Diablo4;

/// <summary>Which Diablo IV content partition a SNO is addressed in. A
/// definition/record SNO lives in <see cref="Meta"/>; texture pixels live in
/// <see cref="Payload"/> (with smaller mips in <see cref="PayLow"/> /
/// <see cref="PayMed"/>).</summary>
public enum SnoFolder
{
    /// <summary>Packed child container.</summary>
    Child,
    /// <summary>The record / definition section (present for most SNOs).</summary>
    Meta,
    /// <summary>The payload section (texture pixels, etc.).</summary>
    Payload,
    /// <summary>Low-resolution payload (smallest mips).</summary>
    PayLow,
    /// <summary>Medium-resolution payload.</summary>
    PayMed,
}

/// <summary>
/// The Diablo IV game module: a thin, modern facade over a
/// <see cref="CascStorage"/> that resolves content by <b>SNO id</b> through
/// <c>CoreTOC.dat</c> and the TVFS file system, exposes the
/// <c>0x44CF00F5</c> combined-meta texture catalog, and BLTE-decodes the
/// result. The Diablo IV trademark appears only in this module, used
/// nominatively as a compatibility descriptor.
/// </summary>
/// <remarks>
/// A SNO is addressed by the path
/// <c>&lt;prefix&gt;\&lt;Folder&gt;\&lt;groupId&gt;\&lt;name&gt;&lt;ext&gt;</c>
/// (prefix defaults to <c>Base</c>), hashed and resolved through TVFS — the
/// same scheme the game uses. The <see cref="CoreToc"/> supplies the name,
/// group and extension.
/// </remarks>
public sealed class Diablo4Storage : IDisposable
{
    /// <summary>The Diablo IV TACT product code.</summary>
    public const string ProductCode = "fenris";

    private readonly CascStorage _casc;
    private readonly bool _ownsCasc;
    private CombinedTextureMeta? _textureMeta;
    private SharedPayloadMapping? _sharedPayloads;
    private readonly Dictionary<string, StringListCatalog> _strings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<CharacterClass>> _classes = new(StringComparer.OrdinalIgnoreCase);
    private (int SnoId, string SnoName)[]? _classRoster;
    private (int SnoId, int Rank)[]? _classRanks;
    private Catalog? _catalog;
    private readonly object _gate = new();

    /// <summary>The default locale (the one most installs ship enabled).</summary>
    public const string DefaultLocale = "enUS";

    private Diablo4Storage(CascStorage casc, bool ownsCasc, CoreToc coreToc)
    {
        _casc = casc;
        _ownsCasc = ownsCasc;
        CoreToc = coreToc;
    }

    /// <summary>The master SNO directory, parsed from <c>Base\CoreTOC.dat</c>.</summary>
    public CoreToc CoreToc { get; }

    /// <summary>FR-C20 — the asset discovery/retrieval facade: find / enumerate
    /// (filtered) / retrieve any catalogued recipe or definition without
    /// hardcoding SNO ids/names. The typed accessors below are ergonomic
    /// shortcuts over the same providers.</summary>
    public Catalog Catalog => _catalog ??= new Catalog(this);

    /// <summary>The underlying game-agnostic CASC storage.</summary>
    public CascStorage Casc => _casc;

    /// <summary>Open a local Diablo IV installation.</summary>
    /// <param name="installPath">The install root (default
    /// <c>D:\Diablo IV</c> on this machine).</param>
    public static Diablo4Storage Open(string installPath)
    {
        var casc = CascStorage.OpenLocal(
            installPath, new CascOpenOptions { Product = ProductCode });
        return Attach(casc, ownsCasc: true);
    }

    /// <summary>Open a local Diablo IV installation asynchronously.</summary>
    public static Task<Diablo4Storage> OpenAsync(
        string installPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Open(installPath), cancellationToken);

    /// <summary>Environment variable that overrides install auto-detection
    /// (<see cref="TryLocateInstall"/>) — set it to a Diablo IV install root.</summary>
    public const string InstallPathEnvironmentVariable = "WISEOWL_CASC_INSTALL";

    /// <summary>Open the local Diablo IV installation, auto-detecting its
    /// location (see <see cref="TryLocateInstall"/>). Throws
    /// <see cref="CascException"/> when none can be found — use
    /// <see cref="Open(string)"/> with an explicit path for a custom or
    /// non-Windows install.</summary>
    public static Diablo4Storage Open() => Open(LocateInstall());

    /// <summary>Open the auto-detected local Diablo IV installation
    /// asynchronously (see <see cref="Open()"/>).</summary>
    public static Task<Diablo4Storage> OpenAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Open(), cancellationToken);

    /// <summary>Resolve the local Diablo IV install root without opening it —
    /// the <see cref="InstallPathEnvironmentVariable"/> override first, then (on
    /// Windows) the registry: the Battle.net uninstall entry
    /// <c>HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Diablo IV</c>
    /// → <c>InstallLocation</c>. A candidate is accepted only when it carries a
    /// <c>.build.info</c> (a real CASC install). Returns <see langword="false"/>
    /// when none is found (no install, or a non-Windows host with no
    /// override).</summary>
    /// <param name="installPath">The resolved install root, or
    /// <see langword="null"/>.</param>
    public static bool TryLocateInstall([NotNullWhen(true)] out string? installPath)
    {
        if (IsCascInstall(Environment.GetEnvironmentVariable(InstallPathEnvironmentVariable), out var env))
        {
            installPath = env;
            return true;
        }
        if (OperatingSystem.IsWindows() && IsCascInstall(ReadRegistryInstallLocation(), out var reg))
        {
            installPath = reg;
            return true;
        }
        installPath = null;
        return false;
    }

    /// <summary>Resolve the install root or throw a clear
    /// <see cref="CascException"/> (see <see cref="TryLocateInstall"/>).</summary>
    private static string LocateInstall() =>
        TryLocateInstall(out var path)
            ? path
            : throw new CascException(
                "Could not locate a Diablo IV installation. Set the " +
                InstallPathEnvironmentVariable +
                " environment variable, or call Open(installPath) with an explicit path.");

    /// <summary>A path is a Diablo IV install when it exists and carries the
    /// <c>.build.info</c> CASC marker.</summary>
    private static bool IsCascInstall(string? candidate, [NotNullWhen(true)] out string? path)
    {
        if (!string.IsNullOrWhiteSpace(candidate) &&
            File.Exists(Path.Combine(candidate, ".build.info")))
        {
            path = candidate;
            return true;
        }
        path = null;
        return false;
    }

    /// <summary>The Battle.net uninstall keys carrying the Diablo IV
    /// <c>InstallLocation</c> — the 64-bit view and the WOW6432Node (32-bit)
    /// view the Battle.net installer typically writes.</summary>
    private static readonly string[] UninstallKeys =
    [
        @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Diablo IV",
        @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Diablo IV",
    ];

    /// <summary>Read <c>Uninstall\Diablo IV\InstallLocation</c> via <c>reg.exe</c>
    /// (dependency-free; Windows-only), trying both the native and 32-bit
    /// registry views. Returns <see langword="null"/> on any failure (key
    /// absent, access denied, reg.exe missing).</summary>
    private static string? ReadRegistryInstallLocation()
    {
        foreach (var key in UninstallKeys)
            if (QueryRegistrySz(key, "InstallLocation") is { } value)
                return value;
        return null;
    }

    private static string? QueryRegistrySz(string keyPath, string valueName)
    {
        try
        {
            var psi = new ProcessStartInfo("reg")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("query");
            psi.ArgumentList.Add(keyPath);       // space in "Diablo IV" quoted by ArgumentList
            psi.ArgumentList.Add("/v");
            psi.ArgumentList.Add(valueName);

            using var process = Process.Start(psi);
            if (process is null) return null;
            string output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { /* best effort */ }
                return null;
            }
            // A value line reads: "    InstallLocation    REG_SZ    D:\Diablo IV"
            // — the path can contain spaces, so take everything after REG_SZ.
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains(valueName, StringComparison.Ordinal)) continue;
                int at = line.IndexOf("REG_SZ", StringComparison.Ordinal);
                if (at >= 0) return line[(at + "REG_SZ".Length)..].Trim();
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
            or InvalidOperationException or IOException or SystemException)
        {
            // reg.exe unavailable / spawn failure → treat as "not found".
        }
        return null;
    }

    /// <summary>Wrap an already-opened <see cref="CascStorage"/> as a
    /// Diablo IV view (the caller keeps ownership of the storage).</summary>
    public static Diablo4Storage Attach(CascStorage casc) => Attach(casc, false);

    private static Diablo4Storage Attach(CascStorage casc, bool ownsCasc)
    {
        var tocBytes = casc.ReadPath(@"Base\CoreTOC.dat");
        return new Diablo4Storage(casc, ownsCasc, CoreToc.Parse(tocBytes));
    }

    /// <summary>
    /// The TVFS path a SNO resolves through:
    /// <c>&lt;prefix&gt;\&lt;Folder&gt;\&lt;id&gt;</c> (a child sub-id
    /// appends <c>-&lt;subId&gt;</c>). Verified empirically against the live
    /// build: Diablo IV addresses SNO content in TVFS by the numeric id —
    /// <b>not</b> by a <c>&lt;group&gt;\&lt;name&gt;&lt;ext&gt;</c> name path
    /// and not by the <c>base:meta\&lt;id&gt;</c> colon form.
    /// </summary>
    public static string SnoPath(int id, SnoFolder folder = SnoFolder.Meta,
        int subId = -1, string prefix = "Base") =>
        subId < 0 ? $@"{prefix}\{folder}\{id}"
                  : $@"{prefix}\{folder}\{id}-{subId}";

    /// <summary>Resolve and BLTE-read a SNO by id (the <see cref="SnoGroup"/>
    /// documents intent; the TVFS address is id-only). For
    /// <see cref="SnoFolder.Payload"/>, an empty/absent direct payload is
    /// transparently resolved through the shared-payload mapping.</summary>
    /// <exception cref="SnoNotFoundException">No such SNO content (the SNO
    /// legitimately has none — callers may skip it).</exception>
    public byte[] ReadSno(SnoGroup group, int id, SnoFolder folder = SnoFolder.Meta,
        int subId = -1)
    {
        _ = group;
        if (TryReadSno(group, id, folder, out var bytes, subId)) return bytes;
        throw new SnoNotFoundException(
            $"SNO {id} (group {(int)group}) folder {folder}" +
            (subId < 0 ? "" : $" sub {subId}") + " is not in the file system.");
    }

    /// <summary>Try to resolve and BLTE-read a SNO by id. Returns
    /// <see langword="false"/> (no throw) when the SNO legitimately has no
    /// such content — the common "skip the art-less node" case.</summary>
    public bool TryReadSno(SnoGroup group, int id, SnoFolder folder,
        out byte[] bytes, int subId = -1)
    {
        _ = group;
        if (_casc.TryResolvePath(SnoPath(id, folder, subId), out var ek))
        {
            bytes = _casc.Read(ek);
            // A texture's direct payload can be present-but-empty: that means
            // "follow the shared-payload alias".
            if (folder is SnoFolder.Payload && bytes.Length == 0 &&
                TryReadSharedPayload(id, subId, out var aliased))
            {
                bytes = aliased;
            }
            return true;
        }

        // No direct payload at all → the shared-payload alias is the source.
        if (folder is SnoFolder.Payload &&
            TryReadSharedPayload(id, subId, out var shared))
        {
            bytes = shared;
            return true;
        }

        bytes = [];
        return false;
    }

    /// <summary>Resolve and open a SNO by id as a decoded stream.</summary>
    public Stream OpenSno(SnoGroup group, int id, SnoFolder folder = SnoFolder.Meta,
        int subId = -1) =>
        new MemoryStream(ReadSno(group, id, folder, subId), writable: false);

    /// <summary>Asynchronously resolve and read a SNO by id.</summary>
    public Task<byte[]> ReadSnoAsync(SnoGroup group, int id,
        SnoFolder folder = SnoFolder.Meta, int subId = -1,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ReadSno(group, id, folder, subId), cancellationToken);

    /// <summary>
    /// Group-agnostic escape hatch: resolve + BLTE-read any SNO by id for a
    /// group <see cref="SnoGroup"/> does not name (the TVFS address is
    /// id-only — the group is informational). Raw bytes only; per the
    /// library/consumer boundary the library does not grow typed readers.
    /// </summary>
    public byte[] ReadSno(int groupId, int id, SnoFolder folder = SnoFolder.Meta,
        int subId = -1) =>
        ReadSno((SnoGroup)groupId, id, folder, subId);

    /// <summary>Group-agnostic non-throwing read by id.</summary>
    public bool TryReadSno(int groupId, int id, SnoFolder folder,
        out byte[] bytes, int subId = -1) =>
        TryReadSno((SnoGroup)groupId, id, folder, out bytes, subId);

    /// <summary>
    /// Stream every SNO in a group as <c>(id, bytes)</c>, skipping ids that
    /// legitimately have no content in <paramref name="folder"/>. The local
    /// index, encoding table and archive handles stay resident, so sweeping
    /// a large group (Affix/Power are thousands of records) does not re-open
    /// storage.
    /// </summary>
    public IEnumerable<(int Id, byte[] Bytes)> ReadGroup(
        SnoGroup group, SnoFolder folder = SnoFolder.Meta)
    {
        foreach (var e in CoreToc.EntriesInGroup(group))
            if (TryReadSno(group, e.Id, folder, out var bytes))
                yield return (e.Id, bytes);
    }

    private bool TryReadSharedPayload(int id, int subId, out byte[] bytes)
    {
        bytes = [];
        if (!SharedPayloads.TryGetSource(id, out var sourceId)) return false;
        if (_casc.TryResolvePath(SnoPath(sourceId, SnoFolder.Payload, subId),
                out var ek))
        {
            bytes = _casc.Read(ek);
            return true;
        }
        return false;
    }

    /// <summary>The shared-payload de-duplication mapping
    /// (<c>Base\CoreTOCSharedPayloadsMapping.dat</c>, magic
    /// <c>0xABBA0003</c>), parsed on first use. Empty if the file is
    /// absent.</summary>
    public SharedPayloadMapping SharedPayloads
    {
        get
        {
            if (_sharedPayloads is not null) return _sharedPayloads;
            lock (_gate)
            {
                _sharedPayloads ??=
                    _casc.TryResolvePath(@"Base\CoreTOCSharedPayloadsMapping.dat",
                        out var ek)
                        ? SharedPayloadMapping.Parse(_casc.Read(ek))
                        : SharedPayloadMapping.Empty;
            }
            return _sharedPayloads;
        }
    }

    /// <summary>True if <paramref name="id"/>'s payload is physically stored
    /// under another SNO; <paramref name="sourceId"/> is that holder.</summary>
    public bool TryGetSharedPayloadSource(int id, out int sourceId) =>
        SharedPayloads.TryGetSource(id, out sourceId);

    /// <summary>The combined-meta texture catalog
    /// (<c>Base\Texture-Base-Global.dat</c>, magic <c>0x44CF00F5</c>),
    /// parsed on first use.</summary>
    public CombinedTextureMeta TextureMeta
    {
        get
        {
            if (_textureMeta is not null) return _textureMeta;
            lock (_gate)
            {
                _textureMeta ??= CombinedTextureMeta.Parse(
                    _casc.ReadPath(@"Base\Texture-Base-Global.dat"));
            }
            return _textureMeta;
        }
    }

    /// <summary>
    /// The per-locale localized-string catalog
    /// (<c>base/StringList-Text-&lt;locale&gt;.dat</c>), parsed and cached on
    /// first use. <paramref name="locale"/> is a D4 locale code such as
    /// <c>enUS</c>, <c>deDE</c>, <c>frFR</c>, <c>esES</c>, <c>esMX</c>,
    /// <c>itIT</c>, <c>jaJP</c>, <c>koKR</c>, <c>plPL</c>, <c>ptBR</c>,
    /// <c>ruRU</c>, <c>trTR</c>, <c>zhCN</c>, <c>zhTW</c>.
    /// </summary>
    /// <exception cref="CascContentNotFoundException">That locale's bundle
    /// is not present in this install.</exception>
    public StringListCatalog GetStrings(string locale = DefaultLocale)
    {
        lock (_gate)
        {
            if (_strings.TryGetValue(locale, out var cached)) return cached;
            var path = $"base/StringList-Text-{locale}.dat";
            if (!_casc.TryResolvePath(path, out var ek))
                throw new CascContentNotFoundException(
                    $"No StringList bundle for locale '{locale}' ({path}).");
            var cat = StringListCatalog.Parse(
                _casc.Read(ek), locale, sno => CoreToc.GetName((SnoGroup)42, sno));
            _strings[locale] = cat;
            return cat;
        }
    }

    /// <summary>Resolve a label within a known StringList table (SNO) to its
    /// localized text. Prefer this — labels are unique only within a table.</summary>
    public bool TryGetString(int tableSno, string label, out string text,
        string locale = DefaultLocale) =>
        GetStrings(locale).TryGet(tableSno, label, out text);

    /// <summary>Resolve a label across all tables (first match). Convenient
    /// but ambiguous if the label exists in multiple tables.</summary>
    public bool TryGetString(string label, out string text,
        string locale = DefaultLocale) =>
        GetStrings(locale).TryGet(label, out text);

    // ----- Typed Diablo IV record readers (raw fields only) ---------------

    /// <summary>
    /// Read + decode a Diablo IV <b>UI-scene</b> SNO (group
    /// <see cref="UiScene.Group"/> = 46, format hash
    /// <see cref="UiScene.FormatHash"/>) into its raw widget graph.
    /// Generic surface (any <c>0xE4825AB8</c> SNO, e.g.
    /// <c>ParagonBoard</c> 657304); raw fields only, no
    /// layout/imaging/policy — see <see cref="UiScene"/>.
    /// </summary>
    /// <param name="snoId">The UI-scene SNO id.</param>
    public UiScene ReadUiScene(int snoId) =>
        UiScene.Parse(snoId, ReadSno(UiScene.Group, snoId));

    /// <summary>
    /// The typed paragon-board render projection (FR-C7) over the
    /// generic <see cref="ReadUiScene"/> decode of <c>ParagonBoard</c>
    /// (SNO 657304). Raw decoded geometry only; the absolute
    /// resolution/zoom scale is permanently the consumer's. See
    /// <see cref="ParagonRenderLayout"/> for the staged-delivery
    /// contract (<c>Ratios.Provisional</c>; the 18-row state matrix is
    /// filled as the per-state assembly is decode-proven — no
    /// fabricated rows).
    /// </summary>
    public ParagonRenderLayout ReadParagonRenderLayout() =>
        ParagonRenderProjection.Project(
            ReadUiScene(657304),
            // FR-C8: validate start/gate 0x58-block layer values against
            // the texture catalog so only real atlas handles are emitted
            // (the blocks also carry small int params). No fabrication —
            // every emitted layer resolves to a frame.
            isTextureHandle: IsParagonTextureHandle,
            // FR-C10: every surfaced NodeElement carries its atlas SNO +
            // native pixel size so the consumer can composite at the
            // engine's authoritative native scale without a second
            // catalog walk.
            frameLookup: FrameSize);

    private (int AtlasSno, int W, int H) FrameSize(uint handle)
    {
        if (!TryGetIconFrame(handle, out var sno, out var frame))
            return (0, 0, 0);
        var meta = TextureMeta.Get(sno);
        if (meta is null) return (sno, 0, 0);
        var (_, _, w, h) = frame.PixelRect(meta.Width, meta.Height);
        return (sno, w, h);
    }

    /// <summary>
    /// FR-C9: the <b>exhaustive</b> paragon render-model — the
    /// role-assigned <see cref="ReadParagonRenderLayout"/> plus, for
    /// <c>ParagonBoard</c> 657304 and <c>ParagonBoardSelect</c> 964599,
    /// every widget binding ≥1 real atlas handle (handle + decoded rect
    /// + alpha), regardless of binding shape. The library guarantees
    /// completeness: no atlas-resolving binding record is dropped — the
    /// FR-C9 coverage gate (the integration suite) fails if any future
    /// shape regresses this. The consumer audits this once and owns
    /// role/state classification (FR-C7 §6 boundary).
    /// </summary>
    public ParagonRenderModel ReadParagonRenderModel()
    {
        var scenes = new List<ParagonSceneModel>(2);
        foreach (var id in new[] { 657304, 964599 })
            scenes.Add(ParagonRenderProjection.SceneModel(
                ReadUiScene(id), IsParagonTextureHandle, FrameSize));
        var chrome = ParagonRenderProjection.BoardChrome(
            ReadUiScene(657304), ReadUiScene(964599),
            IsParagonTextureHandle, FrameSize,
            id => TryReadTiledStyle(id, out var s) ? s : null);
        return new ParagonRenderModel(
            ReadParagonRenderLayout(), scenes, chrome);
    }

    /// <summary>The structural "is a real atlas texture handle" test
    /// (FR-C9): handle-magnitude (≥ <c>0x10000</c> — D4 handles are
    /// 32-bit hashes; smaller atlas-resolving values are field
    /// ints/enums, never bindings) and resolvable via the icon
    /// catalog. Used by the typed projection, the exhaustive model, and
    /// the coverage gate so all three share one sound definition.</summary>
    public bool IsParagonTextureHandle(uint handle) =>
        handle >= 0x10000u && handle != 0xFFFFFFFFu &&
        TryGetIconFrame(handle, out _, out _);

    /// <summary>
    /// Read + decode a <see cref="ParagonBoardDefinition"/> by SNO id (group
    /// 108), with its class/index identity resolved from the SNO-name
    /// convention (FR-D1 — <see cref="ParagonBoardDefinition.ClassSnoId"/> /
    /// <see cref="ParagonBoardDefinition.ClassSnoName"/> /
    /// <see cref="ParagonBoardDefinition.BoardIndex"/>).
    /// </summary>
    /// <remarks>
    /// The board record has no class/index field; the only first-party
    /// source is the SNO name <c>Paragon_&lt;ClassToken&gt;_&lt;Index&gt;</c>.
    /// Per the durable opaque-id principle (Appendix C) this naming
    /// convention is decoded once, library-side, and exposed typed — never a
    /// consumer regex. The class token is the unique case-sensitive prefix of
    /// exactly one <see cref="SnoGroup.PlayerClass"/> roster SnoName (§6.6,
    /// CL-16); identity is left unresolved only if the board SNO name is
    /// unknown to <see cref="CoreToc"/>.
    /// </remarks>
    public ParagonBoardDefinition ReadParagonBoard(int id)
    {
        var blob = ReadSno(SnoGroup.ParagonBoard, id);
        if (CoreToc.TryGetName(SnoGroup.ParagonBoard, id, out var boardName) &&
            TryResolveBoardIdentity(boardName, out var classSnoId,
                out var className, out var boardIndex))
            return ParagonBoardDefinition.Parse(
                blob, classSnoId, className, boardIndex);
        return ParagonBoardDefinition.Parse(blob);
    }

    /// <summary>
    /// Decode the <c>Paragon_&lt;ClassToken&gt;_&lt;Index&gt;</c> naming
    /// convention (FR-D1, §6.6 / CL-16). The token is the substring between
    /// the <c>Paragon_</c> prefix and the final <c>_</c>; the index is the
    /// trailing integer (handles both <c>_03</c> and the single-digit
    /// <c>_0</c> start board). The token maps to a class by being the
    /// <b>unique case-sensitive prefix</b> of exactly one
    /// <see cref="SnoGroup.PlayerClass"/> roster SnoName (e.g.
    /// <c>Sorc</c>→<c>Sorcerer</c>, <c>Spirit</c>→<c>Spiritborn</c>,
    /// <c>Warlock</c>→<c>Warlock</c>). Ambiguity or no match throws — the
    /// re-verify trigger, never a silent drift.
    /// </summary>
    /// <exception cref="CascFormatException">The name does not match the
    /// convention, or the class token is not a unique roster-name prefix
    /// (a re-verify signal — see Appendix D).</exception>
    private bool TryResolveBoardIdentity(
        string boardSnoName, out int classSnoId,
        out string className, out int boardIndex)
    {
        classSnoId = 0;
        className = string.Empty;
        boardIndex = -1;

        const string prefix = "Paragon_";
        if (!boardSnoName.StartsWith(prefix, StringComparison.Ordinal))
            throw new CascFormatException(
                $"ParagonBoard name '{boardSnoName}' does not match the " +
                "'Paragon_<Class>_<Index>' convention (§6.6 — re-verify).");

        var lastUs = boardSnoName.LastIndexOf('_');
        if (lastUs < prefix.Length)
            throw new CascFormatException(
                $"ParagonBoard name '{boardSnoName}' has no class/index " +
                "separator (§6.6 — re-verify).");

        var token = boardSnoName.Substring(prefix.Length, lastUs - prefix.Length);
        var idxText = boardSnoName.Substring(lastUs + 1);
        if (token.Length == 0 || !int.TryParse(idxText, out boardIndex))
            throw new CascFormatException(
                $"ParagonBoard name '{boardSnoName}' trailing index is not " +
                "an integer (§6.6 — re-verify).");

        var match = -1;
        var roster = PlayerClassRoster();
        for (var i = 0; i < roster.Length; i++)
        {
            if (!roster[i].SnoName.StartsWith(token, StringComparison.Ordinal))
                continue;
            if (match >= 0)
                throw new CascFormatException(
                    $"ParagonBoard class token '{token}' is an ambiguous " +
                    $"prefix of both '{roster[match].SnoName}' and " +
                    $"'{roster[i].SnoName}' (§6.6 / CL-16 — re-verify).");
            match = i;
        }
        if (match < 0)
            throw new CascFormatException(
                $"ParagonBoard class token '{token}' is not a prefix of any " +
                $"PlayerClass roster name (§6.6 / CL-16 — re-verify).");

        classSnoId = roster[match].SnoId;
        className = roster[match].SnoName;
        return true;
    }

    /// <summary>
    /// Resolve a <c>ParagonBoard</c>'s <b>localized display name</b> — the
    /// in-game board name ("Start", "Dynamism", "Pyrosis", …) — for a locale.
    /// </summary>
    /// <remarks>
    /// <para>The board's localized name is not on
    /// <see cref="ParagonBoardDefinition"/> (group 108) at all; it lives in
    /// the board's <b>sibling StringList table</b> (group
    /// <see cref="SnoGroup.StringList"/> = 42). The D4 convention, recovered
    /// clean-room and recorded in <c>docs/casc-diablo4-format.md §6.4</c>
    /// (Appendix A CL-15): the sibling table's CoreTOC name is
    /// <c>"ParagonBoard_" + boardSnoName</c> (e.g. board
    /// <c>Paragon_Warlock_00</c> → table
    /// <c>ParagonBoard_Paragon_Warlock_00</c>), and the localized string is
    /// under label <c>"Name"</c>. The SNO ids are unrelated (no fixed offset
    /// — Warlock happens to be board−1, Sorcerer is not); resolution is
    /// strictly name-keyed via <see cref="CoreToc"/>. Holds for every class
    /// (Barb/Druid/Necro/Paladin/Rogue/Sorc/Spirit/Warlock).</para>
    /// <para>Raw decoded value only — no fallback policy. If the board SNO
    /// name is unknown, the sibling table is absent, or it carries no
    /// <c>Name</c> label, this returns <see langword="false"/> and the
    /// consumer owns the fallback (e.g. show the SnoName identifier).</para>
    /// </remarks>
    /// <param name="boardSnoId">The <c>ParagonBoard</c> SNO id (group 108).</param>
    /// <param name="name">The localized board name, or <see cref="string.Empty"/>.</param>
    /// <param name="locale">Locale (default <see cref="DefaultLocale"/>).</param>
    /// <returns><see langword="true"/> iff a localized name was decoded.</returns>
    public bool TryReadParagonBoardName(
        int boardSnoId, out string name, string locale = DefaultLocale) =>
        TryReadSiblingString(
            SnoGroup.ParagonBoard, boardSnoId,
            ParagonBoardStringTablePrefix, ParagonBoardNameLabel,
            locale, out name);

    /// <summary>
    /// The generalized D4 <b>sibling-StringList convention</b> (FR-D1
    /// §6.4 generalized to §6.7 / CL-20): a record's localized text lives
    /// in the group-<see cref="SnoGroup.StringList"/> (42) SNO whose
    /// CoreTOC name is <c><paramref name="tablePrefix"/> + recordSnoName</c>,
    /// under <paramref name="label"/>. Strictly name-keyed via
    /// <see cref="CoreToc"/> (the SNO ids are unrelated). Raw decoded
    /// value only — no fallback policy; returns <see langword="false"/>
    /// (and <see cref="string.Empty"/>) when the record SNO name is
    /// unknown, the sibling table is absent, or the label is missing
    /// (honest sentinel; the consumer owns any fallback).
    /// </summary>
    private bool TryReadSiblingString(
        SnoGroup recordGroup, int recordSnoId, string tablePrefix,
        string label, string locale, out string text)
    {
        text = string.Empty;
        return CoreToc.TryGetName(recordGroup, recordSnoId, out var n)
            && CoreToc.TryGetId(SnoGroup.StringList, tablePrefix + n, out var t)
            && GetStrings(locale).TryGet(t, label, out text);
    }

    /// <summary>
    /// Resolve a <c>ParagonBoard</c>'s localized display name, throwing if it
    /// cannot be resolved. See <see cref="TryReadParagonBoardName"/> for the
    /// convention, the boundary (raw value only), and the no-fallback note;
    /// prefer that overload when the consumer owns an unknown-name fallback.
    /// </summary>
    /// <param name="boardSnoId">The <c>ParagonBoard</c> SNO id (group 108).</param>
    /// <param name="locale">Locale (default <see cref="DefaultLocale"/>).</param>
    /// <returns>The localized board name.</returns>
    /// <exception cref="SnoNotFoundException">The board SNO, its sibling
    /// StringList table, or the <c>Name</c> label could not be resolved.</exception>
    public string ReadParagonBoardName(
        int boardSnoId, string locale = DefaultLocale) =>
        TryReadParagonBoardName(boardSnoId, out var n, locale)
            ? n
            : throw new SnoNotFoundException(
                $"No localized name for ParagonBoard SNO {boardSnoId} " +
                $"(locale '{locale}'): sibling StringList table or '" +
                $"{ParagonBoardNameLabel}' label not found.");

    /// <summary>CoreTOC-name prefix of a <c>ParagonBoard</c>'s sibling
    /// StringList table (the board SnoName prefixed with this).</summary>
    private const string ParagonBoardStringTablePrefix = "ParagonBoard_";

    /// <summary>Label of the localized board name within the sibling
    /// StringList table.</summary>
    private const string ParagonBoardNameLabel = "Name";

    /// <summary>Resolve a <c>ParagonNode</c>'s engine user-facing
    /// tooltip-title via the §6.7 sibling-StringList convention:
    /// <c>ParagonNode_&lt;NodeSnoName&gt;</c> in group
    /// <see cref="SnoGroup.StringList"/>, label <c>Name</c>. The
    /// engine authors a sibling for every node that has its own
    /// tooltip header — structural nodes (<c>StartNodeBarb</c> /
    /// <c>StartNodeWarl</c>/etc. → <c>"Paragon Starting Node"</c>,
    /// <c>Generic_Gate</c> → <c>"Board Attachment Gate"</c>),
    /// class-specific rare nodes (<c>Warlock_Rare_006</c> →
    /// <c>"Binding"</c>, etc.), and named legendary nodes. Only the
    /// generic <c>Generic_&lt;Rarity&gt;_&lt;Token&gt;</c> stat-node
    /// family (the stat-shop of magic / rare-minor / rare-major /
    /// legendary affixes shared across classes —
    /// <c>Generic_Magic_DamageToElite</c>,
    /// <c>Generic_Magic_Armor</c>, etc.) has no sibling — the
    /// tooltip on those is composed by the engine from the
    /// stat-token + the rarity, not from an authored display name.
    /// Returns <see langword="false"/> + <see cref="string.Empty"/>
    /// for the no-sibling case (honest sentinel; consumer composes
    /// from <see cref="ParagonNodeInfo.Stats"/> /
    /// <see cref="ParagonNodeInfo.Kind"/>).</summary>
    /// <param name="nodeSnoId">The <c>ParagonNode</c> SNO id (group
    /// <see cref="SnoGroup.ParagonNode"/>).</param>
    /// <param name="name">The localized tooltip title, or
    /// <see cref="string.Empty"/>.</param>
    /// <param name="locale">Locale (default <see cref="DefaultLocale"/>).</param>
    public bool TryReadParagonNodeTitle(
        int nodeSnoId, out string name, string locale = DefaultLocale) =>
        TryReadSiblingString(
            SnoGroup.ParagonNode, nodeSnoId,
            ParagonNodeStringTablePrefix, ParagonBoardNameLabel,
            locale, out name);

    private const string ParagonNodeStringTablePrefix = "ParagonNode_";

    /// <summary>
    /// FR-C25 / FR-C27 — resolve an engine <c>AttributeId</c> (the raw
    /// <c>eAttribute</c> int on a <see cref="NodeAttribute"/> /
    /// <see cref="GlyphAffixAttributeRef"/> / <see cref="AffixEffect"/>) to its
    /// in-game localized display name via the <c>AttributeDescriptions</c>
    /// StringList (sno <c>4080</c>) — the same source the tooltip renderer uses.
    /// </summary>
    /// <remarks>
    /// <para><b>Pipeline (CL-88, season-robust).</b> The raw
    /// <c>AttributeId</c> is a registry ordinal the engine <b>renumbers every
    /// build</b> (Armor <c>481→482</c>, Damage-to-Elites <c>950→953</c>,
    /// high-health <c>1120→1123</c>, Barrier <c>1124→1127</c>, …), so it is not
    /// a durable key. Resolution is therefore (1) a runtime <c>id → node-name
    /// token</c> scan of the live <c>Generic_</c> nodes →
    /// <see cref="AttributeNames.LabelByToken"/> (the season-stable primary,
    /// auto-tracking each build's renumbering); then (2) a second
    /// <b>read-not-curated</b> source — the item-affix <c>Desc</c> placeholder
    /// token, which is itself a sno-4080 key, keyed by the current-build
    /// <c>AttributeId</c> (FR-C27 R2 / CL-97; resolves many ids the curated node
    /// path misses, e.g. <c>707 → "Damage Over Time"</c>); then (3) the curated
    /// <see cref="AttributeNames.LabelByAttributeId"/> fallback, restricted to
    /// the <b>stable low range</b> (<see cref="AttributeNames.StableAttributeIdRangeExclusiveMax"/>)
    /// — the drift-prone tail is intentionally absent so a shifted id returns
    /// an honest <see langword="null"/> rather than a stale wrong name
    /// (FR-C31 / CL-93); then (4) the compound base-id map. Ids no affix or
    /// curated token names (node/glyph-only stats) return
    /// <see langword="null"/> — the honest residual, never a wrong name. The
    /// affix source is built by a one-time full-affix scan on first use, cached.</para>
    /// <para><b>Flag-namespaced (negative) ids are out of scope here.</b> A
    /// negative <c>attributeId</c> (high bit <c>0x80000000</c> set) is a
    /// <c>DataAttributes</c> designer-table reference, a <b>disjoint</b>
    /// namespace — this method returns <see langword="null"/> for it (never
    /// <c>abs()</c> it into the engine table); resolve it via
    /// <see cref="TryGetDataAttributeName(int, out string)"/>. The <c>-1</c>
    /// "no attribute" sentinel also returns <see langword="null"/>.</para>
    /// <para>Returns <see langword="null"/> when the id resolves to no label,
    /// when the <c>AttributeDescriptions</c> bundle is missing for the locale,
    /// or when the label isn't in the table (honest sentinel — the consumer
    /// composes its own <c>"Attribute &lt;id&gt;"</c> fallback).</para>
    /// </remarks>
    /// <param name="attributeId">The raw <c>eAttribute</c> int.</param>
    /// <param name="locale">Locale (default
    /// <see cref="DefaultLocale"/>); routes through the per-locale
    /// StringList bundle via <see cref="GetStrings"/>.</param>
    /// <returns>The stripped display name (templates / placeholders
    /// / color tags removed) — e.g. <c>"Strength"</c> for id 9,
    /// <c>"Maximum Life"</c> for 133, <c>"Armor"</c> for 482,
    /// <c>"Damage to Elites"</c> for 953 (the current-build ids;
    /// stale predecessors 481/950 resolve to <see langword="null"/>).
    /// <see langword="null"/> on any of the unresolved cases above.</returns>
    public string? GetAttributeName(int attributeId, string locale = DefaultLocale)
    {
        if (!TryResolveBaseLabel(attributeId, out var label))
            return null;
        StringListCatalog stringList;
        try { stringList = GetStrings(locale); }
        catch (CascException) { return null; }
        if (!stringList.TryGet(AttributeDescriptionsSno, label, out var template))
            return null;
        var stripped = AttributeNames.StripTemplate(template);
        return string.IsNullOrEmpty(stripped) ? null : stripped;
    }

    /// <summary>
    /// FR-C28 (CL-85) — compound-key attribute-name resolution. Same
    /// pipeline as the single-id <see cref="GetAttributeName(int, string)"/>
    /// overload, but consults the
    /// <see cref="AttributeNames.LabelByCompoundKey"/> map first when
    /// <paramref name="paramPlus12"/> is non-sentinel (a real
    /// <c>ParamPlus12</c> GBID / enum / SNO ref). Resolves the
    /// tag-conditional cases the simple id can't disambiguate — e.g.
    /// <c>(259, 0x32ABA6FB) → "Demonology Damage"</c> on
    /// <c>Warlock_Rare_006</c>, <c>(254, 1) → "Fire Damage"</c> on a
    /// fire-typed elemental node, <c>(238, 0xCCA1AF65) → "Companion
    /// Cooldown Reduction"</c> on a Druid CDR node.
    /// </summary>
    /// <remarks>
    /// <para><b>Cascade.</b> (1) If <paramref name="paramPlus12"/> is
    /// the no-param sentinel (<c>0xFFFFFFFF</c>) → forward to
    /// <see cref="GetAttributeName(int, string)"/>. (2) Else, look up
    /// <c>(attributeId, paramPlus12)</c> in
    /// <see cref="AttributeNames.LabelByCompoundKey"/> — direct hit
    /// returns the curated enUS string. (3) Compound miss → fall through
    /// to the single-id lookup (so a partially-mapped attribute still
    /// surfaces the base label rather than nothing).</para>
    /// <para><b>Locale.</b> The compound map is enUS-only today
    /// (clean-room curated; the tag names are typically build-stable and
    /// don't need StringList resolution). The <paramref name="locale"/>
    /// argument is reserved for the future iteration that pipes the
    /// AttributeDescriptions template through the per-locale StringList
    /// and substitutes the per-tag name.</para>
    /// </remarks>
    /// <param name="attributeId">The raw <c>eAttribute</c> int.</param>
    /// <param name="paramPlus12">The associated <c>ParamPlus12</c>
    /// — a skill-tag GBID, element/status/form/resource enum, or
    /// power/weapon SNO ref depending on
    /// <paramref name="attributeId"/>'s semantics. Pass the
    /// <see cref="GlyphAffixAttributeRef.NoParam"/> sentinel
    /// (<c>0xFFFFFFFF</c>) for "no compound key" (forwards to the
    /// single-id overload).</param>
    /// <param name="locale">Locale (default
    /// <see cref="DefaultLocale"/>).</param>
    /// <returns>The resolved display string, or
    /// <see langword="null"/> when neither the compound map nor the
    /// single-id map has an entry.</returns>
    public string? GetAttributeName(int attributeId, uint paramPlus12,
        string locale = DefaultLocale)
    {
        if (paramPlus12 == GlyphAffixAttributeRef.NoParam)
            return GetAttributeName(attributeId, locale);

        // Season-robust primary: resolve the base id to its label, then key
        // the compound map on (baseLabel, paramPlus12) — both durable.
        if (TryResolveBaseLabel(attributeId, out var baseLabel) &&
            AttributeNames.NameByCompoundLabelKey.TryGetValue(
                (baseLabel, paramPlus12), out var byLabel))
            return byLabel;

        // Legacy id-keyed compound fallback, then the single-id resolution.
        if (AttributeNames.LabelByCompoundKey.TryGetValue(
                (attributeId, paramPlus12), out var compound))
            return compound;

        return GetAttributeName(attributeId, locale);
    }

    /// <summary>
    /// FR-C32 (CL-93) — resolve a <b>flag-namespaced</b> <c>AttributeId</c>
    /// (a negative id, high bit <c>0x80000000</c> set) to its
    /// <c>DataAttributes</c> designer-table name. These refs — carried on
    /// paragon nodes, glyph affixes, and item affixes for
    /// conditional/seasonal/per-power bonuses (Berserking, Shadowform,
    /// Demonform, Volatile, kill-streak, …) — are <b>not</b> engine
    /// <c>eAttribute</c> ids and do not resolve through
    /// <see cref="GetAttributeName(int, string)"/> (a disjoint namespace);
    /// this method reads them from the data-defined <c>DataAttributes</c>
    /// table (SNO <c>1907204</c>) by ordinal <c>attributeId &amp; 0x7FFFFFFF</c>.
    /// </summary>
    /// <remarks>
    /// <para>The returned <paramref name="name"/> is the table's authored
    /// token (e.g. <c>"Warlock_Demonform_Damage_Bonus"</c>,
    /// <c>"Multiplicative_Damage_Percent_Bonus_While_Volatile"</c>,
    /// <c>"Barb_Berserking_AttackSpeed"</c>) — the first-party designer name,
    /// not a localized display string (these conditional attributes have no
    /// standalone tooltip label; the consumer owns any humanization). The
    /// additive/multiplicative split is in the token (a
    /// <c>Multiplicative_</c> prefix distinguishes the pair member).</para>
    /// <para>Returns <see langword="false"/> — with <paramref name="name"/>
    /// set to <see cref="string.Empty"/> — for a non-flagged (positive)
    /// engine id, the <c>-1</c> "no attribute" sentinel, an ordinal outside
    /// the table, or when the <c>DataAttributes</c> record is unreadable.
    /// Verified: item-affix ordinal <c>84 = Barb_Berserking_AttackSpeed</c>;
    /// node/glyph ordinal <c>251 = Warlock_Demonform_Damage_Bonus</c>,
    /// <c>252 = Multiplicative_Warlock_Demonform_Damage_Bonus</c> — see
    /// <c>casc-diablo4-format.md §11.3</c>.</para>
    /// </remarks>
    /// <param name="attributeId">The raw (possibly flag-namespaced)
    /// <c>eAttribute</c> int, exactly as carried on
    /// <see cref="NodeAttribute.AttributeId"/> /
    /// <see cref="GlyphAffixAttributeRef.AttributeId"/> /
    /// <see cref="AffixEffect.AttributeId"/>.</param>
    /// <param name="name">The resolved <c>DataAttributes</c> token, or
    /// <see cref="string.Empty"/>.</param>
    /// <returns><see langword="true"/> iff <paramref name="attributeId"/> is a
    /// flagged ref that resolved to a table entry.</returns>
    public bool TryGetDataAttributeName(int attributeId, out string name)
    {
        name = string.Empty;
        if (attributeId >= 0) return false;                 // engine id, not flagged
        int ordinal = attributeId & 0x7FFFFFFF;             // strip the namespace flag
        var names = DataAttributeNames();
        if (ordinal < 0 || ordinal >= names.Length) return false;   // -1 sentinel lands here too
        if (names[ordinal].Length == 0) return false;
        name = names[ordinal];
        return true;
    }

    // FR-C27 (CL-88) — season-robust AttributeId → name resolution. The raw
    // AttributeId is a registry ordinal the engine renumbers each build, so
    // it can't be a durable key; the node-name token is. Scan the live
    // Generic_ nodes once for the current build's id→token map, then map the
    // token to a label via the season-stable AttributeNames.LabelByToken.
    private Dictionary<int, HashSet<string>>? _attributeTokens;

    /// <summary>Build (once, cached) the runtime
    /// <c>AttributeId → node-name token(s)</c> map by scanning every live
    /// <c>Generic_&lt;Rarity&gt;_&lt;Token&gt;</c> ParagonNode (group 106) and
    /// recording each of its attributes' name token. Because the engine's
    /// per-build AttributeId renumbering carries the tokens along, this map
    /// always reflects the current install.</summary>
    private Dictionary<int, HashSet<string>> AttributeTokens()
    {
        if (_attributeTokens is not null) return _attributeTokens;
        var map = new Dictionary<int, HashSet<string>>();
        foreach (var e in CoreToc.EntriesInGroup(SnoGroup.ParagonNode))
        {
            if (!e.Name.StartsWith("Generic_", StringComparison.Ordinal)) continue;
            int sep = e.Name.IndexOf('_', "Generic_".Length);   // '_' after the rarity
            if (sep < 0 || sep + 1 >= e.Name.Length) continue;
            var token = e.Name[(sep + 1)..];
            ParagonNodeDefinition node;
            try { node = ReadParagonNode(e.Id); }
            catch (CascException) { continue; }
            foreach (var a in node.Attributes)
            {
                if (a.AttributeId < 0) continue;   // 0x8000_0000-flagged variants — not registry ids
                if (!map.TryGetValue(a.AttributeId, out var set))
                    map[a.AttributeId] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(token);
            }
        }
        return _attributeTokens = map;
    }

    // FR-C27 R2 (CL-97) — a second, read-not-curated id→label source: item-affix
    // Desc placeholders. Each single-modifier affix's Desc names its modified
    // attribute with a token (e.g. "[Crit_Percent_Bonus * 100|%|]") that IS an
    // AttributeDescriptions (sno 4080) key, keyed by the current-build effect
    // AttributeId — so it resolves ids the Generic_ node scan misses, with no
    // curation. Built once (a full g104 scan) and cached.
    private Dictionary<int, string>? _affixAttributeTokens;

    /// <summary>Build (once, cached) the runtime
    /// <c>AttributeId → AttributeDescriptions label</c> map from item-affix
    /// <c>Desc</c> placeholders — the read-not-curated FR-C27 source (CL-97).
    /// Each single-modifier group-104 affix pairs its effect
    /// <see cref="AffixEffect.AttributeId"/> with the leading value token of its
    /// localized <c>Desc</c>, which is a season-stable sno-4080 key. Covers many
    /// ids the <see cref="AttributeTokens"/> node scan doesn't reach.</summary>
    private Dictionary<int, string> AffixAttributeTokens()
    {
        if (_affixAttributeTokens is not null) return _affixAttributeTokens;
        // Set the cache to an empty map first so any re-entrant lookup during
        // the build (there is none on this path, but be defensive) short-circuits
        // rather than recursing — and use the byte-only Parse + a direct sibling
        // Desc read, NOT ReadAffix (which resolves names via GetAttributeName and
        // would recurse into this scan).
        var map = new Dictionary<int, string>();
        _affixAttributeTokens = map;
        foreach (var e in CoreToc.EntriesInGroup(SnoGroup.Affix))
        {
            AffixDefinition a;
            try { a = AffixDefinition.Parse(ReadSno(SnoGroup.Affix, e.Id)); }
            catch (CascException) { continue; }
            if (a.Effects.Count != 1) continue;             // single-modifier → clean id↔token
            int id = a.Effects[0].AttributeId;
            if (id <= 0 || map.ContainsKey(id)) continue;
            if (!TryReadSiblingString(SnoGroup.Affix, e.Id, "Affix_", "Desc", DefaultLocale, out var desc))
                continue;
            int lb = desc.IndexOf('[');
            if (lb < 0) continue;
            int j = lb + 1;
            while (j < desc.Length && desc[j] == ' ') j++;
            int start = j;
            while (j < desc.Length && (char.IsLetterOrDigit(desc[j]) || desc[j] == '_')) j++;
            var token = desc[start..j];
            if (token.Length < 3 || char.IsDigit(token[0])) continue;
            map[id] = token;
        }
        return _affixAttributeTokens = map;
    }

    // FR-C32 (CL-93) — the DataAttributes (SNO 1907204, group 20) designer
    // table, read once and cached as an ordinal-indexed name array. A flagged
    // (negative) AttributeId references it by ordinal (id & 0x7FFFFFFF); see
    // TryGetDataAttributeName. Layout (verified 3.1.1.72836): the entry array
    // is a VLA whose descriptor is at payload +80 (dataOff) / +84 (byteSize);
    // each entry is a fixed 360-byte record with the ASCII szName at +0.
    private string[]? _dataAttributeNames;
    private const int DataAttributesSno = 1907204;
    private const int DataAttributesDescriptorOffset = 80;
    private const int DataAttributesEntryStride = 360;
    private const int DataAttributesNameMaxLength = 256;

    private string[] DataAttributeNames()
    {
        if (_dataAttributeNames is not null) return _dataAttributeNames;
        byte[] b;
        try { b = ReadSno(SnoGroup.GameBalance, DataAttributesSno); }
        catch (CascException) { return _dataAttributeNames = []; }

        const int pbase = SnoRecord.DefaultPayloadBase;
        if (b.Length < pbase + DataAttributesDescriptorOffset + 8)
            return _dataAttributeNames = [];
        int dataOff = BitConverter.ToInt32(b, pbase + DataAttributesDescriptorOffset);
        int byteSize = BitConverter.ToInt32(b, pbase + DataAttributesDescriptorOffset + 4);
        if (dataOff <= 0 || byteSize <= 0 || pbase + dataOff + byteSize > b.Length)
            return _dataAttributeNames = [];

        int count = byteSize / DataAttributesEntryStride;
        var names = new string[count];
        int start = pbase + dataOff;
        for (int i = 0; i < count; i++)
        {
            int entry = start + i * DataAttributesEntryStride;
            int len = 0;
            while (len < DataAttributesNameMaxLength
                   && entry + len < b.Length && b[entry + len] != 0)
                len++;
            names[i] = System.Text.Encoding.ASCII.GetString(b, entry, len);
        }
        return _dataAttributeNames = names;
    }

    /// <summary>Resolve an <c>AttributeId</c> to its season-stable
    /// <c>AttributeDescriptions</c> base label. Cascade: (1) the runtime
    /// id→token scan → <see cref="AttributeNames.LabelByToken"/> (tracks the
    /// engine's per-build renumbering); (2) the curated
    /// <see cref="AttributeNames.LabelByAttributeId"/> fallback (ids without a
    /// scannable token); (3) <see cref="AttributeNames.CompoundBaseLabelById"/>
    /// (the tag/element/resource base ids).</summary>
    private bool TryResolveBaseLabel(int attributeId, out string label)
    {
        if (AttributeTokens().TryGetValue(attributeId, out var tokens))
            foreach (var token in tokens)
                if (AttributeNames.LabelByToken.TryGetValue(token, out var byToken))
                {
                    label = byToken;
                    return true;
                }
        // CL-97 (FR-C27 R2) — read-not-curated: an item-affix Desc placeholder
        // token IS a sno-4080 label, keyed by the current-build AttributeId, so
        // it resolves ids the curated LabelByToken node path misses (no
        // hand-curation, auto-tracks renumbering). One-time affix scan on first
        // miss, cached.
        if (AffixAttributeTokens().TryGetValue(attributeId, out var affixLabel))
        {
            label = affixLabel;
            return true;
        }
        // Defensive by-id fallback — restricted to the stable low range; the
        // drift-prone tail (≥ 481) is season-fragile and must resolve via the
        // token scan above or return null, never a stale by-id name (FR-C31).
        if (attributeId < AttributeNames.StableAttributeIdRangeExclusiveMax &&
            AttributeNames.LabelByAttributeId.TryGetValue(attributeId, out var legacy))
        {
            label = legacy;
            return true;
        }
        if (AttributeNames.CompoundBaseLabelById.TryGetValue(attributeId, out var compoundBase))
        {
            label = compoundBase;
            return true;
        }
        label = string.Empty;
        return false;
    }

    /// <summary>The canonical SNO id of the
    /// <c>AttributeDescriptions</c> StringList in
    /// <see cref="SnoGroup.StringList"/> — the per-attribute display-
    /// name templates the engine renders tooltips from.</summary>
    public const int AttributeDescriptionsSno = 4080;

    /// <summary>
    /// The current build's playable character-class roster + localized
    /// display names (FR-D2), first-party from D4's own class data —
    /// independent of paragon (the roster is correct even if paragon is out
    /// of scope).
    /// </summary>
    /// <remarks>
    /// Decoded clean-room (§6.5 / CL-17): the roster is
    /// <see cref="SnoGroup.PlayerClass"/> (74); a group-74 entry is a real
    /// playable class iff the <c>General</c> StringList table (SNO
    /// <see cref="GeneralStringTableSno"/>) has label
    /// <c>"PlayerClass" + SnoName + "Male"</c> — this data-driven membership
    /// test excludes non-class junk (e.g. <c>Axe Bad Data</c>) with no
    /// hardcoded list. <see cref="CharacterClass.DisplayName"/> is that
    /// label's localized value (the markup-free display form). Ordered by
    /// <see cref="CharacterClass.SnoId"/> ascending for determinism; the
    /// SnoId is the stable per-class key (never an array position), so the
    /// list survives a class being added/reordered next season. Cached per
    /// locale. Raw decoded values only — no policy/imaging.
    /// </remarks>
    /// <param name="locale">Locale (default <see cref="DefaultLocale"/>).</param>
    public IReadOnlyList<CharacterClass> ReadCharacterClasses(
        string locale = DefaultLocale)
    {
        lock (_gate)
        {
            if (_classes.TryGetValue(locale, out var cached)) return cached;

            var strings = GetStrings(locale);
            var list = new List<CharacterClass>();
            foreach (var e in CoreToc.EntriesInGroup(SnoGroup.PlayerClass))
            {
                if (strings.TryGet(GeneralStringTableSno,
                        PlayerClassLabelPrefix + e.Name + PlayerClassLabelSuffix,
                        out var display))
                    list.Add(new CharacterClass(e.Id, e.Name, display));
            }
            list.Sort((a, b) => a.SnoId.CompareTo(b.SnoId));
            IReadOnlyList<CharacterClass> ro = list;
            _classes[locale] = ro;
            return ro;
        }
    }

    /// <summary>The locale-independent (SnoId, SnoName) class roster used to
    /// resolve a paragon board's class (FR-D1). Membership is decided via the
    /// <see cref="DefaultLocale"/> <c>General</c> table (label existence is
    /// locale-independent); cached.</summary>
    private (int SnoId, string SnoName)[] PlayerClassRoster()
    {
        lock (_gate)
        {
            if (_classRoster is not null) return _classRoster;
            var src = ReadCharacterClasses(DefaultLocale);
            var roster = new (int, string)[src.Count];
            for (var i = 0; i < src.Count; i++)
                roster[i] = (src[i].SnoId, src[i].SnoName);
            _classRoster = roster;
            return roster;
        }
    }

    /// <summary>
    /// The §6.5 class roster as <c>(SnoId, Rank)</c>, where <c>Rank</c> is
    /// the class's position when the roster is ordered ascending by its
    /// <c>eClass</c> ordinal (PlayerClass record payload <c>+16</c>) — the
    /// first-party slot order of the glyph <c>fUsableByClass</c> array
    /// (FR-D3, §7.3 / CL-18). Locale-independent; cached.
    /// </summary>
    /// <remarks>
    /// Data-driven, not a hardcoded order: on the verified build the
    /// eClass ordinals are sparse (Sorcerer 0, Barbarian 1, Rogue 3,
    /// Druid 5, Necromancer 6, Spiritborn 7, Paladin 9, Warlock 10) and
    /// rank-compact to 0..7 — independently corroborated by the
    /// explicitly-named <c>*_Necro</c> glyphs (rank 4 = Necromancer) and
    /// the consumer's empirically-verified Warlock = index 7.
    /// </remarks>
    private (int SnoId, int Rank)[] PlayerClassRanks()
    {
        lock (_gate)
        {
            if (_classRanks is not null) return _classRanks;
            var roster = PlayerClassRoster();
            var byEClass = new (int SnoId, int EClass)[roster.Length];
            for (var i = 0; i < roster.Length; i++)
            {
                var rec = new SnoRecord(
                    ReadSno(SnoGroup.PlayerClass, roster[i].SnoId));
                byEClass[i] = (roster[i].SnoId, (int)rec.U32(PlayerClassEClassOffset));
            }
            Array.Sort(byEClass, (a, b) => a.EClass.CompareTo(b.EClass));
            var ranks = new (int, int)[byEClass.Length];
            for (var r = 0; r < byEClass.Length; r++)
                ranks[r] = (byEClass[r].SnoId, r);
            _classRanks = ranks;
            return ranks;
        }
    }

    /// <summary>PlayerClass record payload offset of the <c>eClass</c>
    /// ordinal (the game's internal class enum value; ranked to give the
    /// glyph class-array slot order — §7.3 / CL-18).</summary>
    private const int PlayerClassEClassOffset = 16;

    /// <summary>Glyph record payload offset of the <c>fUsableByClass</c>
    /// per-class boolean fixed array (int32 per slot; slot = eClass
    /// rank).</summary>
    private const int GlyphUsableByClassOffset = 0x24;

    /// <summary>Glyph record payload offset whose value is the affix
    /// array's <c>dataOffset</c> (== 104 for a well-formed glyph). Used as
    /// the structural well-formed guard so malformed/placeholder records
    /// (e.g. the <c>Axe Bad Data</c> junk SNO) yield an empty membership
    /// instead of a silently-wrong class set.</summary>
    private const int GlyphAffixDescriptorOffset = 0x50;

    /// <summary>The <c>General</c> StringList table SNO that holds the
    /// localized class names (build-stable; re-verify per Appendix D).</summary>
    private const int GeneralStringTableSno = 4118;

    /// <summary>Class-name label = this + the PlayerClass SnoName + the
    /// suffix. The gendered form is the markup-free display string (the base
    /// <c>PlayerClass&lt;SnoName&gt;</c> label carries <c>|5sing:plur</c>
    /// markup); the gender variants are identical display strings.</summary>
    private const string PlayerClassLabelPrefix = "PlayerClass";

    /// <summary>See <see cref="PlayerClassLabelPrefix"/>.</summary>
    private const string PlayerClassLabelSuffix = "Male";

    /// <summary>Read + decode a <see cref="ParagonNodeDefinition"/> by SNO
    /// id (group 106).</summary>
    public ParagonNodeDefinition ReadParagonNode(int id) =>
        ParagonNodeDefinition.Parse(ReadSno(SnoGroup.ParagonNode, id));

    /// <summary>
    /// Read + decode a <see cref="ParagonGlyphDefinition"/> by SNO id
    /// (group 111), with its class membership resolved
    /// (<see cref="ParagonGlyphDefinition.UsableByClassSnoIds"/> — FR-D3).
    /// </summary>
    /// <remarks>
    /// The record's <c>fUsableByClass</c> boolean fixed array (payload
    /// <c>+0x24</c>) is indexed by <b>eClass rank</b> (see
    /// <see cref="PlayerClassRanks"/>); membership = the §6.5 PlayerClass
    /// SNO ids whose rank slot is non-zero. Only resolved for a
    /// structurally well-formed glyph (the affix descriptor at payload
    /// <c>+0x50</c> == 104); malformed/placeholder records get an empty
    /// set (honest sentinel, per the durable opaque-id boundary —
    /// Appendix C / CL-18). Raw decoded values only.
    /// </remarks>
    public ParagonGlyphDefinition ReadParagonGlyph(int id) =>
        ReadParagonGlyph(id, DefaultLocale);

    /// <summary>Read + decode a <see cref="ParagonGlyphDefinition"/>
    /// with the FR-C24 localized fields populated
    /// (<see cref="ParagonGlyphDefinition.LocalizedTitle"/> from the
    /// <c>ParagonGlyph_&lt;SnoName&gt;</c> sibling, label <c>Name</c>;
    /// <see cref="ParagonGlyphDefinition.Rarity"/> from the SnoName's
    /// leading-token convention). CL-86 swapped CL-79's
    /// <c>Item_ParagonGlyph_&lt;SnoName&gt;</c> sibling for the
    /// non-<c>Item_</c>-prefixed table — the prefixed one is missing for
    /// the <c>Rare_&lt;Stat&gt;_Generic</c> shape (e.g.
    /// <c>Rare_Will_Generic</c> = <c>Headhunter</c>) while the
    /// non-prefixed table exists for every glyph and carries the bare
    /// title directly (no <c>"Glyph: "</c> prefix to strip).</summary>
    public ParagonGlyphDefinition ReadParagonGlyph(int id, string locale)
    {
        var blob = ReadSno(SnoGroup.ParagonGlyph, id);
        var glyph = ParagonGlyphDefinition.Parse(blob);

        var rec = new SnoRecord(blob);
        // Structural well-formed guard: a real glyph has the affix array
        // descriptor (dataOffset == 104) here; the placeholder/junk record
        // does not. Avoids emitting a silently-wrong class set.
        if (rec.PayloadBase + GlyphAffixDescriptorOffset + 4 <= rec.Length &&
            rec.U32(GlyphAffixDescriptorOffset) == 104)
        {
            var ranks = PlayerClassRanks();
            var hits = new List<int>(ranks.Length);
            foreach (var (snoId, rank) in ranks)
            {
                var off = GlyphUsableByClassOffset + rank * 4;
                if (rec.PayloadBase + off + 4 <= rec.Length &&
                    rec.U32(off) != 0)
                    hits.Add(snoId);
            }
            if (hits.Count > 0) glyph.SetUsableByClassSnoIds(hits.ToArray());
        }

        // FR-C24 / CL-86 — localized title via the sibling
        // ParagonGlyph_<SnoName> StringList (no Item_ prefix). The
        // CL-79 Item_ParagonGlyph_<SnoName> table carries "Glyph:
        // <Title>" + a Description label, but it's only emitted for the
        // numbered Rare_<NN>_<Stat>_<Slot> shape; the
        // Rare_<Stat>_Generic shape (e.g. Rare_Will_Generic =
        // "Headhunter") only has the non-prefixed sibling. The
        // non-prefixed table covers every glyph + carries the bare
        // title directly, so it's the canonical source.
        var title = string.Empty;
        if (TryReadSiblingString(
                SnoGroup.ParagonGlyph, id,
                ParagonGlyphStringTablePrefix, ParagonBoardNameLabel,
                locale, out var raw))
        {
            title = raw;
        }
        glyph.SetLocalizedFields(title, GlyphRarityFromSnoName(id));
        return glyph;
    }

    /// <summary>The leading-token convention: every glyph's CoreTOC
    /// name on the live build (3.0.2.71886) starts with
    /// <c>Rare_&lt;NN&gt;_&lt;Stat&gt;_&lt;Slot&gt;</c>. Forward-looking
    /// for any future Magic / Legendary glyphs the engine adds.</summary>
    private ParagonRarity GlyphRarityFromSnoName(int snoId)
    {
        var name = CoreToc.GetName(SnoGroup.ParagonGlyph, snoId);
        if (string.IsNullOrEmpty(name)) return ParagonRarity.Common;
        var underscore = name.IndexOf('_');
        if (underscore <= 0) return ParagonRarity.Common;
        return name[..underscore] switch
        {
            "Magic" => ParagonRarity.Magic,
            "Rare" => ParagonRarity.Rare,
            "Legendary" => ParagonRarity.Legendary,
            _ => ParagonRarity.Common,
        };
    }

    private const string ParagonGlyphStringTablePrefix = "ParagonGlyph_";

    /// <summary>Read + decode a <see cref="ParagonGlyphAffixDefinition"/> by
    /// SNO id (group 112).</summary>
    public ParagonGlyphAffixDefinition ReadParagonGlyphAffix(int id) =>
        ReadParagonGlyphAffix(id, DefaultLocale);

    /// <summary>Read + decode a
    /// <see cref="ParagonGlyphAffixDefinition"/> with the FR-C24 (CL-79)
    /// localized
    /// <see cref="ParagonGlyphAffixDefinition.Description"/> populated
    /// (sibling <c>ParagonGlyphAffix_&lt;SnoName&gt;</c>, label
    /// <c>Desc</c>; raw template text with all engine markup
    /// preserved).</summary>
    public ParagonGlyphAffixDefinition ReadParagonGlyphAffix(int id, string locale)
    {
        var affix = ParagonGlyphAffixDefinition.Parse(
            ReadSno(SnoGroup.ParagonGlyphAffix, id));
        if (TryReadSiblingString(
                SnoGroup.ParagonGlyphAffix, id,
                ParagonGlyphAffixStringTablePrefix, ParagonGlyphAffixDescLabel,
                locale, out var desc))
        {
            affix.SetDescription(desc);
        }
        return affix;
    }

    private const string ParagonGlyphAffixStringTablePrefix = "ParagonGlyphAffix_";
    private const string ParagonGlyphAffixDescLabel = "Desc";

    /// <summary>Read + decode a <see cref="StatTagDefinition"/> by SNO id
    /// (group <see cref="SnoGroup.StatTag"/>=124) — the stat-threshold tag
    /// referenced from a rare paragon node's
    /// <see cref="ParagonNodeDefinition.BonusStatTagSnoIds"/>. Returns the
    /// raw formula text only (evaluation is the consumer's, per Appendix
    /// C).</summary>
    public StatTagDefinition ReadStatTag(int id) =>
        StatTagDefinition.Parse(ReadSno(SnoGroup.StatTag, id));

    /// <summary>Non-throwing variant of <see cref="ReadStatTag"/> — returns
    /// <see langword="false"/> when the SNO is missing or unreadable;
    /// equivalent to the typed-reader convention used elsewhere on this
    /// facade.</summary>
    public bool TryReadStatTag(int id, out StatTagDefinition tag)
    {
        if (TryReadSno(SnoGroup.StatTag, id, SnoFolder.Meta, out var blob))
        {
            tag = StatTagDefinition.Parse(blob);
            return true;
        }
        tag = null!;
        return false;
    }

    /// <summary>Read + decode the GameBalance <see cref="AttributeFormulaTable"/>
    /// (default SNO <c>201912</c>, the paragon formula table). Returns
    /// formula <i>text</i> + name/GBID indices only — evaluation and the
    /// calibrated intrinsics stay with the consumer.</summary>
    public AttributeFormulaTable ReadAttributeFormulas(int id = 201912) =>
        AttributeFormulaTable.Parse(ReadSno(SnoGroup.GameBalance, id));

    /// <summary>FR-C29 Phase 2 (CL-99) — read + decode the GameBalance
    /// <see cref="LevelScalingTable"/> (default SNO <c>206158</c>): the
    /// per-level <c>hpScalar</c> curve, and via it the class-independent base
    /// Max Life projection (<see cref="LevelScalingTable.BaseLife(int)"/>).</summary>
    public LevelScalingTable ReadLevelScaling(int id = 206158) =>
        LevelScalingTable.Parse(ReadSno(SnoGroup.GameBalance, id));

    /// <summary>Read + decode the <see cref="DifficultyTiersTable"/> (FR-C34,
    /// CL-101) — the per-<b>monster-level</b> scaling curve (SNO
    /// <see cref="DifficultyTiersTable.DefaultSnoId"/> = 1973217, group
    /// <see cref="SnoGroup.GameBalance"/>): 150 rows (monster levels 1..150)
    /// with per-level HP/damage multipliers, the XP-value anchor column, and a
    /// candidate gold column, plus the raw coefficient vector. This is the
    /// monster/content analogue of <see cref="ReadLevelScaling(int)"/> — a
    /// separate, far steeper curve (see the type remarks / §8.2).</summary>
    /// <param name="id">The <c>DifficultyTiers</c> SNO id (defaults to
    /// <c>1973217</c>).</param>
    /// <exception cref="CascFormatException">The blob's row VLA is malformed.</exception>
    public DifficultyTiersTable ReadDifficultyTiers(int id = DifficultyTiersTable.DefaultSnoId) =>
        DifficultyTiersTable.Parse(ReadSno(SnoGroup.GameBalance, id));

    /// <summary>Read the <see cref="MonsterLevelCurvesTable"/> (FR-C36, CL-110) —
    /// the six per-raid-tier (<c>Raid_Tier_0..5</c>) monster-level scaling curves.
    /// Each tier maps a monster/area level to a scaled effective value (climbing
    /// to 100 across the tier's level span). Corrects the earlier "not in the
    /// data" finding — the curves are in this SNO.</summary>
    /// <param name="id">The <c>MonsterLevelCurves</c> SNO id (defaults to
    /// <c>1610053</c>).</param>
    /// <exception cref="CascFormatException">The tier-list VLA is malformed.</exception>
    public MonsterLevelCurvesTable ReadMonsterLevelCurves(int id = MonsterLevelCurvesTable.DefaultSnoId) =>
        MonsterLevelCurvesTable.Parse(ReadSno(SnoGroup.GameBalance, id));

    private const int SkillTreeRewardsSno = 547685;   // g20 per-node metadata table
    private const int SkillTreeBoardGroup = 39;        // class board (g39)

    // (g39 board SNO, SkillTreeRewards name prefixes) per class, in SkillTreeClass order.
    private static readonly (int Board, string[] Prefixes)[] SkillTreeClasses =
    [
        (169806,  ["Barb_", "Barbarian_"]),   // Barbarian
        (164706,  ["Druid_"]),                // Druid
        (199280,  ["Necro_", "Necromancer_"]),// Necromancer
        (2336990, ["Paladin_"]),              // Paladin
        (199278,  ["Rogue_"]),                // Rogue
        (72984,   ["Sorc_", "Sorcerer_"]),    // Sorcerer
        (1663193, ["Spiritborn_"]),           // Spiritborn
        (2208849, ["Warlock_"]),              // Warlock
    ];

    /// <summary>
    /// Read a class's <see cref="SkillTree"/> (#57, CL-111) — the logical per-node
    /// tree: every unlock / skill-rank / modifier / talent node with its kind, the
    /// skill it is/modifies, and its mutually-exclusive modifier group, plus the
    /// class's active skills. Node effect text resolves through
    /// <see cref="SkillTreeNode.SkillSno"/> → <see cref="ReadPower(int, string)"/>.
    /// The visual graph (positions/edges) is intentionally not surfaced — see the
    /// <see cref="SkillTree"/> remarks.
    /// </summary>
    /// <param name="classId">The character class.</param>
    public SkillTree ReadSkillTree(SkillTreeClass classId)
    {
        var (board, prefixes) = SkillTreeClasses[(int)classId];
        return new SkillTree(classId, ReadSkillTreeNodes(prefixes), ReadSkillTreeSkills(board));
    }

    private SkillTreeNode[] ReadSkillTreeNodes(string[] prefixes)
    {
        var blob = ReadSno(SnoGroup.GameBalance, SkillTreeRewardsSno);
        var r = new SnoRecord(blob);
        int len = blob.Length, pb = r.PayloadBase;
        const int first = 88, stride = 284, nameBuf = 256;
        int count = (len - pb - first) / stride;
        var list = new List<SkillTreeNode>();
        for (int i = 0; i < count; i++)
        {
            int rec = first + i * stride;
            if (pb + rec + stride > len) break;
            int nameMax = Math.Min(nameBuf, len - pb - rec);
            string name = ReadCString(r, rec, nameMax);
            bool match = false;
            foreach (var p in prefixes)
                if (name.StartsWith(p, StringComparison.Ordinal)) { match = true; break; }
            if (!match) continue;
            int t = rec + 256;                         // 7-int32 tail
            int skill = r.I32(t + 8);                  // F2 = modified-skill Power SNO
            var kind = MapSkillNodeKind(r.I32(t + 16));// F4 = node type
            int group = r.I32(t + 20);                 // F5 = modifier group id
            list.Add(new SkillTreeNode(
                name, kind, skill < 0 ? 0 : skill,
                kind == SkillTreeNodeKind.Modifier ? group : -1));
        }
        return list.ToArray();
    }

    private int[] ReadSkillTreeSkills(int boardSno)
    {
        if (!TryReadSno(SkillTreeBoardGroup, boardSno, SnoFolder.Meta, out var blob)) return [];
        var r = new SnoRecord(blob);
        int len = blob.Length, pb = r.PayloadBase;
        if (pb + 16 + 8 > len) return [];              // skill-list descriptor @ +0x10
        int dataOff = r.I32(16), size = r.I32(20);
        if (dataOff <= 0 || size <= 0 || size % 8 != 0 || pb + dataOff + size > len) return [];
        int n = size / 8;                              // pairs of (skill SNO, flag)
        var skills = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            int sno = r.I32(dataOff + i * 8);
            if (sno > 0) skills.Add(sno);
        }
        return skills.ToArray();
    }

    private static SkillTreeNodeKind MapSkillNodeKind(int f4) => f4 switch
    {
        15 or 5 => SkillTreeNodeKind.Unlock,   // 5 = the Spiritborn-board unlock variant
        2 => SkillTreeNodeKind.SkillRank,
        3 or 12 => SkillTreeNodeKind.Modifier, // 12 = a default-modifier variant
        1 => SkillTreeNodeKind.Talent,
        _ => SkillTreeNodeKind.Other,
    };

    private static string ReadCString(SnoRecord r, int payloadOffset, int maxLength)
    {
        if (maxLength <= 0) return string.Empty;
        string raw = r.Ascii(payloadOffset, maxLength);
        int end = 0;
        while (end < raw.Length && raw[end] is >= (char)0x20 and < (char)0x7F) end++;
        return raw[..end];
    }

    /// <summary>Read the <see cref="MonsterNameRegistry"/> (FR-C35, CL-105) — the
    /// localized name-affix fragments the game composes into elite/special
    /// monster display names (e.g. <c>FrozenSuffix004</c> → <c>"Frostburn"</c>).
    /// Resolved from the <c>MonsterNames</c> StringList (group 42) via CoreTOC;
    /// returns an empty registry (never throws) if that table is absent for
    /// <paramref name="locale"/>.</summary>
    /// <param name="locale">Locale for the fragment text (default
    /// <see cref="DefaultLocale"/>).</param>
    public MonsterNameRegistry ReadMonsterNames(string locale = DefaultLocale)
    {
        if (!CoreToc.TryGetId(SnoGroup.StringList, MonsterNameRegistry.StringListName, out var sno))
            return MonsterNameRegistry.FromEntries(
                locale, System.Array.Empty<System.Collections.Generic.KeyValuePair<string, string>>());
        var table = GetStrings(locale).Table(sno);
        return MonsterNameRegistry.FromEntries(
            locale,
            (System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>?)table?.Entries
                ?? System.Array.Empty<System.Collections.Generic.KeyValuePair<string, string>>());
    }

    /// <summary>
    /// #51 (CL-106) — the inverted <b>affix pool</b> query: every gear affix that
    /// can roll on the given <c>eItemType</c> ordinal (its
    /// <see cref="AffixDefinition.AllowedItemTypes"/> contains
    /// <paramref name="itemTypeId"/>). The convenience built on the per-affix
    /// <see cref="AffixDefinition.AllowedItemTypes"/> primitive — "what can roll
    /// on this item type."
    /// </summary>
    /// <remarks>Lazy; each yielded affix is decoded <b>byte-only</b> (identity +
    /// <see cref="AffixDefinition.Effects"/> + <see cref="AffixDefinition.AllowedItemTypes"/>,
    /// no localized <c>Name</c>/<c>Desc</c> — call
    /// <see cref="ReadAffix(int, string)"/> with the affix's
    /// <see cref="AffixDefinition.SnoId"/> for those). A full pass over the group-104
    /// affix group; cache the result if you query many types (the pool is stable
    /// per build).</remarks>
    /// <param name="itemTypeId">The engine <c>eItemType</c> ordinal (as it appears
    /// in <see cref="AffixDefinition.AllowedItemTypes"/>).</param>
    public System.Collections.Generic.IEnumerable<AffixDefinition> RollableAffixes(int itemTypeId)
    {
        foreach (var e in CoreToc.Entries)
        {
            if ((int)e.Group != (int)SnoGroup.Affix) continue;
            var affix = TryParseAffixByteOnly(e.Id);
            if (affix is null) continue;
            var types = affix.AllowedItemTypes;
            for (int i = 0; i < types.Count; i++)
                if (types[i] == itemTypeId) { yield return affix; break; }
        }
    }

    private AffixDefinition? TryParseAffixByteOnly(int id)
    {
        try { return AffixDefinition.Parse(ReadSno(SnoGroup.Affix, id)); }
        catch { return null; }
    }

    // ----- C6 typed record readers (identity + localized text) ----------
    // Scope-unfrozen by owner 2026-05-17. Raw decoded data only; deep
    // gameplay modeling remains the consumer's domain (Appendix C). The
    // localized fields use the generalized sibling-StringList convention
    // (§6.7 / CL-20).

    /// <summary>Read + decode a <see cref="PlayerClassDefinition"/> by SNO
    /// id (group <see cref="SnoGroup.PlayerClass"/> = 74) — <c>SnoId</c> +
    /// the binary <c>eClass</c> ordinal (§11.1 / CL-21).</summary>
    public PlayerClassDefinition ReadPlayerClass(int id) =>
        PlayerClassDefinition.Parse(ReadSno(SnoGroup.PlayerClass, id));

    /// <summary>Read + decode a <see cref="PowerDefinition"/> by SNO id
    /// (group <see cref="SnoGroup.Power"/> = 29): identity + the localized
    /// <c>name</c>/<c>desc</c> from the sibling <c>Power_&lt;snoName&gt;</c>
    /// StringList table (§11.2 / CL-22). Localized fields are empty (honest
    /// sentinel) when the power has no sibling table.</summary>
    /// <param name="id">The Power SNO id.</param>
    /// <param name="locale">Locale (default <see cref="DefaultLocale"/>).</param>
    public PowerDefinition ReadPower(int id, string locale = DefaultLocale)
    {
        var p = PowerDefinition.Parse(ReadSno(SnoGroup.Power, id));
        TryReadSiblingString(SnoGroup.Power, id, "Power_", "name", locale, out var n);
        TryReadSiblingString(SnoGroup.Power, id, "Power_", "desc", locale, out var d);
        p.SetStrings(n, d);
        p.SetModifiers(ReadPowerModifiers(id, locale));
        return p;
    }

    /// <summary>LIB-5 (CL-104) — decode a skill's selectable modifiers (skill-tree
    /// enhancement / upgrade nodes) from its sibling StringList
    /// (<c>Power_&lt;snoName&gt;</c>) <c>Mod&lt;N&gt;_Name</c> /
    /// <c>Mod&lt;N&gt;_Description</c> labels, ordered by the sparse modifier
    /// index. Empty when the power has no sibling table or no modifier labels
    /// (passives / non-skill powers).</summary>
    private IReadOnlyList<PowerModifier> ReadPowerModifiers(int powerId, string locale)
    {
        if (!CoreToc.TryGetName(SnoGroup.Power, powerId, out var name) ||
            !CoreToc.TryGetId(SnoGroup.StringList, "Power_" + name, out var tableSno))
            return System.Array.Empty<PowerModifier>();
        var table = GetStrings(locale).Table(tableSno);
        if (table is null) return System.Array.Empty<PowerModifier>();

        List<PowerModifier>? mods = null;
        foreach (var (label, value) in table.Entries)
        {
            // Labels are shaped "Mod<N>_Name"; pair each with "Mod<N>_Description".
            if (!label.StartsWith("Mod", System.StringComparison.Ordinal) ||
                !label.EndsWith("_Name", System.StringComparison.Ordinal))
                continue;
            var digits = label.AsSpan(3, label.Length - 3 - "_Name".Length);
            if (digits.Length == 0 || !int.TryParse(digits, out var idx)) continue;
            table.TryGet("Mod" + idx.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "_Description", out var desc);
            (mods ??= new List<PowerModifier>()).Add(new PowerModifier(idx, value, desc));
        }
        if (mods is null) return System.Array.Empty<PowerModifier>();
        mods.Sort((a, b) => a.Index.CompareTo(b.Index));
        return mods;
    }

    /// <summary>Read + decode an <see cref="AffixDefinition"/> by SNO id
    /// (group <see cref="SnoGroup.Affix"/> = 104): identity + the localized
    /// <c>Name</c> and <c>Desc</c> from the sibling <c>Affix_&lt;snoName&gt;</c>
    /// StringList table (§11.3 / CL-87 / CL-22). Each localized field is an
    /// honest empty sentinel when its label is absent (many system/internal
    /// affixes carry a <c>Desc</c> but no <c>Name</c>).</summary>
    /// <param name="id">The Affix SNO id.</param>
    /// <param name="locale">Locale (default <see cref="DefaultLocale"/>).</param>
    public AffixDefinition ReadAffix(int id, string locale = DefaultLocale)
    {
        var a = AffixDefinition.Parse(ReadSno(SnoGroup.Affix, id));
        TryReadSiblingString(SnoGroup.Affix, id, "Affix_", "Name", locale, out var n);
        TryReadSiblingString(SnoGroup.Affix, id, "Affix_", "Desc", locale, out var d);
        a.SetName(n);
        a.SetDescription(d);
        // CL-92 (LIB-3) / CL-94 — resolve each effect's attribute name: the
        // season-robust engine resolver for a positive id (handles
        // tag-conditional params), or the DataAttributes designer token for a
        // flag-namespaced (negative) id (CL-93 — a disjoint namespace).
        a.ResolveEffectNames((attrId, param) =>
            attrId < 0
                ? (TryGetDataAttributeName(attrId, out var dataName) ? dataName : null)
                : GetAttributeName(attrId, param, locale));
        return a;
    }

    /// <summary>
    /// LIB-4 (CL-103) — resolve a unique/legendary item's fixed <b>aspect
    /// affix</b>: the <see cref="AffixDefinition"/> that <i>is</i> the item's
    /// power (its <see cref="AffixDefinition.Effects"/> /
    /// <see cref="AffixEffect.InlineFormula"/> / localized <c>Name</c>).
    /// </summary>
    /// <remarks>
    /// A unique item (group <see cref="SnoGroup.Item"/> = 73, e.g.
    /// <c>1HAxe_Unique_Druid_100</c>) shares its SNO name <b>verbatim</b> with an
    /// affix definition (group <see cref="SnoGroup.Affix"/> = 104) of the same
    /// name — the generalized §6.7 sibling convention (as with the localized
    /// sibling-StringList tables, CL-20). The item record itself references only
    /// its model actor and base-item template, not the affix, so the affix is
    /// reached by that shared name. This wires the two already-decoded readers
    /// (<see cref="ReadItem"/> + <see cref="ReadAffix"/>): pass a unique item's
    /// SNO id and get its power's decoded affix. Name-verified across the unique
    /// roster; returns <see langword="false"/> when the item has no same-name
    /// affix (a non-unique item, or a seasonal <c>S<i>NN</i>_</c>-prefixed
    /// variant whose affix name differs).
    /// </remarks>
    /// <param name="itemSnoId">The unique item's SNO id (group 73).</param>
    /// <param name="affix">On success, the decoded sibling affix; otherwise
    /// <see langword="null"/>.</param>
    /// <param name="locale">Locale for the affix's localized text (default
    /// <see cref="DefaultLocale"/>).</param>
    /// <returns><see langword="true"/> iff a same-name affix was found and
    /// decoded.</returns>
    public bool TryReadUniqueAffix(
        int itemSnoId, out AffixDefinition? affix, string locale = DefaultLocale)
    {
        affix = null;
        if (!CoreToc.TryGetName(SnoGroup.Item, itemSnoId, out var name))
            return false;
        if (!CoreToc.TryGetId(SnoGroup.Affix, name, out var affixSnoId))
            return false;
        affix = ReadAffix(affixSnoId, locale);
        return true;
    }

    /// <summary>Resolve an affix's localized <b>display name</b> without a
    /// full <see cref="ReadAffix(int,string)"/> decode — the affix analogue
    /// of <see cref="TryReadParagonBoardName"/>. The name lives in the
    /// affix's sibling StringList table (group
    /// <see cref="SnoGroup.StringList"/> = 42), CoreTOC name
    /// <c>"Affix_" + affixSnoName</c>, under label <c>Name</c>
    /// (§11.3 / CL-87). Strictly name-keyed via <see cref="CoreToc"/> — the
    /// SNO ids are unrelated. Raw decoded value only, no fallback: returns
    /// the authored fragment verbatim (e.g. <c>"of Limitless Rage"</c>) with
    /// any <c>"Aspect …"</c> composition left to the consumer. Only ~1 in 4
    /// group-104 affixes carry a <c>Name</c>; the remainder are unnamed
    /// system/internal affixes → honest <see langword="false"/>.</summary>
    /// <param name="affixSnoId">The <c>Affix</c> SNO id (group 104).</param>
    /// <param name="name">The localized affix name, or <see cref="string.Empty"/>.</param>
    /// <param name="locale">Locale (default <see cref="DefaultLocale"/>).</param>
    /// <returns><see langword="true"/> iff a localized name was decoded.</returns>
    public bool TryReadAffixName(
        int affixSnoId, out string name, string locale = DefaultLocale) =>
        TryReadSiblingString(
            SnoGroup.Affix, affixSnoId, "Affix_", "Name", locale, out name);

    /// <summary>Read + decode an <see cref="ItemDefinition"/> by SNO id
    /// (group <see cref="SnoGroup.Item"/> = 73): identity + the localized
    /// <c>Name</c>/<c>Flavor</c>/<c>TransmogName</c> from the sibling
    /// <c>Item_&lt;snoName&gt;</c> StringList table (§11.4 / CL-22). Each
    /// field is empty when absent.</summary>
    /// <param name="id">The Item SNO id.</param>
    /// <param name="locale">Locale (default <see cref="DefaultLocale"/>).</param>
    public ItemDefinition ReadItem(int id, string locale = DefaultLocale)
    {
        var it = ItemDefinition.Parse(ReadSno(SnoGroup.Item, id));
        it.SetSnoName(CoreToc.GetName(SnoGroup.Item, id) ?? string.Empty);
        TryReadSiblingString(SnoGroup.Item, id, "Item_", "Name", locale, out var nm);
        TryReadSiblingString(SnoGroup.Item, id, "Item_", "Flavor", locale, out var fl);
        TryReadSiblingString(SnoGroup.Item, id, "Item_", "TransmogName", locale, out var tm);
        it.SetStrings(nm, fl, tm);
        return it;
    }

    /// <summary>Read + decode an <see cref="ItemType"/> (item base type) by
    /// SNO id (group <see cref="SnoGroup.ItemType"/> = 98) — the weapon /
    /// armor / jewelry / charm classification, resolved structurally from the
    /// record (§13 / LIB-1). Pass an item's
    /// <see cref="ItemDefinition.ItemTypeSnoId"/> to classify that item.</summary>
    /// <param name="id">The item-type SNO id.</param>
    public ItemType ReadItemType(int id) =>
        ItemType.Parse(id, CoreToc.GetName(SnoGroup.ItemType, id) ?? string.Empty,
            ReadSno(SnoGroup.ItemType, id));

    /// <summary>Enumerate every item base type (group
    /// <see cref="SnoGroup.ItemType"/> = 98), each decoded + classified — the
    /// dictionary a gear/item API filters (e.g. <c>.Where(t =&gt; t.Class ==
    /// ItemClass.Weapon)</c>). Ordered as the CoreTOC lists the group;
    /// unreadable records are skipped.</summary>
    public IEnumerable<ItemType> EnumerateItemTypes()
    {
        foreach (var e in CoreToc.EntriesInGroup(SnoGroup.ItemType))
        {
            ItemType t;
            try { t = ItemType.Parse(e.Id, e.Name, ReadSno(SnoGroup.ItemType, e.Id)); }
            catch { continue; }
            yield return t;
        }
    }

    private IReadOnlyDictionary<int, string>? _itemTypeNames;

    /// <summary>
    /// The engine <c>eItemType</c> <b>ordinal → base-type name</b> map (#51,
    /// CL-108) — the key that names an affix pool. An affix's
    /// <see cref="AffixDefinition.AllowedItemTypes"/> (and the inverse
    /// <see cref="RollableAffixes(int)"/>) speak these ordinals; this resolves
    /// each to a readable base-type name (<c>16 → "Helm"</c>, <c>71 → "Charm"</c>,
    /// <c>1 → "Axe"</c>). Built from the g98 <see cref="ItemType"/> records
    /// (<see cref="ItemType.EItemType"/>). Where several base types share one
    /// ordinal (1H/2H/class variants — <c>Axe</c> and <c>Axe2H</c> are both
    /// <c>1</c>) the representative is the shortest equippable name (the base).
    /// Cached after the first call.
    /// </summary>
    /// <remarks>A few ordinals seen in affix pools (e.g. <c>9</c>, <c>23</c>) have
    /// no g98 record and are therefore absent from the map — they are
    /// engine-aggregate/legacy values not nameable from the data.</remarks>
    public IReadOnlyDictionary<int, string> ReadItemTypeNames()
    {
        if (_itemTypeNames is not null) return _itemTypeNames;
        // Per ordinal, keep the best representative: an equippable base type beats
        // a non-equippable one; among equal equippability the shorter name wins
        // (so "Axe" beats "Axe2H", "Staff" beats "StaffDruid"/"StaffSorcerer").
        var best = new Dictionary<int, (string Name, bool Equip)>();
        foreach (var t in EnumerateItemTypes())
        {
            if (t.EItemType < 0) continue;
            if (!best.TryGetValue(t.EItemType, out var cur)
                || (t.IsEquippable && !cur.Equip)
                || (t.IsEquippable == cur.Equip && t.Name.Length < cur.Name.Length))
                best[t.EItemType] = (t.Name, t.IsEquippable);
        }
        var map = new Dictionary<int, string>(best.Count);
        foreach (var kv in best) map[kv.Key] = kv.Value.Name;
        return _itemTypeNames = map;
    }

    /// <summary>Resolve an engine <c>eItemType</c> ordinal (as it appears in
    /// <see cref="AffixDefinition.AllowedItemTypes"/> / <see cref="RollableAffixes(int)"/>)
    /// to a representative base-type name (#51, CL-108), or <see langword="null"/>
    /// when the ordinal has no g98 <see cref="ItemType"/> record (an
    /// engine-aggregate/legacy value — never a wrong name). See
    /// <see cref="ReadItemTypeNames"/>.</summary>
    /// <param name="eItemType">The <c>eItemType</c> ordinal.</param>
    public string? GetItemTypeName(int eItemType) =>
        ReadItemTypeNames().TryGetValue(eItemType, out var n) ? n : null;

    /// <summary>Enumerate every item (group <see cref="SnoGroup.Item"/> = 73)
    /// whose base type falls in <paramref name="category"/> — e.g.
    /// <c>EnumerateItems(ItemClass.Weapon)</c> for every weapon in the game,
    /// or <c>ItemClass.Charm</c> for every charm (§13 / LIB-1). Yields identity
    /// only (<see cref="ItemDefinition.SnoId"/> + <see cref="ItemDefinition.ItemTypeSnoId"/>);
    /// call <see cref="ReadItem(int,string)"/> for localized text on a chosen
    /// item. This is a full group scan (base-type classifications are memoized
    /// across the enumeration).</summary>
    /// <param name="category">The equipment category to filter to.</param>
    public IEnumerable<ItemDefinition> EnumerateItems(ItemClass category)
    {
        var classOfType = new Dictionary<int, ItemClass>();
        foreach (var e in CoreToc.EntriesInGroup(SnoGroup.Item))
        {
            ItemDefinition item;
            try { item = ItemDefinition.Parse(ReadSno(SnoGroup.Item, e.Id)); }
            catch { continue; }
            item.SetSnoName(e.Name);   // the CoreTOC name — enables ^S\d+_ dedup (#56)
            if (item.ItemTypeSnoId == 0) continue;
            if (!classOfType.TryGetValue(item.ItemTypeSnoId, out var cls))
            {
                try { cls = ReadItemType(item.ItemTypeSnoId).Class; }
                catch { cls = ItemClass.Other; }
                classOfType[item.ItemTypeSnoId] = cls;
            }
            if (cls == category) yield return item;
        }
    }

    /// <summary>
    /// FR-C16 — read the engine's per-node render program from the main
    /// paragon scene (657304): the ordered, z-sorted list of node
    /// state-widget layers (<see cref="ParagonNodeRecipe"/>). The
    /// consumer interprets it mechanically — supplying runtime predicates
    /// keyed by each layer's verbatim widget name — instead of inventing
    /// composition logic. See <see cref="ParagonNodeRecipe"/> for the
    /// name-keyed-predicate rationale.
    /// </summary>
    public ParagonNodeRecipe ReadParagonNodeRecipe() =>
        ParagonRenderProjection.NodeRecipe(
            ReadUiScene(657304), IsParagonTextureHandle);

    /// <summary>
    /// FR-C17 — read the engine's paragon-board grid-layout metric
    /// (<see cref="ParagonBoardGrid"/>): the design-canvas extent + node
    /// cell extent + cell pitch, in authored reference units. The
    /// consumer positions a board's grid cells with this metric (scaled
    /// to its render resolution) instead of an empirical pixel pitch.
    /// The per-board logical grid (dimensions + cell→node) stays
    /// <see cref="ReadParagonBoard"/>.
    /// </summary>
    public ParagonBoardGrid ReadParagonBoardGrid() =>
        ParagonRenderProjection.BoardGrid(ReadUiScene(657304));

    /// <summary>
    /// FR-C19 — read the mouse-over / cursor <see cref="SelectionHighlight"/>:
    /// the authored selection-highlight <see cref="TiledStyleDefinition">TiledStyle</see>
    /// recipes, as a typed shortcut over <see cref="Catalog"/>
    /// (<c>Find(AssetKind.SelectionHighlight)</c>). The consumer draws the
    /// matching style <b>topmost</b> over a selected node and applies it via
    /// <see cref="ReadTiledStyle"/>. Empty (<see cref="SelectionHighlight.IsEmpty"/>)
    /// if the selection atlases are absent.
    /// </summary>
    public SelectionHighlight ReadSelectionHighlight()
    {
        var styles = new List<SelectionHighlightStyle>();
        foreach (var r in Catalog.OfKind(AssetKind.SelectionHighlight))
            if (Catalog.TryGet<TiledStyleDefinition>(r, out var ts))
                styles.Add(new SelectionHighlightStyle(
                    r.Sno, r.Name, SelectionHighlight.ShapeOf(r.Name),
                    ts.SourceImageHandle, AtlasOf(r)));
        styles.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        return new SelectionHighlight(styles);

        // The atlas the style composes is carried as an "atlas:<sno>" tag.
        static int AtlasOf(in AssetRef r)
        {
            foreach (var t in r.Tags)
                if (t.StartsWith("atlas:", StringComparison.Ordinal) &&
                    int.TryParse(t.AsSpan(6), out var sno)) return sno;
            return 0;
        }
    }

    /// <summary>
    /// FR-C19 #30 — read the paragon <b>node mouse-over selection highlight</b>
    /// as the engine authors it: the <c>ContextualHighlight_Square</c> recipe
    /// (the named 4-corner "square contextual highlight" TiledStyle) paired with
    /// its drawable corner art — the 4 corner frames of the
    /// <c>2DUITiled_SelectionHighlight</c> atlas (the
    /// <c>SelectionRectangleInset</c> window-pieces' verified corners). The
    /// consumer draws the 4 corners as a hollow square border sized to the node
    /// perimeter — each corner in its quadrant, no edges/centre (see
    /// <see cref="NodeSelectionHighlight"/>). Empty corners if the art is absent.
    /// </summary>
    public NodeSelectionHighlight ReadNodeSelectionHighlight()
    {
        CoreToc.TryGetId(SnoGroup.UiStyle, "ContextualHighlight_Square", out var recipeSno);

        uint tl = 0, tr = 0, br = 0, bl = 0;
        if (CoreToc.TryGetId(SnoGroup.UiStyle, "SelectionRectangleInset", out var artSno) &&
            TryReadTiledStyle(artSno, out var art) && art.WindowPieces.Count >= 4)
        {
            // WindowPieces[0..3] are the corners clockwise from top-left
            // (verified by decoding + viewing each piece).
            tl = art.WindowPieces[0];
            tr = art.WindowPieces[1];
            br = art.WindowPieces[2];
            bl = art.WindowPieces[3];
        }
        return new NodeSelectionHighlight(recipeSno, "ContextualHighlight_Square", tl, tr, br, bl);
    }

    /// <summary>
    /// Read and decode a <see cref="TiledStyleDefinition"/> by SNO id
    /// (group <see cref="SnoGroup.UiStyle"/> = 103). The record describes
    /// a UI tile-rendering composition (piece handles + scale +
    /// padding) the engine applies via a widget's <c>snoTiledStyle</c>
    /// field. See <see cref="TiledStyleDefinition"/> for the layout and
    /// FR-C14 R9 disclosure on partial-decode of the variant suffix.
    /// </summary>
    /// <param name="id">The TiledStyle SNO id.</param>
    /// <exception cref="System.FormatException">If the blob is malformed
    /// (wrong magic / too short). Small sentinel ids (1, 3, 20, …)
    /// observed as <c>snoTiledStyle</c> bindings throw here — the
    /// consumer should guard with a length / read check.</exception>
    public TiledStyleDefinition ReadTiledStyle(int id) =>
        TiledStyleDefinition.Parse(ReadSno(SnoGroup.UiStyle, id));

    /// <summary>Try-read the tile-style record at <paramref name="id"/>,
    /// returning <see langword="false"/> when the read or parse fails
    /// (typically because <paramref name="id"/> is a small sentinel
    /// rather than a real SNO reference — see the
    /// <c>snoTiledStyle = 1 | 3 | 20</c> bindings observed in scene
    /// 657304).</summary>
    public bool TryReadTiledStyle(int id, out TiledStyleDefinition style)
    {
        try { style = ReadTiledStyle(id); return true; }
        catch { style = null!; return false; }
    }

    /// <summary>
    /// Resolve a node icon handle (<see cref="ParagonNodeDefinition.HIconMask"/>
    /// or <see cref="ParagonNodeDefinition.HIcon"/>) to the atlas SNO and
    /// <see cref="TexFrame"/> that carry it — the first-party node↔icon
    /// link (<c>hIconMask == TexFrame.ImageHandle</c>). The handle→frame
    /// index is built once from <see cref="TextureMeta"/> on first use.
    /// </summary>
    public bool TryGetIconFrame(uint handle, out int atlasSno, out TexFrame frame)
    {
        var idx = IconFrameIndex;
        if (idx.TryGetValue(handle, out var hit))
        {
            atlasSno = hit.Sno;
            frame = hit.Frame;
            return true;
        }
        atlasSno = 0;
        frame = default;
        return false;
    }

    private Dictionary<uint, (int Sno, TexFrame Frame)>? _iconFrames;

    private Dictionary<uint, (int Sno, TexFrame Frame)> IconFrameIndex
    {
        get
        {
            if (_iconFrames is not null) return _iconFrames;
            lock (_gate)
            {
                if (_iconFrames is null)
                {
                    var map = new Dictionary<uint, (int, TexFrame)>();
                    foreach (var kv in TextureMeta.BySno)
                        foreach (var f in kv.Value.Frames)
                            // First atlas wins on a shared handle.
                            if (!map.ContainsKey(f.ImageHandle))
                                map[f.ImageHandle] = (kv.Key, f);
                    _iconFrames = map;
                }
            }
            return _iconFrames;
        }
    }

    /// <summary>The Diablo IV SNO-group → file-extension table (factual data,
    /// matching the current build). Unknown groups fall back to the numeric
    /// <c>.NNN</c> form the game uses.</summary>
    public static string ExtensionFor(SnoGroup group) =>
        Extensions.TryGetValue((int)group, out var e) ? e : $".{(int)group:D3}";

    private static readonly Dictionary<int, string> Extensions = new()
    {
        [1] = ".acr", [2] = ".npc", [3] = ".aib", [4] = ".ais", [5] = ".ams",
        [6] = ".ani", [7] = ".an2", [8] = ".ans", [9] = ".app", [10] = ".hro",
        [11] = ".clt", [12] = ".cnv", [13] = ".cnl", [14] = ".efg", [15] = ".enc",
        [16] = "", [17] = ".xpl", [18] = ".flg", [19] = ".fnt", [20] = ".gam",
        [21] = ".glo", [22] = ".lvl", [23] = ".lit", [24] = ".mrk", [25] = "",
        [26] = ".obs", [27] = ".prt", [28] = ".phy", [29] = ".pow", [30] = "",
        [31] = ".qst", [32] = ".rop", [33] = ".scn", [34] = "", [35] = ".scr",
        [36] = ".shm", [37] = ".shd", [38] = ".shk", [39] = ".skl", [40] = ".snd",
        [41] = "", [42] = ".stl", [43] = ".srf", [44] = ".tex", [45] = ".trl",
        [46] = ".ui", [47] = ".wth", [48] = ".wrl", [49] = ".rcp", [50] = "",
        [51] = ".cnd", [52] = ".trs", [53] = ".acc", [57] = ".mat", [59] = ".lor",
        [60] = ".rev", [62] = ".mus", [63] = ".tut", [67] = ".ant", [68] = ".vib",
        [71] = ".wsb", [72] = ".spk", [73] = ".itm", [74] = ".pcl", [76] = ".fog",
        [77] = ".bio", [78] = ".wal", [79] = ".sdt", [104] = ".aff", [105] = ".rep",
        [106] = ".pgn", [107] = ".maf", [108] = ".pbd", [109] = ".set",
        [110] = ".prd", [111] = ".gph", [112] = ".gaf",
    };

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsCasc) _casc.Dispose();
    }
}
