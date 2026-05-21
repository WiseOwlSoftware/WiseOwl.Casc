using System;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A reader over a decoded Diablo IV SNO blob (the bytes you get back from
/// <see cref="Diablo4Storage.OpenSno"/>). Encapsulates the verified D4
/// conventions: a 16-byte <c>SNOFileHeader</c>, then the <b>payload base</b>
/// at file offset <c>0x10</c> (where the SNO id sits), with every field
/// offset and <c>DT_VARIABLEARRAY</c> <c>dataOffset</c> measured from that
/// payload base.
/// </summary>
/// <remarks>
/// Construct over a standalone blob with the default base, or over a
/// descriptor embedded in a combined-meta bundle by passing an explicit
/// <paramref name="payloadBase"/> (there is no 16-byte header in that case —
/// see <see cref="CombinedTextureMeta"/>).
/// </remarks>
/// <param name="data">The full SNO blob (or the enclosing bundle buffer).</param>
/// <param name="payloadBase">Absolute offset of the payload root. Defaults
/// to <see cref="DefaultPayloadBase"/> (0x10) for standalone blobs.</param>
public readonly ref struct SnoRecord(ReadOnlySpan<byte> data, int payloadBase)
{
    /// <summary>Standalone-blob payload base: immediately after the
    /// 16-byte <c>SNOFileHeader</c>.</summary>
    public const int DefaultPayloadBase = 0x10;

    /// <summary>Expected value of <see cref="Signature"/>.</summary>
    public const uint ExpectedSignature = 0xDEADBEEF;

    private readonly ReadOnlySpan<byte> _data = data;

    /// <summary>Wrap a standalone SNO blob (payload base <c>0x10</c>).</summary>
    public SnoRecord(ReadOnlySpan<byte> data) : this(data, DefaultPayloadBase) { }

    /// <summary>Absolute offset of the payload root for this record.</summary>
    public int PayloadBase { get; } = payloadBase;

    /// <summary>Header signature (expected <see cref="ExpectedSignature"/>).
    /// Only meaningful for standalone blobs (base <c>0x10</c>).</summary>
    public uint Signature => Bytes.U32LE(_data, 0x00);

    /// <summary>Header format hash; often <c>0</c> — resolve via the CoreTOC
    /// group hash when zero.</summary>
    public uint FormatHash => Bytes.U32LE(_data, 0x04);

    /// <summary>The SNO id stored at the payload base.</summary>
    public int SnoId => Bytes.I32LE(_data, PayloadBase);

    /// <summary>Underlying buffer length (for bounds checks).</summary>
    public int Length => _data.Length;

    /// <summary><see cref="uint"/> at a payload-relative offset.</summary>
    public uint U32(int payloadOffset) => Bytes.U32LE(_data, PayloadBase + payloadOffset);

    /// <summary><see cref="int"/> at a payload-relative offset.</summary>
    public int I32(int payloadOffset) => Bytes.I32LE(_data, PayloadBase + payloadOffset);

    /// <summary><see cref="ushort"/> at a payload-relative offset.</summary>
    public ushort U16(int payloadOffset) => Bytes.U16LE(_data, PayloadBase + payloadOffset);

    /// <summary>Single byte at a payload-relative offset.</summary>
    public byte U8(int payloadOffset) => _data[PayloadBase + payloadOffset];

    /// <summary>Read a NUL-terminated ASCII string at a payload-relative
    /// offset, stopping at the first NUL or <paramref name="maxLength"/>
    /// bytes. Used for inline record strings (e.g. a node's
    /// <c>szAttributeFormula</c> text).</summary>
    public string Ascii(int payloadOffset, int maxLength) =>
        Bytes.AsciiZ(_data, PayloadBase + payloadOffset, maxLength);

    /// <summary>Read a NUL-terminated ASCII string at an <b>absolute</b>
    /// buffer offset (a <c>DT_CSTRING</c>/<c>DT_STRING_FORMULA</c> indirection
    /// stores an absolute offset).</summary>
    public string AsciiAbsolute(int absoluteOffset, int maxLength) =>
        Bytes.AsciiZ(_data, absoluteOffset, maxLength);

    /// <summary>IEEE-754 <see cref="float"/> at a payload-relative offset.</summary>
    public float F32(int payloadOffset)
    {
        var bits = U32(payloadOffset);
        return BitConverter.UInt32BitsToSingle(bits);
    }

    /// <summary>
    /// Read a standard <c>DT_VARIABLEARRAY</c> descriptor — the
    /// <c>{ int64 padding; int32 dataOffset; int32 dataSize }</c> shape used
    /// in standalone SNO payloads — and return the absolute element span.
    /// <c>dataOffset</c> is relative to the payload base.
    /// </summary>
    /// <param name="payloadOffset">Descriptor offset from the payload base.</param>
    /// <returns>The element bytes (length is the descriptor's
    /// <c>dataSize</c>).</returns>
    public ReadOnlySpan<byte> VariableArray(int payloadOffset)
    {
        var dataOffset = (int)U32(payloadOffset + 8);
        var dataSize = (int)U32(payloadOffset + 12);
        var start = PayloadBase + dataOffset;
        if (start < 0 || dataSize < 0 || start + dataSize > _data.Length)
            return default;
        return _data.Slice(start, dataSize);
    }
}
