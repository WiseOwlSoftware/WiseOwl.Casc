using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WiseOwl.Casc.Configuration;
using WiseOwl.Casc.Encoding;
using System.Collections.Generic;
using WiseOwl.Casc.Indices;
using WiseOwl.Casc.Internal;
using WiseOwl.Casc.Tvfs;

namespace WiseOwl.Casc;

/// <summary>
/// An opened, local Blizzard CASC storage. This is the game-agnostic
/// transport entry point: it resolves content by <see cref="EncodingKey"/>
/// or <see cref="ContentKey"/>, reads the archive envelope, and BLTE-decodes
/// it. Game-specific name/SNO resolution lives in the game modules
/// (e.g. <c>WiseOwl.Casc.Diablo4</c>).
/// </summary>
/// <remarks>
/// Reads only a local installation (no CDN). The encoding table is large, so
/// it is parsed lazily on first content-key use; the local index is parsed
/// up front. Instances are thread-safe for reads.
/// </remarks>
public sealed class CascStorage : IDisposable
{
    private const int EnvelopeHeaderSize = 30; // 16 (reversed EKey) + 4 size + 10

    private readonly string _installPath;
    private readonly LocalIndex _index;
    private readonly object _gate = new();
    private EncodingTable? _encoding;          // lazily parsed
    private TvfsManifest? _tvfs;               // lazily parsed
    private readonly Dictionary<int, FileStream> _archives = [];  // cached handles

    private CascStorage(
        string installPath, BuildInfo build,
        BuildConfiguration config, LocalIndex index)
    {
        _installPath = installPath;
        Build = build;
        Config = config;
        _index = index;
    }

    /// <summary>The parsed <c>.build.info</c>.</summary>
    public BuildInfo Build { get; }

    /// <summary>The parsed build configuration.</summary>
    public BuildConfiguration Config { get; }

    /// <summary>The local archive index.</summary>
    public LocalIndex Index => _index;

    /// <summary>Open a local CASC installation.</summary>
    /// <param name="installPath">The game install root (the folder
    /// containing <c>.build.info</c> and <c>Data/</c>).</param>
    /// <param name="options">Optional open options.</param>
    public static CascStorage OpenLocal(string installPath, CascOpenOptions? options = null)
    {
        options ??= CascOpenOptions.Default;
        var build = BuildInfo.Load(installPath);
        var config = BuildConfiguration.Load(installPath, build);
        var index = LocalIndex.LoadLocal(installPath);
        return new CascStorage(installPath, build, config, index);
    }

    /// <summary>Open a local CASC installation asynchronously.</summary>
    public static Task<CascStorage> OpenLocalAsync(
        string installPath, CascOpenOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => OpenLocal(installPath, options), cancellationToken);

    /// <summary>True if a blob with this encoding key is physically present
    /// in the local archives.</summary>
    public bool Contains(in EncodingKey eKey) =>
        _index.TryGetLocation(eKey, out _);

    /// <summary>Resolve a content key to its encoding key via the encoding
    /// table (parsed on first use).</summary>
    public bool TryGetEncodingKey(in ContentKey cKey, out EncodingKey eKey)
    {
        EnsureEncoding();
        return _encoding!.TryGetEncodingKey(cKey, out eKey);
    }

    /// <summary>Read and BLTE-decode the blob for an encoding key.</summary>
    /// <exception cref="CascContentNotFoundException">Not in the local index.</exception>
    public byte[] Read(in EncodingKey eKey)
    {
        if (!_index.TryGetLocation(eKey, out var loc))
            throw new CascContentNotFoundException(
                $"Encoding key {eKey} is not in the local index.");

        var envelope = ReadEnvelope(loc);
        // Envelope: 16-byte reversed EKey, 4-byte size, 10 reserved, then BLTE.
        return Blte.Decode(envelope.AsSpan(EnvelopeHeaderSize));
    }

    /// <summary>Read and BLTE-decode the blob for a content key.</summary>
    public byte[] Read(in ContentKey cKey)
    {
        if (!TryGetEncodingKey(cKey, out var eKey))
            throw new CascContentNotFoundException(
                $"Content key {cKey} has no encoding-table entry.");
        return Read(eKey);
    }

