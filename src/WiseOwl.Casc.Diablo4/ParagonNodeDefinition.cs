using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
/// The verified first-party paragon node <c>eNodeType</c> values
/// (<see cref="ParagonNodeDefinition.NodeType"/>, payload <c>+16</c>). A
/// named-int convenience over the raw int — not policy; the raw int remains
/// the serialized contract. Observed across all seven class boards: every
/// class start node is <see cref="Start"/> (5); magic nodes are
/// <see cref="Magic"/> (3); normal/structural/gate/rare nodes are
/// <see cref="Normal"/> (0). This is a distinct axis from
/// <see cref="ParagonRarity"/> (e.g. a rare node is <c>NodeType 0</c> with
/// <c>RarityOverride 3</c>); values other than 0/3/5 have not been observed
/// and would surface here as the raw cast.
/// </summary>
public enum ParagonNodeType
{
    /// <summary>Normal / structural node — also the gate and (observed) rare
    /// nodes. Value 0.</summary>
    Normal = 0,
    /// <summary>Magic node. Value 3.</summary>
    Magic = 3,
    /// <summary>Board start node (the class emblem). Value 5 — verified on all
    /// seven class start boards.</summary>
    Start = 5,
}

/// <summary>
/// One attribute grant on a paragon node (an <c>AttributeSpecifier</c>,
/// stride 88). Raw decoded fields only — the magnitude is produced by
/// evaluating <see cref="InlineFormula"/> or the GameBalance formula named
/// by <see cref="FormulaGbid"/>; evaluation and the calibrated intrinsics
/// are the consumer's, not the library's (see
/// <c>docs/casc-diablo4-format.md</c> Appendix C, library boundary).
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
/// <param name="AttributeGbid">The attribute's GBID from the node's second
/// parallel array (descriptor at payload <c>+88</c>; one <see cref="uint"/>
/// per attribute, in <see cref="ParagonNodeDefinition.Attributes"/> order).
/// Stable per <see cref="AttributeId"/> across nodes (e.g.
/// <c>AttributeId 9</c> → <c>0x1E663884</c> everywhere it appears), so it is a
/// reliable secondary key for the same <c>eAttribute</c>. Its canonical
/// resource name is not yet recovered (it is not a DJB2/GBID hash of any
/// tested attribute label); surfaced raw rather than left undecoded. <c>0</c>
/// when the node has no parallel entry for this attribute.</param>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "\"Attribute\" is the established Diablo IV domain term " +
        "(the serialized eAttribute field; matches the spec, ARTICLE-SOURCE, " +
        "and upstream RE record). This is a data record struct, not a " +
        "System.Attribute; renaming would diverge the code from the " +
        "canonical byte-format vocabulary.")]
public readonly record struct NodeAttribute(
    int AttributeId,
    int NParam,
    int ParamPlus12,
    uint FormulaGbid,
    string InlineFormula,
    uint AttributeGbid)
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
/// Byte layout per the canonical reference (<c>docs/casc-diablo4-format.md §7.2</c>,
/// migrated/verified from the upstream <c>d4-binary-formats.md §5</c>):
/// payload base <c>0x10</c>; <c>snoId@0</c>; <c>hIcon@8</c> (DT_UINT);
/// <c>hIconMask@12</c> (DT_UINT); <c>eNodeType@16</c> (0/3/5; see
/// <see cref="ParagonNodeType"/>); <c>eRarityOverride@20</c> (0/2/3/4);
/// <c>snoPassivePower@24</c> (DT_SNO, group 29 Power); <c>ptAttributes</c>
/// <c>DT_VARIABLEARRAY[AttributeSpecifier]</c> descriptor <c>@32</c>
/// (<c>dataOffset</c> payload-relative <c>@+8</c>, <c>dataSize@+12</c>;
/// element stride 88); a <c>DT_VARIABLEARRAY[DT_SNO]</c> descriptor
/// <c>@48</c> — the bonus-passive-power slot (size-1 on rares; empty
/// otherwise; see <see cref="BonusPassivePowerSno"/>); a
/// <c>DT_VARIABLEARRAY[DT_SNO]</c> descriptor <c>@64</c> — the bonus
/// stat-threshold tag array, populated only on rare nodes (see
/// <see cref="BonusStatTagSnoIds"/>); <c>bHasSocket@80</c>;
/// <c>bIsGate@84</c>; a <c>DT_VARIABLEARRAY[DT_UINT]</c> descriptor
/// <c>@88</c> — one per-attribute GBID, parallel to <c>ptAttributes</c>
/// (see <see cref="NodeAttribute.AttributeGbid"/>).
/// </remarks>
public sealed class ParagonNodeDefinition
{
    private const int AttrStride = 88;

