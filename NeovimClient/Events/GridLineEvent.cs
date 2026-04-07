// <copyright file="GridLineEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>grid_line</c> event. Redraws a continuous part of a row on a grid.
/// </summary>
/// <param name="Grid">The grid identifier.</param>
/// <param name="Row">The row index.</param>
/// <param name="ColStart">The starting column.</param>
/// <param name="Cells">The cell data array.</param>
/// <param name="Wrap">Whether this line wraps to the next row.</param>
public record GridLineEvent(int Grid, int Row, int ColStart, GridLineCell[] Cells, bool Wrap) : IRedrawEvent;
