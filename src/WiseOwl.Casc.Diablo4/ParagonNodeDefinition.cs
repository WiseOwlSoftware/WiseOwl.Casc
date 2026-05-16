using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// The verified first-party paragon node rarity values
/// (<c>eRarityOverride</c>). A named-int convenience over
/// <see cref="ParagonNodeDefinition.RarityOverride"/> — not policy; the raw
/// int remains the serialized contract.
/// </summary>
public enum ParagonRarity
{
    /// <summary>Normal / structural node (start, gate, socket). Value 0.</summary>
    Common = 0,
    /// <summary>Magic node (<c>*_Magic_*</c>). Value 2.</summary>
    Magic = 2,
    /// <summary>Rare node (<c>*_Rare_*</c>). Value 3.</summary>
    Rare = 3,
    /// <summary>Legendary node. Value 4.</summary>
    Legendary = 4,
}

/// <summary>
/// One attribute grant on a paragon node (an <c>AttributeSpecifier</c>,
/// stride 88). Raw decoded fields only — the magnitude is produced by
/// evaluating <see cref="InlineFormula"/> or the GameBalance formula named
/// by <see cref="FormulaGbid"/>; evaluation and the calibrated intrinsics
/// are the consumer's, not the library's (see <c>docs/casc-format.md</c>
/// library boundary).
/// </summary>
/// <param name="AttributeId"><c>eAttribute</c> at specifier <c>+0</c> — the
/// attribute id (== Maxroll attribute numeric key).</param>
/// <param name="NParam"><c>nParam</c> at specifier <c>+4</c> (DT_INT; 0 for
/// plain stat nodes). This is the field historically called "Param".</param>
/// <param name="ParamPlus12">The distinct value at specifier <c>+12</c>
/// (verified: <c>MaximumDominance</c> → 11, <c>EarthquakeDuration</c> →
/// 1031902). Exposed raw so the consumer never re-parses the specifier.</param>
/// <param name="FormulaGbid"><c>gbidFormula</c> at specifier <c>+48</c>
/// (DT_GBID). <c>0xFFFFFFFF</c> means "no shared formula — use
/// <see cref="InlineFormula"/>"; otherwise it is
/// <c>GbidHash(formulaName)</c> resolvable through
/// <see cref="AttributeFormulaTable"/>.</param>
/// <param name="InlineFormula">The node's own formula source text (read at
/// specifier <c>+24</c> offset / <c>+28</c> size, payload-relative) when
/// <see cref="FormulaGbid"/> is <c>0xFFFFFFFF</c>; otherwise empty.</param>
public readonly record struct NodeAttribute(
    int AttributeId,
    int NParam,
    int ParamPlus12,
    uint FormulaGbid,
    string InlineFormula)
{
    /// <summary>The <see cref="FormulaGbid"/> sentinel meaning "use
    /// <see cref="InlineFormula"/>".</summary>
    public const uint NoGbid = 0xFFFFFFFF;

    /// <summary>True when this attribute carries its own inline formula text
    /// rather than referencing a shared GameBalance formula by GBID.</summary>
    public bool IsInline => FormulaGbid == NoGbid;
}

/// <summary>
/// A decoded Diablo IV <c>ParagonNodeDefinition</c> (<c>.pgn</c>, SNO group
/// 106). Raw fields only — no rarity scaling, no formula evaluation, no
/// scoring (that interpretation is permanently the consumer's).
/// </summary>
/// <remarks>
/// Byte layout per the canonical reference (<c>docs/casc-format.md</c>,
/// migrated/verified from the upstream <c>d4-binary-formats.md §5</c>):
/// payload base <c>0x10</c>; <c>snoId@0</c>; <c>hIcon@8</c> (DT_UINT);
/// <c>hIconMask@12</c> (DT_UINT); <c>eRarityOverride@20</c> (0/2/3/4);
/// <c>snoPassivePower@24</c> (DT_SNO, group 29 Power); <c>ptAttributes</c>
/// <c>DT_VARIABLEARRAY[AttributeSpecifier]</c> descriptor <c>@32</c>
/// (<c>dataOffset</c> payload-relative <c>@+8</c>, <c>dataSize@+12</c>;
/// element stride 88); <c>bHasSocket@80</c>; <c>bIsGate@84</c>.
/// </remarks>
public sealed class ParagonNodeDefinition
{
    private const int AttrStride = 88;