    /// <summary>Payload offset of the bonus-passive-power slot's
    /// <c>DT_VARIABLEARRAY[DT_SNO]</c> descriptor (size-1 on rare nodes;
    /// empty on all other observed node kinds).</summary>
    private const int BonusPowerArrayDescriptor = 48;

    /// <summary>Payload offset of the bonus stat-threshold tag array's
    /// <c>DT_VARIABLEARRAY[DT_SNO]</c> descriptor (group-124 SNO ids;
    /// non-empty only on rare nodes).</summary>
    private const int BonusStatTagArrayDescriptor = 64;

    /// <summary>Payload offset of the parallel per-attribute GBID array's
    /// <c>DT_VARIABLEARRAY</c> descriptor.</summary>
    private const int AttrGbidArrayDescriptor = 88;

    private readonly NodeAttribute[] _attributes;
    private readonly int[] _bonusStatTagSnoIds;

    private ParagonNodeDefinition(
        int snoId, int nodeType, int rarityOverride, bool hasSocket, bool isGate,
        uint hIcon, uint hIconMask, int snoPassivePower, int bonusPassivePowerSno,
        NodeAttribute[] attributes, int[] bonusStatTagSnoIds)
    {
        SnoId = snoId;
        NodeTypeRaw = nodeType;
        RarityOverride = rarityOverride;
        HasSocket = hasSocket;
        IsGate = isGate;
        HIcon = hIcon;
        HIconMask = hIconMask;
        SnoPassivePower = snoPassivePower;
        BonusPassivePowerSno = bonusPassivePowerSno;
        _attributes = attributes;
        _bonusStatTagSnoIds = bonusStatTagSnoIds;
    }

    /// <summary>The node's own SNO id.</summary>
    public int SnoId { get; }

    /// <summary>Raw <c>eNodeType</c> (payload <c>+16</c>): <b>0</b>=Normal/
    /// structural/gate/rare, <b>3</b>=Magic, <b>5</b>=Start. This exact int is
    /// the serialized contract — kept raw deliberately; see
    /// <see cref="NodeType"/> for the named enum.</summary>
    public int NodeTypeRaw { get; }

    /// <summary><see cref="NodeTypeRaw"/> as the verified enum (convenience;
    /// the raw int remains authoritative). A distinct axis from
    /// <see cref="Rarity"/>.</summary>
    public ParagonNodeType NodeType => (ParagonNodeType)NodeTypeRaw;

    /// <summary>True when this is a board start node
    /// (<see cref="NodeTypeRaw"/> == 5) — verified on all seven class start
    /// boards.</summary>
    public bool IsStart => NodeTypeRaw == (int)ParagonNodeType.Start;

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

    /// <summary>The single SNO slot decoded from the
    /// <c>DT_VARIABLEARRAY[DT_SNO]</c> descriptor at payload <c>+48</c>.
    /// Rare nodes carry the descriptor with one slot; all other observed
    /// kinds (Common/Magic/Start/Gate/Socket) leave the descriptor empty.
    /// Across every rare node sampled so far the slot itself holds <c>0</c>
    /// (no bonus passive power authored), so the population case is
    /// unobserved — the value is surfaced raw rather than left undecoded:
    /// <c>0</c> means "rare-shape descriptor with no power", and
    /// <c>-1</c> means "no descriptor / not a rare node". The canonical
    /// engine field name has not yet been recovered.</summary>
    public int BonusPassivePowerSno { get; }

