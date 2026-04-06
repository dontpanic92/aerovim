// <copyright file="GridScrollEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>grid_scroll</c> event. Scrolls a region of the specified grid.
/// Unlike the legacy <c>set_scroll_region</c>+<c>scroll</c>, ranges are
/// end-exclusive.
/// </summary>
public class GridScrollEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GridScrollEvent"/> class.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    /// <param name="top">Top row of the scroll region (inclusive).</param>
    /// <param name="bot">Bottom row of the scroll region (exclusive).</param>
    /// <param name="left">Left column of the scroll region (inclusive).</param>
    /// <param name="right">Right column of the scroll region (exclusive).</param>
    /// <param name="rows">Number of rows to scroll (positive = up, negative = down).</param>
    /// <param name="cols">Number of columns to scroll (reserved, always 0).</param>
    public GridScrollEvent(int grid, int top, int bot, int left, int right, int rows, int cols)
    {
        this.Grid = grid;
        this.Top = top;
        this.Bot = bot;
        this.Left = left;
        this.Right = right;
        this.Rows = rows;
        this.Cols = cols;
    }

    /// <summary>
    /// Gets the grid identifier.
    /// </summary>
    public int Grid { get; }

    /// <summary>
    /// Gets the top row (inclusive).
    /// </summary>
    public int Top { get; }

    /// <summary>
    /// Gets the bottom row (exclusive).
    /// </summary>
    public int Bot { get; }

    /// <summary>
    /// Gets the left column (inclusive).
    /// </summary>
    public int Left { get; }

    /// <summary>
    /// Gets the right column (exclusive).
    /// </summary>
    public int Right { get; }

    /// <summary>
    /// Gets the number of rows to scroll.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Gets the number of columns to scroll (reserved, always 0).
    /// </summary>
    public int Cols { get; }
}
