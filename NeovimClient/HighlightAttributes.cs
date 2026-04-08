// <copyright file="HighlightAttributes.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient;

/// <summary>
/// Highlight attributes defined by the <c>hl_attr_define</c> event in the
/// <c>ext_linegrid</c> UI protocol. Each instance maps to a numeric highlight
/// ID that <c>grid_line</c> events reference.
/// </summary>
public sealed class HighlightAttributes
{
    /// <summary>
    /// Gets or sets the foreground color, or <c>null</c> to use the default.
    /// </summary>
    public int? Foreground { get; set; }

    /// <summary>
    /// Gets or sets the background color, or <c>null</c> to use the default.
    /// </summary>
    public int? Background { get; set; }

    /// <summary>
    /// Gets or sets the special color (used for underlines/undercurls), or
    /// <c>null</c> to use the default.
    /// </summary>
    public int? Special { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether foreground and background
    /// colors should be swapped.
    /// </summary>
    public bool Reverse { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text is italic.
    /// </summary>
    public bool Italic { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text is bold.
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text is underlined.
    /// </summary>
    public bool Underline { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text has an undercurl.
    /// </summary>
    public bool Undercurl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the text has strikethrough.
    /// </summary>
    public bool Strikethrough { get; set; }
}
