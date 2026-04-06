// <copyright file="HlAttrDefineEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

using AeroVim.Editor.Utilities;

/// <summary>
/// The <c>hl_attr_define</c> event. Adds a highlight entry to the highlight
/// attribute table used by <c>grid_line</c> events.
/// </summary>
public class HlAttrDefineEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HlAttrDefineEvent"/> class.
    /// </summary>
    /// <param name="id">The highlight ID.</param>
    /// <param name="rgbAttrs">The RGB highlight attributes.</param>
    public HlAttrDefineEvent(int id, HighlightAttributes rgbAttrs)
    {
        this.Id = id;
        this.RgbAttrs = rgbAttrs;
    }

    /// <summary>
    /// Gets the highlight ID.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the RGB highlight attributes.
    /// </summary>
    public HighlightAttributes RgbAttrs { get; }
}