    private readonly NodeAttribute[] _attributes;

    private ParagonNodeDefinition(
        int snoId, int rarityOverride, bool hasSocket, bool isGate,
        uint hIcon, uint hIconMask, int snoPassivePower,
        NodeAttribute[] attributes)
    {
        SnoId = snoId;
        RarityOverride = rarityOverride;
        HasSocket = hasSocket;
        IsGate = isGate;
        HIcon = hIcon;
        HIconMask = hIconMask;
        SnoPassivePower = snoPassivePower;
        _attributes = attributes;
    }

    /// <summary>The node's own SNO id.</summary>
    public int SnoId { get; }

    /// <summary>Raw <c>eRarityOverride</c> (<b>0</b>=Common/structural,
    /// <b>2</b>=Magic, <b>3</b>=Rare, <b>4</b>=Legendary). This exact int is
    /// the serialized contract — kept raw deliberately.</summary>
    public int RarityOverride { get; }

    /// <summary><see cref="RarityOverride"/> as the verified enum
    /// (convenience; the raw int remains authoritative).</summary>
    public ParagonRarity Rarity => (ParagonRarity)RarityOverride;

    /// <summary><c>bHasSocket</c> — a glyph-socket node.</summary>
    public bool HasSocket { get; }

    /// <summary><c>bIsGate</c> — a board-attachment gate node.</summary>
    public bool IsGate { get; }

    /// <summary><c>hIcon</c> (DT_UINT, <c>+8</c>). Not a SNO id; the
    /// first-party icon link — equals a <see cref="TexFrame.ImageHandle"/>
    /// (usually 0 here; the symbol handle is normally
    /// <see cref="HIconMask"/>).</summary>
    public uint HIcon { get; }

    /// <summary><c>hIconMask</c> (DT_UINT, <c>+12</c>). The symbol icon
    /// handle; equals a <see cref="TexFrame.ImageHandle"/> in a paragon
    /// atlas (resolve via <see cref="Diablo4Storage.TryGetIconFrame"/>).</summary>
    public uint HIconMask { get; }

    /// <summary><c>snoPassivePower</c> (DT_SNO, group 29 Power, <c>+24</c>) —
    /// the node's granted passive power SNO id (0 / <c>0xFFFFFFFF</c> when
    /// none). Exposed raw for future node→power character modeling.</summary>
    public int SnoPassivePower { get; }

    /// <summary>The node's attribute grants (raw <see cref="NodeAttribute"/>
    /// specifiers).</summary>
    public IReadOnlyList<NodeAttribute> Attributes => _attributes;

    /// <summary>Decode a ParagonNode from its raw SNO blob.</summary>
    public static ParagonNodeDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        var snoId = r.SnoId;
        var hIcon = r.U32(8);
        var hIconMask = r.U32(12);
        var rarity = r.I32(20);
        var snoPassivePower = r.I32(24);
        var hasSocket = r.I32(80) != 0;
        var isGate = r.I32(84) != 0;

        // ptAttributes DT_VARIABLEARRAY @ payload 32:
        //   dataOffset (payload-relative) @ +8, dataSize @ +12.
        var dataOffset = (int)r.U32(32 + 8);
        var dataSize = (int)r.U32(32 + 12);
        var count = dataSize > 0 ? dataSize / AttrStride : 0;
        var attrs = new NodeAttribute[count];
        for (var i = 0; i < count; i++)
        {
            var e = dataOffset + i * AttrStride;          // payload-relative
            var gbid = r.U32(e + 48);
            var inlineOff = r.I32(e + 24);
            var inlineSize = r.I32(e + 28);
            var inline = gbid == NodeAttribute.NoGbid && inlineSize > 0
                ? r.Ascii(inlineOff, inlineSize)
                : string.Empty;
            attrs[i] = new NodeAttribute(
                AttributeId: r.I32(e + 0),
                NParam: r.I32(e + 4),
                ParamPlus12: r.I32(e + 12),
                FormulaGbid: gbid,
                InlineFormula: inline);
        }

        return new ParagonNodeDefinition(
            snoId, rarity, hasSocket, isGate, hIcon, hIconMask,
            snoPassivePower, attrs);
    }
}
