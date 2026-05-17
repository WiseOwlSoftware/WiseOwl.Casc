using System;
using System.Collections.Generic;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <b>UI-scene</b> SNO (CoreTOC group <c>46</c>,
/// format hash <c>0xE4825AB8</c>) — the reflection / data-binding widget
/// graph behind screens such as the paragon board. This is the
/// <i>raw</i> graph only: per widget, its name, class id, and the
/// schema + bound field values exactly as serialized. No evaluation,
/// imaging, layout, or policy is applied (that boundary is the
/// consumer's — see <c>docs/casc-diablo4-format.md</c> Appendix C and
/// §10). For the typed paragon projection use
/// <c>Diablo4Storage.ReadParagonRenderLayout</c> (§7.1 of the
/// FR-C7 contract).
/// </summary>
/// <remarks>
/// Byte format: <c>docs/casc-diablo4-format.md §10</c>. The parser is
/// built only on the independently-proven facts: the pinned record
/// header (§10.3 — <c>classOff = nameStart + alignUp8(len+1) + 0x10</c>,
/// <c>0xFFFFFFFF</c> sentinel at <c>classOff+0x08</c>), the 12-byte
/// schema entry <c>(fieldHash, typeHash("DT_BINDABLEPROPERTY"),
/// DT_type)</c>, and the fixed 56-byte <c>0x22</c> instance record with
/// the bound value at <c>+0x08</c>, positionally keyed to the schema.
/// </remarks>
public sealed record UiScene(int SnoId, IReadOnlyList<UiWidget> Widgets)
{
    /// <summary>Diablo IV UI-scene SNO group id (CoreTOC type
    /// <c>UI</c>).</summary>
    public const int Group = 46;

    /// <summary>The expected per-group format hash for this family
    /// (<c>0xE4825AB8</c>).</summary>
    public const uint FormatHash = 0xE4825AB8u;

    /// <summary>
    /// Decode a UI-scene SNO blob (the <c>Meta</c> bytes, including the
    /// 16-byte <c>SNOFileHeader</c>). Raw graph only; never throws on
    /// unknown fields — unrecognised type ids are surfaced as their raw
    /// hash so the contract stays lossless.
    /// </summary>
    /// <param name="snoId">The SNO id (for <see cref="SnoId"/>).</param>
    /// <param name="blob">The full SNO <c>Meta</c> blob.</param>
    public static UiScene Parse(int snoId, ReadOnlySpan<byte> blob)
    {
        const uint DtBindableProperty = 0x1332C78Du; // typeHash("DT_BINDABLEPROPERTY")
        const uint Sentinel = 0xFFFFFFFFu;
        const byte RecordTag = 0x22;
        const int RecordSize = 0x38;                  // 56-byte instance record

        static int AlignUp8(int n) => (n + 7) & ~7;
        static uint U32(ReadOnlySpan<byte> b, int o) =>
            (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
        static bool IsNameByte(byte c) =>
            c is (>= (byte)'A' and <= (byte)'Z')
              or (>= (byte)'a' and <= (byte)'z')
              or (>= (byte)'0' and <= (byte)'9')
              or (byte)'_';
        static bool IsNameStart(byte c) =>
            c is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z');

        // Pass 1: every valid widget header (name → classOff via the
        // pinned formula → require the 0xFFFFFFFF sentinel). The
        // sentinel check rejects coincidental identifier strings.
        var starts = new List<(int nameStart, string name, uint classId)>();
        int i = 0;
        while (i < blob.Length)
        {
            if (!IsNameStart(blob[i])) { i++; continue; }
            int s = i, e = i;
            while (e < blob.Length && IsNameByte(blob[e])) e++;
            int len = e - s;
            // names are NUL-terminated; require the terminator and a
            // reasonable identifier length.
            if (len >= 4 && e < blob.Length && blob[e] == 0)
            {
                int classOff = s + AlignUp8(len + 1) + 0x10;
                if (classOff + 12 <= blob.Length &&
                    U32(blob, classOff + 8) == Sentinel)
                {
                    starts.Add((s,
#if NETSTANDARD2_0
                        System.Text.Encoding.ASCII.GetString(blob.Slice(s, len).ToArray()),
#else
                        System.Text.Encoding.ASCII.GetString(blob.Slice(s, len)),
#endif
                        U32(blob, classOff)));
                    i = e; // skip past the consumed name
                    continue;
                }
            }
            i = e > i ? e : i + 1;
        }

        // Pass 2: per widget, its byte span is [nameStart, nextStart).
        // Within it, the schema run is every (x, DT_BINDABLEPROPERTY, y)
        // triplet; the instance values are the +0x08 of every 56-byte
        // 0x22 record, positionally keyed to the schema.
        var widgets = new List<UiWidget>(starts.Count);
        for (int w = 0; w < starts.Count; w++)
        {
            int from = starts[w].nameStart;
            int to = w + 1 < starts.Count ? starts[w + 1].nameStart : blob.Length;

            var fields = new List<(uint f, uint t)>();
            for (int k = from; k + 12 <= to; k += 4)
                if (U32(blob, k + 4) == DtBindableProperty)
                {
                    fields.Add((U32(blob, k), U32(blob, k + 8)));
                    k += 8; // advance past this triplet (loop adds 4)
                }

            var values = new List<uint>();
            for (int p = from; p + RecordSize <= to; )
            {
                if (blob[p] == RecordTag && U32(blob, p) == RecordTag)
                { values.Add(U32(blob, p + 8)); p += RecordSize; }
                else p += 4;
            }

            var uf = new UiField[fields.Count];
            for (int q = 0; q < fields.Count; q++)
                uf[q] = new UiField(fields[q].f, fields[q].t,
                    q < values.Count ? values[q] : 0u,
                    q < values.Count);
            widgets.Add(new UiWidget(starts[w].name, starts[w].classId, uf));
        }

        return new UiScene(snoId, widgets);
    }
}

/// <summary>
/// One widget in a <see cref="UiScene"/>: its inline name, its class id
/// (<c>= Diablo4.TypeHash(class name)</c>), and its bound fields in
/// serialized order.
/// </summary>
public sealed record UiWidget(
    string Name, uint ClassId, IReadOnlyList<UiField> Fields);

/// <summary>
/// One bound field of a <see cref="UiWidget"/>: the field-name hash
/// (<c>= Diablo4.FieldHash(name)</c>), the underlying <c>DT_*</c> type
/// hash (<c>= Diablo4.TypeHash("DT_…")</c>), and the raw 32-bit bound
/// value (interpretation — int / SNO handle / RGBA / enum / byte — is
/// the consumer's, per <see cref="System.Type"/> of the field).
/// </summary>
/// <param name="FieldHash">The 28-bit field-name hash.</param>
/// <param name="TypeHash">The underlying <c>DT_*</c> type hash.</param>
/// <param name="RawValue">The raw bound 32-bit value.</param>
/// <param name="HasValue"><see langword="true"/> if a positional
/// instance record supplied the value; <see langword="false"/> when the
/// field is declared but unbound in this scene.</param>
public readonly record struct UiField(
    uint FieldHash, uint TypeHash, uint RawValue, bool HasValue);
