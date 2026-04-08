// <copyright file="CellStyle.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor;

/// <summary>
/// Groups the visual attributes of a <see cref="Cell"/>.
/// </summary>
/// <param name="ForegroundColor">Foreground color.</param>
/// <param name="BackgroundColor">Background color.</param>
/// <param name="SpecialColor">Special/underline color.</param>
/// <param name="Reverse">Whether foreground and background are swapped.</param>
/// <param name="Italic">Whether the text is italic.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Underline">Whether the text is underlined.</param>
/// <param name="Undercurl">Whether the text has an undercurl.</param>
public readonly record struct CellStyle(
    int ForegroundColor,
    int BackgroundColor,
    int SpecialColor,
    bool Reverse,
    bool Italic,
    bool Bold,
    bool Underline,
    bool Undercurl);
