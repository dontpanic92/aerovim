// <copyright file="SetScrollRegionEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// SetScrollRegion Event.
/// </summary>
/// <param name="Top">Top row in the region.</param>
/// <param name="Bottom">Bottom row in the region.</param>
/// <param name="Left">Leftmost col in the region.</param>
/// <param name="Right">Rightmost col in the region.</param>
public record SetScrollRegionEvent(int Top, int Bottom, int Left, int Right) : IRedrawEvent;
