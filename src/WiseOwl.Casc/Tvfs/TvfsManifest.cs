using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Tvfs;

/// <summary>
/// A parsed <b>TVFS</b> (TACT Virtual File System) manifest: the path→content
/// tree newer Blizzard titles (including Diablo IV) use instead of a classic
/// root. Walking it yields, for every file, the
/// lookup3 path-hash mapped to the encoding key the storage is addressed by.
/// </summary>
/// <remarks>
/// <para>Clean-room implementation of the documented TVFS layout:
/// a <c>TVFS</c>-magic directory header sizing three tables (path, VFS,
/// CFT), then a recursively-walked path table. Each path node carries
/// pre/post separator flags, a name, and either a folder span (recurse) or
/// a node value indexing the VFS table; a VFS span points into the CFT
/// table where the file's encoding key lives. Some entries are themselves
/// nested TVFS directories (the build config's <c>vfs-1…vfs-N</c>); those
/// are resolved through a caller-supplied sub-manifest reader.</para>
/// <para>This implementation indexes the first span's encoding key per
/// file — sufficient to open the single-span data/meta/payload files game
/// modules need.</para>
/// </remarks>
public sealed class TvfsManifest
{
    private const uint Magic = 0x53465654;          // 'TVFS'
    private const int FlagSepPre = 0x0001;
    private const int FlagSepPost = 0x0002;
    private const int FlagNodeValue = 0x0004;
    private const uint FolderNode = 0x80000000;
    private const int FolderSizeMask = 0x7FFFFFFF;

    private readonly Dictionary<ulong, EncodingKey> _byPathHash;

    private TvfsManifest(Dictionary<ulong, EncodingKey> map, TvfsStats stats)
    {
        _byPathHash = map;
        Stats = stats;
    }

    /// <summary>Number of indexed files.</summary>
    public int Count => _byPathHash.Count;

    /// <summary>Walk diagnostics (sub-manifest descent counts, a capped
    /// sample of reconstructed path strings) — for understanding the tree,
    /// not part of normal use.</summary>
    public TvfsStats Stats { get; }

    /// <summary>All path-hash → encoding-key entries.</summary>
    public IReadOnlyDictionary<ulong, EncodingKey> Entries => _byPathHash;

    /// <summary>Resolve a CASC path (e.g. <c>Base\CoreTOC.dat</c>) to its
    /// encoding key.</summary>
    public bool TryResolve(string path, out EncodingKey eKey) =>
        _byPathHash.TryGetValue(CascPathHash.OfPath(path), out eKey);

    /// <summary>Resolve a pre-computed path hash to its encoding key.</summary>
    public bool TryResolve(ulong pathHash, out EncodingKey eKey) =>
        _byPathHash.TryGetValue(pathHash, out eKey);

    /// <summary>Parse a TVFS manifest tree.</summary>
    /// <param name="rootData">The BLTE-decoded <c>vfs-root</c> bytes.</param>
    /// <param name="isSubManifestKey">Predicate: is this 9-byte encoding key
    /// one of the build config's nested <c>vfs-N</c> manifests?</param>
    /// <param name="readSubManifest">Reads a nested manifest's decoded bytes
    /// given its encoding key.</param>
    /// <param name="capturePathIf">Optional diagnostic: every reconstructed
    /// raw path the predicate accepts is collected in
    /// <see cref="TvfsStats.CapturedPaths"/> (for format investigation).</param>
    public static TvfsManifest Parse(
        byte[] rootData,
        Func<EncodingKey, bool> isSubManifestKey,
        Func<EncodingKey, byte[]> readSubManifest,
        Func<string, bool>? capturePathIf = null)
    {
        var map = new Dictionary<ulong, EncodingKey>();
        var stats = new TvfsStats { CapturePathIf = capturePathIf };
        var ctx = new Context(isSubManifestKey, readSubManifest, map, stats);
        WalkDirectory(rootData, ctx, new PathBuilder());
        return new TvfsManifest(map, stats);
    }

    /// <summary>TVFS walk diagnostics.</summary>
    public sealed class TvfsStats
    {
        /// <summary>File nodes visited.</summary>
        public int FileNodes { get; internal set; }
        /// <summary>Spans whose key matched a nested manifest.</summary>
        public int SubManifestSpans { get; internal set; }
        /// <summary>Nested manifests actually descended.</summary>
        public int SubManifestsDescended { get; internal set; }
        /// <summary>Deepest directory recursion reached.</summary>
        public int MaxDepth { get; internal set; }
        /// <summary>A capped sample of reconstructed path strings (raw,
        /// pre-hash) — to see exactly what the tree names look like.</summary>
        public List<string> SamplePaths { get; } = [];

