using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WiseOwl.Casc.Configuration;
using WiseOwl.Casc.Encoding;
using WiseOwl.Casc.Indices;
using WiseOwl.Casc.Internal;

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

    private byte[] ReadEnvelope(in ArchiveLocation loc)
    {
        var dataFile = Path.Combine(
            _installPath, "Data", "data", $"data.{loc.ArchiveIndex:D3}");
        if (!File.Exists(dataFile))
            throw new CascContentNotFoundException($"Missing archive '{dataFile}'.");

        using var fs = new FileStream(
            dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: 1, FileOptions.RandomAccess);
        fs.Seek(loc.Offset, SeekOrigin.Begin);

        var buffer = new byte[loc.Size];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = fs.Read(buffer, read, buffer.Length - read);
            if (n <= 0)
                throw new CascFormatException(
                    $"Short read in '{dataFile}' at {loc.Offset} " +
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
    public void Dispose() { /* archives are opened per-read; nothing held */ }
}
