// <copyright file="PutEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The Put event.
/// </summary>
public class PutEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PutEvent"/> class.
    /// </summary>
    /// <param name="text">The text to be rendered.</param>
    public PutEvent(IList<int?> text)
    {
        this.Text = text;
    }

    /// <summary>
    /// Gets the text.
    /// </summary>
    public IList<int?> Text { get; }
}
