// <copyright file="GridLineEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>grid_line</c> event. Redraws a continuous part of a row on a grid.
/// </summary>
public class GridLineEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GridLineEvent"/> class.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    /// <param name="row">The row index.</param>
    /// <param name="colStart">The starting column.</param>
    /// <param name="cells">The cell data array.</param>
    /// <param name="wrap">Whether this line wraps to the next row.</param>
    public GridLineEvent(int grid, int row, int colStart, GridLineCell[] cells, bool wrap)
    {
        this.Grid = grid;
        this.Row = row;
        this.ColStart = colStart;
        this.Cells = cells;
        this.Wrap = wrap;
    }

    /// <summary>
    /// Gets the grid identifier.
    /// </summary>
    public int Grid { get; }

    /// <summary>
    /// Gets the row index.
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// Gets the starting column.
    /// </summary>
    public int ColStart { get; }

    /// <summary>
    /// Gets the cell data array.
    /// </summary>
    public GridLineCell[] Cells { get; }

    /// <summary>
    /// Gets a value indicating whether this line wraps to the next row.
    /// </summary>
    public bool Wrap { get; }
}