        /// <summary>Optional diagnostic filter: when set, every reconstructed
        /// raw path the predicate accepts is collected in
        /// <see cref="CapturedPaths"/> (uncapped). For format investigation.</summary>
        public Func<string, bool>? CapturePathIf { get; internal set; }

        /// <summary>Raw paths matching <see cref="CapturePathIf"/>.</summary>
        public List<string> CapturedPaths { get; } = [];
    }

    private sealed class Context(
        Func<EncodingKey, bool> isSub,
        Func<EncodingKey, byte[]> readSub,
        Dictionary<ulong, EncodingKey> map,
        TvfsStats stats)
    {
        public Func<EncodingKey, bool> IsSub { get; } = isSub;
        public Func<EncodingKey, byte[]> ReadSub { get; } = readSub;
        public Dictionary<ulong, EncodingKey> Map { get; } = map;
        public TvfsStats Stats { get; } = stats;
        public int Depth { get; set; }
    }

    /// <summary>A growable ASCII path accumulator with save/restore (the
    /// recursive walk rewinds to a saved length when leaving a node).</summary>
    private sealed class PathBuilder
    {
        private byte[] _buf = new byte[512];
        public int Length { get; private set; }

        public void Append(byte b)
        {
            if (Length == _buf.Length) Array.Resize(ref _buf, _buf.Length * 2);
            _buf[Length++] = b;
        }

        public void Append(ReadOnlySpan<byte> s)
        {
            while (Length + s.Length > _buf.Length)
                Array.Resize(ref _buf, _buf.Length * 2);
            s.CopyTo(_buf.AsSpan(Length));
            Length += s.Length;
        }

        public void Truncate(int length) => Length = length;

        /// <summary>The raw (un-normalized) assembled path, for diagnostics.</summary>
        public string Raw() =>
            System.Text.Encoding.ASCII.GetString(_buf.AsSpan(0, Length));

        /// <summary>Hash the accumulated path with the same normalization the
        /// storage uses: <c>/</c> → <c>\</c> and ASCII upper-cased (matching
        /// <see cref="CascPathHash.OfPath"/>, so resolves line up).</summary>
        public ulong Hash()
        {
            Span<byte> norm = Length <= 512 ? stackalloc byte[Length] : new byte[Length];
            for (var i = 0; i < Length; i++)
            {
                var b = _buf[i];
                norm[i] = b switch
                {
                    (byte)'/' => (byte)'\\',
                    >= (byte)'a' and <= (byte)'z' => (byte)(b - 32),
                    _ => b,
                };
            }
            return CascPathHash.Of(norm);
        }
    }

    private readonly struct Header
    {
        public Header(byte[] d)
        {
            if (Bytes.U32LE(d, 0) != Magic)
                throw new CascFormatException("Not a TVFS manifest (bad magic).");
            // d[4]=version(1) d[5]=headerSize d[6]=eKeySize(9) d[7]=patchKeySize
            EKeySize = d[6];
            PathOff = Bytes.I32BE(d, 12);
            PathSize = Bytes.I32BE(d, 16);
            VfsOff = Bytes.I32BE(d, 20);
            VfsSize = Bytes.I32BE(d, 24);
            CftOff = Bytes.I32BE(d, 28);
            CftSize = Bytes.I32BE(d, 32);
            // d[36..37]=maxDepth(BE); EST table follows (unused).
            CftOffsSize = OffsetFieldSize(CftSize);
        }

        public int EKeySize { get; }
        public int PathOff { get; }
        public int PathSize { get; }
        public int VfsOff { get; }
        public int VfsSize { get; }
        public int CftOff { get; }
        public int CftSize { get; }
        public int CftOffsSize { get; }

        private static int OffsetFieldSize(int tableSize) => tableSize switch
        {
            > 0xFFFFFF => 4,
            > 0xFFFF => 3,
            > 0xFF => 2,
            _ => 1,
        };
    }

    private static void WalkDirectory(byte[] data, Context ctx, PathBuilder path)
    {
        var h = new Header(data);
        var pathTable = data.AsSpan(h.PathOff, h.PathSize);

        // A leading folder-node prelude (0xFF + BE folder size) is skipped.
        if (pathTable.Length > 1 + 4 && pathTable[0] == 0xFF)
        {
            var nodeValue = (uint)Bytes.I32BE(pathTable, 1);
            if ((nodeValue & FolderNode) == 0)
                throw new CascFormatException("TVFS root is not a folder node.");
            pathTable = pathTable.Slice(1 + 4);
        }

        WalkPathTable(data, h, pathTable, ctx, path);
    }

