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
/// <param name="Grid">The grid identifier.</param>
/// <param name="Top">Top row of the scroll region (inclusive).</param>
/// <param name="Bot">Bottom row of the scroll region (exclusive).</param>
/// <param name="Left">Left column of the scroll region (inclusive).</param>
/// <param name="Right">Right column of the scroll region (exclusive).</param>
/// <param name="Rows">Number of rows to scroll (positive = up, negative = down).</param>
/// <param name="Cols">Number of columns to scroll (reserved, always 0).</param>
public record GridScrollEvent(int Grid, int Top, int Bot, int Left, int Right, int Rows, int Cols) : IRedrawEvent;
