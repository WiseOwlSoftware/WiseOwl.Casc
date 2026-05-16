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
    private readonly object _gate = new();

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
    public string SnoPath(int id, SnoFolder folder = SnoFolder.Meta,
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
