using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <b>UI tile-style</b> record (<c>.uis</c>, SNO group
/// <see cref="SnoGroup.UiStyle"/> = 103) — the engine's recipe for
/// rendering a tiled UI overlay (vignette, inner-shadow, bag background,
/// frame chrome, …) as a composition of texture pieces with a scale
/// factor and padding. FR-C14 Phase 4 (CL-42).
/// </summary>
/// <remarks>
/// <para>
/// The mechanism: a UI widget can carry a <c>DT_SNO</c> field named
/// <c>snoTiledStyle</c> (<see cref="Diablo4.FieldHash"/>(<c>"snoTiledStyle"</c>) =
/// 0x07DB38D3) pointing to one of these records. At render time the
/// engine consults the bound tile-style and composes the overlay from
/// the style's piece handles + image-scale + padding. Distinct from
/// the per-widget <c>hImage</c> texture handle, which is the primary
/// content; <c>snoTiledStyle</c> defines the *framing/composition*
/// applied to the widget's rect.
/// </para>
/// <para>
/// Record layout (verified across 8 dumped SNOs from scene 657304's
/// <c>snoTiledStyle</c> bindings: 843662, 1309282, 872641, 787949,
/// 603760, 792649, 1841254, plus 20/3/1 sentinel values):
/// </para>
/// <code>
///   +0x00  uint32   magic        = 0xDEADBEEF
///   +0x04  byte[12] header pad   = 0
///   +0x10  int32    SnoId        (self-reference)
///   +0x14  byte[12] pad          = 0
///   +0x20  uint32   field_off    (typically 0x30 — payload start)
///   +0x24  uint32   field_size   (typically 0x58 — payload size)
///   +0x28  uint32   field_count  (typically 1)
///   +0x2C  byte[36] zero
///   +0x50  uint32   TypeTag      (variant identifier — 0xBC0D579E for
///                                 HorizontalTiledWindowPieces-like;
///                                 0x02E46583 for a different variant
///                                 observed on BagBackground 603760)
///   +0x54  uint32   pad          = 0
///   +0x58  float32  flImageScale (the documented scaling factor —
///                                 observed values 1.0f, 0.5f, 0.9f)
///   +0x5C  uint32   pad          = 0
///   +0x60  uint32   PrimaryHandle (the primary texture handle —
///                                  hPieceMiddle in the 3-slice variant,
///                                  observed varying per record)
///   +0x64..  variable-length per-variant suffix (sub-rects, padding
///            spec, additional piece handles for 9-slice — only
///            partially decoded as of FR-C14 R9; honest sentinel via
///            <see cref="HasPartialDecode"/>)
/// </code>
/// <para>
/// The full multi-piece structure (hPieceLeft/hPieceRight/hPieceTop/
/// hPieceBottom plus per-piece sub-rects) is encoded in the trailing
/// bytes per the <c>HorizontalTiledWindowPieces</c> /
/// <c>VerticalTiledWindowPieces</c> schemas published by
/// <c>blizzhackers/d4data</c>. The variant is selected via
/// <see cref="TypeTag"/>. FR-C14 R9 surfaces the verified primary
/// handle + image scale + type tag; future iterations will decode the
/// variant suffixes per the cumulative-hash-decode principle (memory
/// <c>feedback_cumulative-hash-decode</c>).
/// </para>
/// </remarks>
/// <param name="SnoId">The record's own SNO id (self-reference at +0x10).</param>
/// <param name="TypeTag">The polymorphic-variant tag at +0x50.
/// <c>0xBC0D579E</c> is the most-common variant observed (7 of 8 dumped
/// records); <c>0x02E46583</c> appears on at least one record
/// (<see cref="SnoId"/> 603760 BagBackground). The exact variant name
/// for each tag is a future-cracked TypeHash.</param>
/// <param name="ImageScale">The <c>flImageScale</c> field at +0x58 —
/// the engine's per-record scale factor for the composed tile. Observed
/// values 1.0, 0.5, 0.9 across the dumped records.</param>
/// <param name="PrimaryHandle">The texture handle at +0x60. In the
/// 3-slice <c>HorizontalTiledWindowPieces</c> variant this is the
/// <c>hPieceMiddle</c>; in other variants its semantic role differs.
/// Resolved via the engine's icon-frame index; <c>0u</c> means absent.</param>
/// <param name="HasPartialDecode"><see langword="true"/> when the
/// record's trailing variant-specific bytes (additional piece handles,
/// sub-rects) were not fully decoded by the current
/// <see cref="Parse(ReadOnlySpan{byte})"/> implementation. Consumers
/// that need the full composition should treat the absent fields as
/// "unknown" rather than "zero".</param>
public sealed record TiledStyleDefinition(
    int SnoId,
    uint TypeTag,
    float ImageScale,
    uint PrimaryHandle,
    bool HasPartialDecode)
{
    /// <summary>The magic that prefixes every <c>.uis</c> SNO blob — the
    /// engine's universal "valid serialized object" marker.</summary>
    public const uint Magic = 0xDEADBEEFu;

    /// <summary>The format hash for <see cref="SnoGroup.UiStyle"/>, as
    /// reported by <see cref="CoreToc.FormatHashFor"/>. Surfaced for
    /// callers that want to verify the group binding before reading.</summary>
    public const uint FormatHash = 0x80504E18u;

    /// <summary>The polymorphic-variant tag for the most common
    /// TiledStyle records (e.g. InnerShadow 843662, Tutorial_Highlight
    /// 872641, Frame_AbilityPoints 1309282) — 7 of 8 records dumped
    /// during FR-C14 R8 carry this tag. Its underlying type name (a
    /// <see cref="Diablo4.TypeHash"/>) is uncracked.</summary>
    public const uint TypeTagHorizontalPieces = 0xBC0D579Eu;

    /// <summary>Parse a tile-style record from its raw SNO blob.</summary>
    /// <param name="blob">The raw <c>.uis</c> blob (≥ 96 bytes; the
    /// records observed in FR-C14 are 152..168 bytes depending on the
    /// variant suffix). The first 4 bytes must be <see cref="Magic"/>.</param>
    /// <exception cref="FormatException">If <paramref name="blob"/> is
    /// too short or doesn't start with <see cref="Magic"/>.</exception>
    public static TiledStyleDefinition Parse(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < 0x68)
            throw new FormatException(
                $"TiledStyle blob too short ({blob.Length} bytes; expected ≥ 0x68).");
        var magic = Bytes.U32LE(blob, 0);
        if (magic != Magic)
            throw new FormatException(
                $"TiledStyle blob: expected magic 0x{Magic:X8}, got 0x{magic:X8}.");
        var snoId = Bytes.I32LE(blob, 0x10);
        var typeTag = Bytes.U32LE(blob, 0x50);
        var scaleU = Bytes.U32LE(blob, 0x58);
        var imageScale = BitConverter.ToSingle(BitConverter.GetBytes(scaleU), 0);
        var primaryHandle = Bytes.U32LE(blob, 0x60);
        // The 152..168-byte records have variant-specific trailing data
        // we don't fully decode yet (additional piece handles, sub-rects).
        // Mark partial-decode so callers don't mistake silence for zeros.
        var hasPartial = blob.Length > 0x68;
        return new TiledStyleDefinition(snoId, typeTag, imageScale, primaryHandle, hasPartial);
    }
}

