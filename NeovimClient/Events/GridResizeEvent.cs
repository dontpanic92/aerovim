// <copyright file="GridResizeEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>grid_resize</c> event. Resizes (or creates) a grid.
/// </summary>
/// <param name="Grid">The grid identifier.</param>
/// <param name="Width">Column count.</param>
/// <param name="Height">Row count.</param>
public record GridResizeEvent(int Grid, int Width, int Height) : IRedrawEvent;
