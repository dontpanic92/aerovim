// <copyright file="GridClearEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>grid_clear</c> event. Clears the specified grid.
/// </summary>
/// <param name="Grid">The grid identifier.</param>
public record GridClearEvent(int Grid) : IRedrawEvent;