/// <summary>
/// FR-C14 R9 — one binding of a UI widget's <c>snoTiledStyle</c> field
/// to a <see cref="TiledStyleDefinition"/>. Surfaced on
/// <see cref="ParagonBoardChrome.TiledStyleBindings"/> so consumers can
/// reconstruct the engine's overlay composition for the paragon board.
/// </summary>
/// <param name="WidgetName">The bound widget's name (e.g.
/// <c>"Vignette"</c>, <c>"Board_Info"</c>, <c>"Paragon_Points_Container"</c>).
/// Names are useful for the consumer to attribute the overlay to a
/// specific visual element; semantic-role naming is the consumer's
/// (per memory <c>feedback_widget-name-not-role</c>).</param>
/// <param name="WidgetClassId">The bound widget's class id (28-bit
/// truncated DJB2 of the class name). <c>0x1E3077C7</c> is the standard
/// "draw a textured rect" widget class (Background, Framing, Vignette,
/// Divider …); <c>0x112661D5</c> is the Stack/Layout container class
/// (Layout_Stack, ControllerStack, Board_Selector_BG …).</param>
/// <param name="TiledStyleSnoId">The SNO id the widget's
/// <c>snoTiledStyle</c> field is bound to. Pass to
/// <see cref="Diablo4Storage.ReadTiledStyle"/> to read the full record;
/// <see cref="Style"/> is the pre-read convenience.</param>
/// <param name="Style">The pre-read <see cref="TiledStyleDefinition"/>,
/// or <see langword="null"/> if the SNO id couldn't be read (typically
/// because it's a sentinel value like <c>1</c>, <c>3</c>, <c>20</c> —
/// observed as "enum-shaped" small-id bindings whose semantics are not
/// the same as a real SNO reference).</param>
public sealed record TiledStyleBinding(
    string WidgetName,
    uint WidgetClassId,
    int TiledStyleSnoId,
    TiledStyleDefinition? Style);
