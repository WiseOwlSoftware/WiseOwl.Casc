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
/// <see cref="Diablo4Storage.ReadParagonRenderLayout"/> (§7.1 of the
/// FR-C7 contract).
/// </summary>
/// <remarks>
/// Byte format: <c>docs/casc-diablo4-format.md §10</c>. The parser is
/// built only on the independently-proven facts: the pinned record
/// header (§10.3 — <c>classOff = nameStart + alignUp8(len+1) + 0x10</c>,
/// <c>0xFFFFFFFF</c> sentinel at <c>classOff+0x08</c>), the 12-byte
/// schema entry <c>(fieldHash, typeHash("DT_BINDABLEPROPERTY"),
/// DT_type)</c>, and the per-field instance value record — the bound
/// value at <c>+0x08</c> of either a 56-byte <c>0x22</c> literal record
/// or a 12-byte tag-2 block (<c>tag==2, +4==0</c>) — positionally keyed
/// to the schema (FR-C16 R7; widgets use either encoding, sometimes
/// mixed). Parent widgets whose span nests anonymous child sub-records
/// confine their own field scan to the run before the first child.
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
                        System.Text.Encoding.ASCII.GetString(blob.Slice(s, len)),
                        U32(blob, classOff)));
                    i = e; // skip past the consumed name
                    continue;
                }
            }
            i = e > i ? e : i + 1;
        }

        // Pass 1b: the class ids actually used as widget headers — for
        // nested-child detection below (a child sub-record reuses one of
        // these class ids with the 0xFFFFFFFF sentinel at +0x08).
        var classIds = new HashSet<uint>();
        foreach (var st in starts) classIds.Add(st.classId);

        // Pass 2: per widget, its byte span is [nameStart, nextStart).
        // Within it, the schema run is every (x, DT_BINDABLEPROPERTY, y)
        // triplet; each schema field has exactly one instance value
        // record, positionally keyed to the schema. A value record is one
        // of two interchangeable shapes (FR-C16 R7 — both proven against
        // the live scene 657304):
        //   • the 56-byte 0x22 literal record (value at +0x08), and
        //   • the 12-byte tag-2 block (tag==2, +4==0, value at +0x08).
        // Different widgets use different encodings for the same fields
        // (e.g. Node_IconBase is all-0x22; Template_Board_Background_Center
        // is all-tag-2; Node_Icon mixes them). Reading only 0x22 records
        // (the pre-R7 parser) under-decoded the tag-2 widgets — a chrome
        // centre's authored 1200² rect read as all-zero, and a mixed
        // widget's positional keying collapsed (CL-47 errata). The
        // field-value run is the first `fields.Count` records: they
        // precede any trailing 0x58 layer-block (Pass 2c), so the count
        // cleanly bounds the run.
        var widgets = new List<UiWidget>(starts.Count);
        for (int w = 0; w < starts.Count; w++)
        {
            int from = starts[w].nameStart;
            int to = w + 1 < starts.Count ? starts[w + 1].nameStart : blob.Length;

            // FR-C16 R7: a PARENT widget's span can contain anonymous,
            // name-less child sub-records (a class id + 0xFFFFFFFF sentinel
            // at +0x08 — the rarity sub-templates' per-state disc layers).
            // Confine the parent's own schema + value scan to [from, ownEnd)
            // so child fields/values never bleed into the parent's keying.
            // The 0x58-block scan (Pass 2c) keeps the full span — it
            // harvests those child layer handles.
            int ownClassOff = from + AlignUp8(starts[w].name.Length + 1) + 0x10;
            int ownEnd = to;
            for (int o = ownClassOff + 4; o + 12 <= to; o += 4)
                if (U32(blob, o + 8) == Sentinel && classIds.Contains(U32(blob, o)))
                { ownEnd = o; break; }

            var fields = new List<(uint f, uint t)>();
            for (int k = from; k + 12 <= ownEnd; k += 4)
                if (U32(blob, k + 4) == DtBindableProperty)
                {
                    fields.Add((U32(blob, k), U32(blob, k + 8)));
                    k += 8; // advance past this triplet (loop adds 4)
                }

            var values = new List<uint>();
            for (int p = from; p + 12 <= ownEnd && values.Count < fields.Count; )
            {
                if (blob[p] == RecordTag && U32(blob, p) == RecordTag)
                {
                    // 0x22 literal record. Its value (+0x08) is readable
                    // even when the full 56 bytes straddle ownEnd (the
                    // last record of a widget; FR-C8 R6 / CL-24).
                    values.Add(U32(blob, p + 8));
                    p += RecordSize;
                }
                else if (U32(blob, p) == 2u && U32(blob, p + 4) == 0u)
                {
                    values.Add(U32(blob, p + 8)); // tag-2 block
                    p += 12;
                }
                else p += 4;
            }

            var uf = new UiField[fields.Count];
            for (int q = 0; q < fields.Count; q++)
                uf[q] = new UiField(fields[q].f, fields[q].t,
                    q < values.Count ? values[q] : 0u,
                    q < values.Count);

            // FR-C16 R9 / FR-C18: the anonymous child sub-records nested in
            // a parent widget's span are NOT a flat handle soup — each is a
            // self-contained mini-widget (a class id + 0xFFFFFFFF sentinel,
            // name-less, then its own schema run + positionally-keyed value
            // records, exactly like a named widget). Parse them structurally
            // so each child's authored rect stays paired with its handle
            // (the rarity disc / starter filigree / gate ornate layers each
            // carry their own inset; the prior flat ExtraLayerValues lost
            // that pairing — CL-46's all-zero rarity rect and the consumer's
            // full-cell over-paint both trace to it). A child's span runs
            // from its marker to the next sibling marker (or the parent end).
            // Children are not observed to nest further in scene 657304, so
            // the parse is one level deep.
            var children = new List<UiWidgetChild>();
            for (int o = ownClassOff + 4; o + 12 <= to; o += 4)
            {
                if (U32(blob, o + 8) != Sentinel || !classIds.Contains(U32(blob, o)))
                    continue;
                int childStart = o;
                uint childClass = U32(blob, o);
                // next sibling marker bounds this child.
                int childEnd = to;
                for (int n = o + 12; n + 12 <= to; n += 4)
                    if (U32(blob, n + 8) == Sentinel && classIds.Contains(U32(blob, n)))
                    { childEnd = n; break; }

                var cFields = new List<(uint f, uint t)>();
                for (int k = childStart; k + 12 <= childEnd; k += 4)
                    if (U32(blob, k + 4) == DtBindableProperty)
                    { cFields.Add((U32(blob, k), U32(blob, k + 8))); k += 8; }

                var cValues = new List<uint>();
                for (int p = childStart; p + 12 <= childEnd && cValues.Count < cFields.Count; )
                {
                    if (blob[p] == RecordTag && U32(blob, p) == RecordTag)
                    { cValues.Add(U32(blob, p + 8)); p += RecordSize; }
                    else if (U32(blob, p) == 2u && U32(blob, p + 4) == 0u)
                    { cValues.Add(U32(blob, p + 8)); p += 12; }
                    else p += 4;
                }

                var cf = new UiField[cFields.Count];
                for (int q = 0; q < cFields.Count; q++)
                    cf[q] = new UiField(cFields[q].f, cFields[q].t,
                        q < cValues.Count ? cValues[q] : 0u, q < cValues.Count);
                children.Add(new UiWidgetChild(childClass, cf));
                o = childEnd - 4; // resume scan at the next sibling marker
            }

            // Pass 2c (FR-C8/FR-C9): the "bound-layer block" — a value
            // bound NOT as a 56-byte 0x22 record but as the shape
            //   +0x00 u32 tag = 2   +0x04 u32 0   +0x08 u32 value
            // (start/gate frames, rare/leg ornate, grey ring, …). The
            // FR-C8/CL-23 model over-fit two example blocks
            // (owner-class-id @+0x20, 0xFFFFFFFF @+0x28); those words are
            // NOT universal (other blocks carry a pointer / zeros there),
            // and the *last* block of a widget straddles the next
            // nameStart so its tail is unreadable anyway — exactly the
            // CL-24 lesson, generalised (FR-C9, CL-26). The only stable,
            // self-validating marker is `tag==2, +4==0, value@+8`;
            // capture every such block's value losslessly (the typed
            // projection / the consumer catalog-validate — raw stays
            // raw). Bound on the value field (p+12), never the full
            // block, so no straddling tail is dropped.
            var extra = new List<uint>();
            for (int p = from; p + 12 <= to; )
            {
                if (U32(blob, p) == 2u && U32(blob, p + 4) == 0u)
                {
                    uint v = U32(blob, p + 8);
                    if (v is not 0u and not Sentinel) extra.Add(v);
                    p += 12;
                }
                else p += 4;
            }

            widgets.Add(new UiWidget(starts[w].name, starts[w].classId, uf, extra, children));
        }

        return new UiScene(snoId, widgets);
    }
}

