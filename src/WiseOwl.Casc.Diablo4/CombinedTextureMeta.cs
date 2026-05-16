using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// The Diablo IV combined-meta bundle (magic <c>0x44CF00F5</c>,
/// <c>Base\Texture-Base-Global.dat</c>): an indexed blob holding every
/// <see cref="TextureDefinition"/>. Texture metadata is <b>not</b>
/// path-addressable per-SNO; it is consolidated here, while the pixel
/// payloads remain addressable by SNO id (de-duplicated through the
/// shared-payload mapping).
/// </summary>
/// <remarks>
/// Layout (clean-room, cross-checked against the upstream record
/// <c>§8.12–§8.13</c>): <c>u32 magic; u32 count; count × { i32 snoId,
/// u32 defSize }</c>. Then, per index entry in order,
/// <c>descStart = alignUp8(prevEnd) + 8</c> (the <c>+8</c> is the
/// Texture-group convention); the per-entry <c>snoId</c> is
/// <c>i32 @descStart</c> and the <see cref="TextureDefinition"/> body
/// follows with payload base <c>descStart + 4</c>.
/// </remarks>
public sealed class CombinedTextureMeta
{
    /// <summary>The bundle's magic number.</summary>
    public const uint Magic = 0x44CF00F5;

    private readonly Dictionary<int, TextureDefinition> _bySno;

    private CombinedTextureMeta(Dictionary<int, TextureDefinition> bySno) => _bySno = bySno;

    /// <summary>Every parsed texture definition, keyed by SNO id.</summary>
    public IReadOnlyDictionary<int, TextureDefinition> BySno => _bySno;

    /// <summary>Look up a texture definition by SNO id.</summary>
    public TextureDefinition? Get(int snoId) =>
        _bySno.TryGetValue(snoId, out var td) ? td : null;

    /// <summary>Try to look up a texture definition by SNO id.</summary>
    public bool TryGet(int snoId, out TextureDefinition definition)
    {
        var ok = _bySno.TryGetValue(snoId, out var td);
        definition = td!;
        return ok;
    }

    /// <summary>Parse the combined-meta bundle bytes.</summary>
    /// <exception cref="CascFormatException">The magic number is wrong.</exception>
    public static CombinedTextureMeta Parse(ReadOnlySpan<byte> bundle)
    {
        var magic = Bytes.U32LE(bundle, 0);
        if (magic != Magic)
            throw new CascFormatException(
                $"combined-meta magic 0x{magic:X8} != 0x{Magic:X8}");

        var count = (int)Bytes.U32LE(bundle, 4);
        var index = new (int Sno, int Size)[count];
        for (var i = 0; i < count; i++)
        {
            index[i].Sno = Bytes.I32LE(bundle, 8 + i * 8);
            index[i].Size = (int)Bytes.U32LE(bundle, 12 + i * 8);
        }

        var map = new Dictionary<int, TextureDefinition>(count);
        var cursor = 8 + count * 8;            // end of index = first prevEnd
        foreach (var (sno, size) in index)
        {
            var aligned = (cursor + 7) & ~7;   // alignUp8
            var descStart = aligned + 8;       // Texture-group (+8) convention
            if (descStart + size > bundle.Length) break;

            var entrySno = Bytes.I32LE(bundle, descStart);
            if (entrySno == sno && size >= 88)
                map[sno] = TextureDefinition.ParseFromBundle(bundle, descStart);

            cursor = descStart + size;         // prevEnd for the next align
        }
        return new CombinedTextureMeta(map);
    }
}
