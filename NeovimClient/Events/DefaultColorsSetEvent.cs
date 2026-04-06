// <copyright file="DefaultColorsSetEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>default_colors_set</c> event. Sets the default foreground,
/// background, and special colors for the editor.
/// </summary>
public class DefaultColorsSetEvent : IRedrawEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultColorsSetEvent"/> class.
    /// </summary>
    /// <param name="rgbFg">Default RGB foreground color.</param>
    /// <param name="rgbBg">Default RGB background color.</param>
    /// <param name="rgbSp">Default RGB special color.</param>
    /// <param name="ctermFg">Default cterm foreground color code.</param>
    /// <param name="ctermBg">Default cterm background color code.</param>
    public DefaultColorsSetEvent(int rgbFg, int rgbBg, int rgbSp, int ctermFg, int ctermBg)
    {
        this.RgbFg = rgbFg;
        this.RgbBg = rgbBg;
        this.RgbSp = rgbSp;
        this.CtermFg = ctermFg;
        this.CtermBg = ctermBg;
    }

    /// <summary>
    /// Gets the default RGB foreground color.
    /// </summary>
    public int RgbFg { get; }

    /// <summary>
    /// Gets the default RGB background color.
    /// </summary>
    public int RgbBg { get; }

    /// <summary>
    /// Gets the default RGB special color.
    /// </summary>
    public int RgbSp { get; }

    /// <summary>
    /// Gets the default cterm foreground color code.
    /// </summary>
    public int CtermFg { get; }

    /// <summary>
    /// Gets the default cterm background color code.
    /// </summary>
    public int CtermBg { get; }
}
