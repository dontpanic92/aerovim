// <copyright file="GridCursorGotoEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>grid_cursor_goto</c> event. Moves the visible cursor to a position
/// on the specified grid.
/// </summary>
/// <param name="Grid">The grid identifier.</param>
/// <param name="Row">The row index.</param>
/// <param name="Col">The column index.</param>
public record GridCursorGotoEvent(int Grid, int Row, int Col) : IRedrawEvent;
