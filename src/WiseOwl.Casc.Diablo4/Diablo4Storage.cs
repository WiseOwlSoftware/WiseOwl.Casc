using System;
using System.Collections.Generic;
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
#if NETSTANDARD2_0
        if (token.Length == 0 || idxText.Length == 0 ||
            !int.TryParse(idxText, out boardIndex))
#else
        if (token.Length == 0 || !int.TryParse(idxText, out boardIndex))
#endif
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
    public ParagonGlyphDefinition ReadParagonGlyph(int id)
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
        return glyph;
    }

    /// <summary>Read + decode a <see cref="ParagonGlyphAffixDefinition"/> by
    /// SNO id (group 112).</summary>
    public ParagonGlyphAffixDefinition ReadParagonGlyphAffix(int id) =>
        ParagonGlyphAffixDefinition.Parse(ReadSno(SnoGroup.ParagonGlyphAffix, id));

    /// <summary>Read + decode the GameBalance <see cref="AttributeFormulaTable"/>
    /// (default SNO <c>201912</c>, the paragon formula table). Returns
    /// formula <i>text</i> + name/GBID indices only — evaluation and the
    /// calibrated intrinsics stay with the consumer.</summary>
    public AttributeFormulaTable ReadAttributeFormulas(int id = 201912) =>
        AttributeFormulaTable.Parse(ReadSno(SnoGroup.GameBalance, id));

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
        return p;
    }

    /// <summary>Read + decode an <see cref="AffixDefinition"/> by SNO id
    /// (group <see cref="SnoGroup.Affix"/> = 104): identity + the localized
    /// <c>Desc</c> from the sibling <c>Affix_&lt;snoName&gt;</c> StringList
    /// table (§11.3 / CL-22).</summary>
    /// <param name="id">The Affix SNO id.</param>
    /// <param name="locale">Locale (default <see cref="DefaultLocale"/>).</param>
    public AffixDefinition ReadAffix(int id, string locale = DefaultLocale)
    {
        var a = AffixDefinition.Parse(ReadSno(SnoGroup.Affix, id));
        TryReadSiblingString(SnoGroup.Affix, id, "Affix_", "Desc", locale, out var d);
        a.SetDescription(d);
        return a;
    }

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
        TryReadSiblingString(SnoGroup.Item, id, "Item_", "Name", locale, out var nm);
        TryReadSiblingString(SnoGroup.Item, id, "Item_", "Flavor", locale, out var fl);
        TryReadSiblingString(SnoGroup.Item, id, "Item_", "TransmogName", locale, out var tm);
        it.SetStrings(nm, fl, tm);
        return it;
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
        ParagonRenderProjection.NodeRecipe(ReadUiScene(657304));

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