    /// <summary>Open the decoded blob for an encoding key as a stream.</summary>
    public Stream OpenRead(in EncodingKey eKey) =>
        new MemoryStream(Read(eKey), writable: false);

    /// <summary>Open the decoded blob for a content key as a stream.</summary>
    public Stream OpenRead(in ContentKey cKey) =>
        new MemoryStream(Read(cKey), writable: false);

    /// <summary>Asynchronously read and decode the blob for an encoding key.</summary>
    public Task<byte[]> ReadAsync(
        EncodingKey eKey, CancellationToken cancellationToken = default) =>
        Task.Run(() => Read(eKey), cancellationToken);

    /// <summary>The storage's TVFS file system (parsed on first use). For
    /// Diablo IV this is the <c>vfs-root</c> tree; <see langword="null"/> if
    /// the storage has no TVFS.</summary>
    public TvfsManifest? Tvfs
    {
        get
        {
            if (_tvfs is not null) return _tvfs;
            if (Config.VfsRoot is not { } vr) return null;
            lock (_gate)
            {
                if (_tvfs is null)
                {
                    // Nested vfs-N manifests are matched by their 9-byte
                    // index prefix (TVFS/CFT keys are 9 bytes).
                    var subKeys = new HashSet<ulong>();
                    foreach (var k in Config.VfsManifestEncodingKeys())
                        subKeys.Add(k.IndexPrefix);

                    _tvfs = TvfsManifest.Parse(
                        Read(vr.Encoding),
                        ek => subKeys.Contains(ek.IndexPrefix),
                        ek => Read(ek));
                }
            }
            return _tvfs;
        }
    }

    /// <summary>Resolve a TVFS path to its encoding key.</summary>
    public bool TryResolvePath(string path, out EncodingKey eKey)
    {
        eKey = default;
        return Tvfs is { } t && t.TryResolve(path, out eKey);
    }

    /// <summary>Read and BLTE-decode a file by its TVFS path
    /// (e.g. <c>Base\CoreTOC.dat</c>).</summary>
    /// <exception cref="CascContentNotFoundException">No such path / no TVFS.</exception>
    public byte[] ReadPath(string path)
    {
        if (!TryResolvePath(path, out var eKey))
            throw new CascContentNotFoundException(
                $"Path '{path}' is not in the TVFS file system.");
        return Read(eKey);
    }

    /// <summary>Open a TVFS file by path as a decoded stream.</summary>
    public Stream OpenPath(string path) =>
        new MemoryStream(ReadPath(path), writable: false);

    private byte[] ReadEnvelope(in ArchiveLocation loc)
    {
        // Keep one handle per data.NNN open and seek per read: a dataset run
        // does hundreds of by-id reads and re-opening each time dominated.
        var fs = GetArchive(loc.ArchiveIndex);
        lock (fs)
        {
            fs.Seek(loc.Offset, SeekOrigin.Begin);
            return ReadExactly(fs, loc.Size, loc.ArchiveIndex, loc.Offset);
        }
    }

    private FileStream GetArchive(int archiveIndex)
    {
        if (_archives.TryGetValue(archiveIndex, out var existing)) return existing;
        lock (_gate)
        {
            if (_archives.TryGetValue(archiveIndex, out existing)) return existing;
            var dataFile = Path.Combine(
                _installPath, "Data", "data", $"data.{archiveIndex:D3}");
            if (!File.Exists(dataFile))
                throw new CascContentNotFoundException($"Missing archive '{dataFile}'.");
            var fs = new FileStream(
                dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                bufferSize: 1, FileOptions.RandomAccess);
            _archives[archiveIndex] = fs;
            return fs;
        }
    }

    private static byte[] ReadExactly(FileStream fs, int size, int archive, long offset)
    {
        var buffer = new byte[size];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = fs.Read(buffer, read, buffer.Length - read);
            if (n <= 0)
                throw new CascFormatException(
                    $"Short read in data.{archive:D3} at {offset} " +
                    $"({read}/{buffer.Length}).");
            read += n;
        }
        return buffer;
    }

    private void EnsureEncoding()
    {
        if (_encoding is not null) return;
        lock (_gate)
        {
            if (_encoding is not null) return;
            var raw = Read(Config.EncodingEncodingKey);
            _encoding = EncodingTable.Parse(raw);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var fs in _archives.Values) fs.Dispose();
            _archives.Clear();
        }
    }
}
