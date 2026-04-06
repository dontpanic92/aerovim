// <copyright file="GridLineCell.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// A single cell entry in a <c>grid_line</c> event. Represents one or more
/// consecutive cells that share the same text and highlight.
/// </summary>
public readonly struct GridLineCell
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GridLineCell"/> struct.
    /// </summary>
    /// <param name="text">The UTF-8 text for this cell.</param>
    /// <param name="hlId">
    /// The highlight attribute ID, or <c>null</c> to reuse the most recently
    /// seen highlight ID in the same <c>grid_line</c> event.
    /// </param>
    /// <param name="repeat">
    /// Number of times this cell should be repeated (including the first).
    /// Defaults to 1 when absent in the protocol.
    /// </param>
    public GridLineCell(string text, int? hlId, int repeat)
    {
        this.Text = text;
        this.HlId = hlId;
        this.Repeat = repeat;
    }

    /// <summary>
    /// Gets the UTF-8 text for this cell.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the highlight attribute ID, or <c>null</c> to reuse the previous one.
    /// </summary>
    public int? HlId { get; }

    /// <summary>
    /// Gets the number of times this cell is repeated.
    /// </summary>
    public int Repeat { get; }
}
