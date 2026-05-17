using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>ParagonBoardDefinition</c> (<c>.pbd</c>, SNO
/// group 108): the square node grid for one paragon board. Raw fields
/// only — no scoring, no evaluation (see the library boundary in
/// <c>docs/casc-diablo4-format.md</c> Appendix C).
/// </summary>
/// <remarks>
/// Byte layout per the canonical reference
/// (<c>docs/casc-diablo4-format.md §7.1</c>): payload base <c>0x10</c>;
/// <c>snoId</c> at payload <c>0</c>; <c>nWidth</c> (DT_UINT) at payload
/// <c>12</c>; <c>arEntries</c> (<c>DT_VARIABLEARRAY[DT_SNO]</c>) descriptor
/// at payload <c>16</c>. Cells are <c>dataSize/4</c> little-endian
/// <see cref="uint"/> SNO ids, row-major (<c>index = row*Width + col</c>);
/// <c>0xFFFFFFFF</c> marks an empty cell.
/// </remarks>
public sealed class ParagonBoardDefinition
{
    private readonly int?[] _cells;

    private ParagonBoardDefinition(
        int snoId, int width, int?[] cells,
        int classSnoId, string className, int boardIndex)
    {
        SnoId = snoId;
        Width = width;
        _cells = cells;
        ClassSnoId = classSnoId;
        ClassSnoName = className;
        BoardIndex = boardIndex;
    }

    /// <summary>The board's own SNO id (== the CoreTOC id).</summary>
    public int SnoId { get; }

    /// <summary>
    /// The owning class's <see cref="SnoGroup.PlayerClass"/> SNO id — the
    /// stable per-class key (FR-D1), or <c>0</c> if this instance was decoded
    /// via the byte-only <see cref="Parse(ReadOnlySpan{byte})"/> overload
    /// (which has no <see cref="CoreToc"/> to resolve the name convention).
    /// </summary>
    /// <remarks>
    /// A <c>ParagonBoard</c> record carries <b>no</b> class field; the only
    /// first-party source is the SNO-name convention
    /// <c>Paragon_&lt;ClassToken&gt;_&lt;Index&gt;</c>. Per the durable
    /// opaque-id principle (<c>docs/casc-diablo4-format.md</c> Appendix C)
    /// that convention is decoded <b>once, library-side</b> — the token is
    /// the unique case-sensitive prefix of exactly one
    /// <see cref="SnoGroup.PlayerClass"/> roster SnoName (§6.6, CL-16) — and
    /// exposed typed here; it is never a consumer regex. Populated by
    /// <see cref="Diablo4Storage.ReadParagonBoard(int)"/>.
    /// </remarks>
    public int ClassSnoId { get; }

    /// <summary>The owning class's CoreTOC SnoName (e.g. <c>Warlock</c>) — a
    /// stable key matching <see cref="CharacterClass.SnoName"/>; or
    /// <see cref="string.Empty"/> if decoded byte-only. See
    /// <see cref="ClassSnoId"/> for the convention/boundary.</summary>
    public string ClassSnoName { get; } = string.Empty;

    /// <summary>The per-class board ordinal — the SNO name's trailing
    /// integer (<c>Paragon_Warlock_03</c> → <c>3</c>;
    /// <c>Paragon_Spirit_0</c> → <c>0</c>), or <c>-1</c> if decoded
    /// byte-only. See <see cref="ClassSnoId"/> for the convention/boundary.</summary>
    public int BoardIndex { get; } = -1;

    /// <summary>Grid side length (<c>nWidth</c>; 21 on the current build).</summary>
    public int Width { get; }

    /// <summary>All cells, row-major. A cell is the ParagonNode SNO id, or
    /// <see langword="null"/> for an empty cell (<c>0xFFFFFFFF</c>).</summary>
    public IReadOnlyList<int?> Cells => _cells;

    /// <summary>Number of non-empty cells (placed nodes).</summary>
    public int NodeCount
    {
        get
        {
            var n = 0;
            foreach (var c in _cells) if (c is not null) n++;
            return n;
        }
    }

    /// <summary>The cell at <paramref name="row"/>, <paramref name="col"/>
    /// (the ParagonNode SNO id, or <see langword="null"/> if empty / out of
    /// range).</summary>
    public int? CellAt(int row, int col)
    {
        if ((uint)col >= (uint)Width) return null;
        var i = row * Width + col;
        return (uint)i < (uint)_cells.Length ? _cells[i] : null;
    }

    /// <summary>Decode a ParagonBoard from its raw SNO blob (as returned by
    /// <see cref="Diablo4Storage.ReadSno(SnoGroup,int,SnoFolder,int)"/> for
    /// <see cref="SnoGroup.ParagonBoard"/>). Grid only — the class/index
    /// identity (<see cref="ClassSnoId"/> / <see cref="ClassSnoName"/> /
    /// <see cref="BoardIndex"/>) is left unresolved (<c>0</c>/empty/<c>-1</c>)
    /// because it derives from the SNO <i>name</i>, not the bytes; use
    /// <see cref="Diablo4Storage.ReadParagonBoard(int)"/> to get a fully
    /// resolved definition.</summary>
    public static ParagonBoardDefinition Parse(ReadOnlySpan<byte> blob) =>
        Parse(blob, 0, string.Empty, -1);

    /// <summary>Decode a ParagonBoard, attaching the class/index identity the
    /// caller resolved from the SNO name (FR-D1; see
    /// <see cref="Diablo4Storage.ReadParagonBoard(int)"/>).</summary>
    internal static ParagonBoardDefinition Parse(
        ReadOnlySpan<byte> blob, int classSnoId, string className, int boardIndex)
    {
        var r = new SnoRecord(blob);
        var snoId = r.SnoId;                          // payload + 0
        var width = (int)r.U32(12);                   // nWidth
        var cellBytes = r.VariableArray(16);          // arEntries[]
        var count = cellBytes.Length / 4;
        var cells = new int?[count];
        for (var i = 0; i < count; i++)
        {
            var v = Bytes.U32LE(cellBytes, i * 4);
            cells[i] = v == 0xFFFFFFFF ? null : (int)v;
        }
        return new ParagonBoardDefinition(
            snoId, width, cells, classSnoId, className, boardIndex);
    }
}
