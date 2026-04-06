// <copyright file="GridResizeEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>grid_resize</c> event. Resizes (or creates) a grid.
/// </summary>
public class GridResizeEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GridResizeEvent"/> class.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    /// <param name="width">Column count.</param>
    /// <param name="height">Row count.</param>
    public GridResizeEvent(int grid, int width, int height)
    {
        this.Grid = grid;
        this.Width = width;
        this.Height = height;
    }

    /// <summary>
    /// Gets the grid identifier.
    /// </summary>
    public int Grid { get; }

    /// <summary>
    /// Gets the column count.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the row count.
    /// </summary>
    public int Height { get; }
}
