// <copyright file="ModeChangeEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The mode change event.
/// </summary>
public class ModeChangeEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModeChangeEvent"/> class.
    /// </summary>
    /// <param name="modeName">The name of this mode.</param>
    /// <param name="index">The index of this mode.</param>
    public ModeChangeEvent(string modeName, int index)
    {
        this.ModeName = modeName;
        this.Index = index;
    }

    /// <summary>
    /// Gets the name of this mode.
    /// </summary>
    public string ModeName { get; }

    /// <summary>
    /// Gets the index of this mode.
    /// </summary>
    public int Index { get; }
}
