// <copyright file="DefaultColorsSetEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The <c>default_colors_set</c> event. Sets the default foreground,
/// background, and special colors for the editor.
/// </summary>
/// <param name="RgbFg">Default RGB foreground color.</param>
/// <param name="RgbBg">Default RGB background color.</param>
/// <param name="RgbSp">Default RGB special color.</param>
/// <param name="CtermFg">Default cterm foreground color code.</param>
/// <param name="CtermBg">Default cterm background color code.</param>
public record DefaultColorsSetEvent(int RgbFg, int RgbBg, int RgbSp, int CtermFg, int CtermBg) : IRedrawEvent;