    /// <summary>The node's attribute grants (raw <see cref="NodeAttribute"/>
    /// specifiers).</summary>
    public IReadOnlyList<NodeAttribute> Attributes => _attributes;

    /// <summary>The bonus stat-threshold tag SNO ids from the
    /// <c>DT_VARIABLEARRAY[DT_SNO]</c> descriptor at payload <c>+64</c>
    /// (group <see cref="SnoGroup.StatTag"/>=124). Populated only on
    /// <see cref="ParagonRarity.Rare"/> nodes — every other observed node
    /// kind (Common/Magic/Start/Gate/Socket) returns an empty list. Each
    /// tag references a <see cref="StatTagDefinition"/> whose formula text
    /// evaluates to the stat threshold the player must meet for the node's
    /// "bonus when threshold met" effect to activate. Class-generic rares
    /// list 2–3 tags (alternative stats keyed to the player's class —
    /// e.g. <c>[Barb_Strength+Dexterity, DexteritySide2, StrengthSide2]</c>);
    /// class-specific rares list one (<c>Warlock_Rare_006</c> →
    /// <c>WillpowerMain2</c>). The canonical engine field name has not yet
    /// been recovered.</summary>
    public IReadOnlyList<int> BonusStatTagSnoIds => _bonusStatTagSnoIds;

    /// <summary>Decode a ParagonNode from its raw SNO blob.</summary>
    public static ParagonNodeDefinition Parse(ReadOnlySpan<byte> blob)
    {
        var r = new SnoRecord(blob);
        var snoId = r.SnoId;
        var hIcon = r.U32(8);
        var hIconMask = r.U32(12);
        var nodeType = r.I32(16);
        var rarity = r.I32(20);
        var snoPassivePower = r.I32(24);
        var hasSocket = r.I32(80) != 0;
        var isGate = r.I32(84) != 0;

        // ptAttributes DT_VARIABLEARRAY @ payload 32:
        //   dataOffset (payload-relative) @ +8, dataSize @ +12.
        var dataOffset = (int)r.U32(32 + 8);
        var dataSize = (int)r.U32(32 + 12);
        var count = dataSize > 0 ? dataSize / AttrStride : 0;

        // Bonus-passive-power slot @ +48 (DT_VARIABLEARRAY[DT_SNO], size 0 or 4).
        // -1 ("no descriptor") when the array is empty; otherwise the raw SNO id
        // (observed 0 across every rare node sampled — no rare populates it yet).
        var bonusPowerBytes = r.VariableArray(BonusPowerArrayDescriptor);
        var bonusPassivePowerSno = bonusPowerBytes.Length >= 4
            ? (int)Bytes.U32LE(bonusPowerBytes, 0)
            : -1;

        // Bonus stat-threshold tag array @ +64 (DT_VARIABLEARRAY[DT_SNO]) —
        // group-124 StatTag SNO ids; populated only on rare nodes.
        var bonusTagBytes = r.VariableArray(BonusStatTagArrayDescriptor);
        var bonusTagCount = bonusTagBytes.Length / 4;
        var bonusTags = bonusTagCount == 0 ? Array.Empty<int>() : new int[bonusTagCount];
        for (var i = 0; i < bonusTagCount; i++)
            bonusTags[i] = (int)Bytes.U32LE(bonusTagBytes, i * 4);

        // Parallel per-attribute GBID array — a DT_VARIABLEARRAY whose
        // descriptor is at payload +88 (one DT_UINT per attribute, same order;
        // see NodeAttribute.AttributeGbid for the documented size discrepancy
        // observed on rare nodes).
        var gbidBytes = r.VariableArray(AttrGbidArrayDescriptor);
        var gbidCount = gbidBytes.Length / 4;

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
                InlineFormula: inline,
                AttributeGbid: i < gbidCount ? Bytes.U32LE(gbidBytes, i * 4) : 0);
        }

        return new ParagonNodeDefinition(
            snoId, nodeType, rarity, hasSocket, isGate, hIcon, hIconMask,
            snoPassivePower, bonusPassivePowerSno, attrs, bonusTags);
    }
}