/// <summary>
/// One widget in a <see cref="UiScene"/>: its inline name, its class id
/// (<c>= Diablo4.TypeHash(class name)</c>), its bound fields in
/// serialized order, and any <see cref="ExtraLayerValues"/> bound via
/// the 0x58-block shape (FR-C8 — the start/gate composite layers).
/// </summary>
/// <param name="Name">The widget's inline name.</param>
/// <param name="ClassId">The class id (<c>= Diablo4.TypeHash(class)</c>).</param>
/// <param name="Fields">Bound fields (56-byte 0x22 path), in order.</param>
/// <param name="ExtraLayerValues">Values bound via the fixed 0x58-block
/// shape (tag 2, sentinel at +0x28), in serialized order — the layer
/// stack for templates like <c>Template_Node_Starter</c> /
/// <c>Template_Node_Quest</c> whose composites the §10.3 0x22 scan does
/// not model. Raw values (e.g. texture handles); interpretation is the
/// consumer's / the typed projection's. <b>Flat and lossy</b> for the
/// per-child geometry (handles and rect insets are interleaved with no
/// pairing) — prefer <see cref="Children"/> when the handle↔rect
/// association matters; <see cref="ExtraLayerValues"/> is retained for
/// the §10.14 losslessness/coverage guarantee.</param>
/// <param name="Children">FR-C16 R9 / FR-C18 — the anonymous child
/// sub-records nested in this widget's span, in serialized (z) order,
/// each parsed structurally as a name-less mini-widget (its own class id
/// + bound fields). Empty for a leaf widget. The per-rarity / start /
/// gate templates (<c>Template_Node_Magic</c>/<c>Rare</c>/<c>Legendary</c>/
/// <c>Starter</c>/<c>Quest</c>) carry their disc / ornate / filigree /
/// locator layers here, each with its own authored <c>hImageFrame</c> +
/// rect insets — the pairing <see cref="ExtraLayerValues"/> flattens
/// away.</param>
public sealed record UiWidget(
    string Name, uint ClassId, IReadOnlyList<UiField> Fields,
    IReadOnlyList<uint> ExtraLayerValues,
    IReadOnlyList<UiWidgetChild> Children);

/// <summary>
/// An anonymous child sub-record of a <see cref="UiWidget"/> (FR-C16 R9 /
/// FR-C18). The Diablo IV UI scene serializes a composite node template
/// (e.g. <c>Template_Node_Magic</c>) as a parent widget whose disc /
/// ornate / overlay layers are <i>name-less</i> nested records — each a
/// class id + <c>0xFFFFFFFF</c> sentinel followed by its own schema +
/// positionally-keyed value records, structurally identical to a named
/// widget minus the inline name. Decoding them structurally keeps each
/// layer's authored rect paired with its <c>hImageFrame</c> handle.
/// </summary>
/// <param name="ClassId">The child's class id
/// (<c>= Diablo4.TypeHash(class)</c>; typically <c>UIWindowStyle</c>).</param>
/// <param name="Fields">The child's bound fields, in serialized order
/// (same shape as <see cref="UiWidget.Fields"/>) — its <c>hImageFrame</c>
/// handle, <c>nLeft/nTop/nRight/nBottom/nWidth/nHeight</c> rect insets,
/// <c>bActive</c>, etc.</param>
public sealed record UiWidgetChild(
    uint ClassId, IReadOnlyList<UiField> Fields);

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
