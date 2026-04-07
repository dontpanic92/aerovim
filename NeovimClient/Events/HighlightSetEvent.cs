// <copyright file="HighlightSetEvent.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient.Events;

/// <summary>
/// The HightlightSet event.
/// </summary>
/// <param name="Foreground">Foreground color.</param>
/// <param name="Background">Background color.</param>
/// <param name="Special">Special color.</param>
/// <param name="Reverse">Whether foreground color and background color need to reverse.</param>
/// <param name="Italic">Whether the text is italic.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Underline">Whether Underline is needed.</param>
/// <param name="Undercurl">Whether Undercurl is needed.</param>
public record HighlightSetEvent(
    int? Foreground = null,
    int? Background = null,
    int? Special = null,
    bool Reverse = false,
    bool Italic = false,
    bool Bold = false,
    bool Underline = false,
    bool Undercurl = false) : IRedrawEvent;
