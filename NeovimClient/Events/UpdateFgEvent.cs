// <copyright file="UpdateFgEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// UpdateFg event.
/// </summary>
public class UpdateFgEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateFgEvent"/> class.
    /// </summary>
    /// <param name="color">Foreground color.</param>
    public UpdateFgEvent(int color)
    {
        this.Color = color;
    }

    /// <summary>
    /// Gets the color.
    /// </summary>
    public int Color { get; }
}
