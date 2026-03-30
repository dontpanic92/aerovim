// <copyright file="SetIconTitleEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The SetTitle event.
/// </summary>
public class SetIconTitleEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SetIconTitleEvent"/> class.
    /// </summary>
    /// <param name="title">The title.</param>
    public SetIconTitleEvent(string title)
    {
        this.Title = title;
    }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }
}