    private static void WalkPathTable(
        byte[] data, in Header h, ReadOnlySpan<byte> pathTable,
        Context ctx, PathBuilder path)
    {
        var savedLen = path.Length;

        while (pathTable.Length > 0)
        {
            pathTable = CapturePathEntry(pathTable, out var name, out var flags, out var nodeValue);

            if ((flags & FlagSepPre) != 0) path.Append((byte)'/');
            path.Append(name);
            if ((flags & FlagSepPost) != 0) path.Append((byte)'/');

            if ((flags & FlagNodeValue) != 0)
            {
                if (((uint)nodeValue & FolderNode) != 0)
                {
                    // Folder: recurse into the next dirLen bytes.
                    var dirLen = (nodeValue & FolderSizeMask) - 4;
                    WalkPathTable(data, h, pathTable.Slice(0, dirLen), ctx, path);
                    pathTable = pathTable.Slice(dirLen);
                }
                else
                {
                    EmitFile(data, h, nodeValue, ctx, path);
                }
                path.Truncate(savedLen);
            }
        }
    }

    private static void EmitFile(
        byte[] data, in Header h, int vfsOffset, Context ctx, PathBuilder path)
    {
        var vfs = data.AsSpan(h.VfsOff, h.VfsSize).Slice(vfsOffset);
        if (vfs.Length == 0) return;
        var spanCount = vfs[0];
        vfs = vfs.Slice(1);
        if (spanCount < 1 || spanCount > 224) return;

        // First span's CFT entry holds the encoding key.
        var itemSize = 4 + 4 + h.CftOffsSize;     // contentOffset, length, cftOff
        if (itemSize > vfs.Length) return;
        var cftOffset = ReadVar(vfs.Slice(8), h.CftOffsSize);

        var cft = data.AsSpan(h.CftOff, h.CftSize);
        if (cftOffset + h.EKeySize > cft.Length) return;
        var eKey = EncodingKey.FromBytes(cft.Slice(cftOffset, h.EKeySize));

        ctx.Stats.FileNodes++;

        // A span pointing at a nested TVFS directory: recurse into it.
        if (ctx.IsSub(eKey))
        {
            ctx.Stats.SubManifestSpans++;
            ctx.Stats.SubManifestsDescended++;
            ctx.Depth++;
            if (ctx.Depth > ctx.Stats.MaxDepth) ctx.Stats.MaxDepth = ctx.Depth;
            path.Append((byte)'/');
            WalkDirectory(ctx.ReadSub(eKey), ctx, path);
            ctx.Depth--;
            return;
        }

        if (ctx.Stats.SamplePaths.Count < 60)
            ctx.Stats.SamplePaths.Add(path.Raw());

        if (ctx.Stats.CapturePathIf is { } pred)
        {
            var raw = path.Raw();
            if (pred(raw)) ctx.Stats.CapturedPaths.Add(raw);
        }

        var hash = path.Hash();
        ctx.Map.TryAdd(hash, eKey);
    }

    /// <summary>Decode one path-table entry: optional pre-separator, a name,
    /// optional post-separator, and either a 0xFF-tagged 32-bit node value or
    /// an implicit post-separator.</summary>
    private static ReadOnlySpan<byte> CapturePathEntry(
        ReadOnlySpan<byte> t, out ReadOnlySpan<byte> name, out int flags, out int nodeValue)
    {
        name = default;
        flags = 0;
        nodeValue = 0;

        if (t.Length > 0 && t[0] == 0) { flags |= FlagSepPre; t = t.Slice(1); }

        if (t.Length > 0 && t[0] != 0xFF)
        {
            int len = t[0];
            if (len > t.Length) return default;
            name = t.Slice(1, len);
            t = t.Slice(1 + len);
        }

        if (t.Length > 0 && t[0] == 0) { flags |= FlagSepPost; t = t.Slice(1); }

        if (t.Length > 0)
        {
            if (t[0] == 0xFF)
            {
                if (1 + 4 > t.Length) return default;
                nodeValue = Bytes.I32BE(t, 1);
                flags |= FlagNodeValue;
                t = t.Slice(1 + 4);
            }
            else
            {
                flags |= FlagSepPost;
            }
        }
        return t;
    }

    private static int ReadVar(ReadOnlySpan<byte> s, int n)
    {
        var v = 0;
        for (var i = 0; i < n; i++) v = (v << 8) | s[i];
        return v;
    }
}
