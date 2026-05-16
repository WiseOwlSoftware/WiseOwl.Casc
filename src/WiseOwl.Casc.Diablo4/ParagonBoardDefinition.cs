using System;
using System.Collections.Generic;
using WiseOwl.Casc.Internal;

namespace WiseOwl.Casc.Diablo4;

/// <summary>
/// A decoded Diablo IV <c>ParagonBoardDefinition</c> (<c>.pbd</c>, SNO
/// group 108): the square node grid for one paragon board. Raw fields
/// only — no scoring, no evaluation (see the library boundary in
/// <c>docs/casc-format.md §10</c>).
/// </summary>
/// <remarks>
/// Byte layout is the authoritative upstream record
/// (<c>e:\Paragon\docs\d4-binary-formats.md §5</c>,
/// <c>ParagonBoardDefinition — VERIFIED</c>): payload base <c>0x10</c>;
/// <c>snoId</c> at payload <c>0</c>; <c>nWidth</c> (DT_UINT) at payload
/// <c>12</c>; <c>arEntries</c> (<c>DT_VARIABLEARRAY[DT_SNO]</c>) descriptor
/// at payload <c>16</c>. Cells are <c>dataSize/4</c> little-endian
/// <see cref="uint"/> SNO ids, row-major (<c>index = row*Width + col</c>);
/// <c>0xFFFFFFFF</c> marks an empty cell.
/// </remarks>
public sealed class ParagonBoardDefinition
{
    private readonly int?[] _cells;

    private ParagonBoardDefinition(int snoId, int width, int?[] cells)
    {
        SnoId = snoId;
        Width = width;
        _cells = cells;
    }

    /// <summary>The board's own SNO id (== the CoreTOC id).</summary>
    public int SnoId { get; }

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
    /// <see cref="SnoGroup.ParagonBoard"/>).</summary>
    public static ParagonBoardDefinition Parse(ReadOnlySpan<byte> blob)
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
        return new ParagonBoardDefinition(snoId, width, cells);
    }
}
