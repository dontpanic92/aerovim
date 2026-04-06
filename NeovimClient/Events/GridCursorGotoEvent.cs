// <copyright file="GridCursorGotoEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>grid_cursor_goto</c> event. Moves the visible cursor to a position
/// on the specified grid.
/// </summary>
public class GridCursorGotoEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GridCursorGotoEvent"/> class.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    /// <param name="row">The row index.</param>
    /// <param name="col">The column index.</param>
    public GridCursorGotoEvent(int grid, int row, int col)
    {
        this.Grid = grid;
        this.Row = row;
        this.Col = col;
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
    /// Gets the column index.
    /// </summary>
    public int Col { get; }
}
