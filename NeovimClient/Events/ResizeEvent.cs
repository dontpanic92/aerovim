// <copyright file="ResizeEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The CursorGoto event.
/// </summary>
public class ResizeEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResizeEvent"/> class.
    /// </summary>
    /// <param name="row">row.</param>
    /// <param name="col">column.</param>
    public ResizeEvent(uint row, uint col)
    {
        this.Row = row;
        this.Col = col;
    }

    /// <summary>
    /// Gets the row.
    /// </summary>
    public uint Row { get; }

    /// <summary>
    /// Gets the col.
    /// </summary>
    public uint Col { get; }
}
