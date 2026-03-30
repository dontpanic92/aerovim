// <copyright file="SetScrollRegionEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// SetScrollRegion Event.
/// </summary>
public class SetScrollRegionEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetScrollRegionEvent"/> class.
    /// </summary>
    /// <param name="top">Top row in the region.</param>
    /// <param name="bottom">Bottom row in the region.</param>
    /// <param name="left">Leftmost col in the region.</param>
    /// <param name="right">Rightmost col in the region.</param>
    public SetScrollRegionEvent(int top, int bottom, int left, int right)
    {
        this.Top = top;
        this.Bottom = bottom;
        this.Left = left;
        this.Right = right;
    }

    /// <summary>
    /// Gets the top row of the region.
    /// </summary>
    public int Top { get; }

    /// <summary>
    /// Gets the bottom row of the region.
    /// </summary>
    public int Bottom { get; }

    /// <summary>
    /// Gets the leftmost col of the region.
    /// </summary>
    public int Left { get; }

    /// <summary>
    /// Gets the rightmost col of the region.
    /// </summary>
    public int Right { get; }
}
