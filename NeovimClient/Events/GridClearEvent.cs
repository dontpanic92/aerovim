// <copyright file="GridClearEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>grid_clear</c> event. Clears the specified grid.
/// </summary>
public class GridClearEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GridClearEvent"/> class.
    /// </summary>
    /// <param name="grid">The grid identifier.</param>
    public GridClearEvent(int grid)
    {
        this.Grid = grid;
    }

    /// <summary>
    /// Gets the grid identifier.
    /// </summary>
    public int Grid { get; }
}
