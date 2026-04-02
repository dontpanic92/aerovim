// <copyright file="ModeInfo.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Utilities;

/// <summary>
/// Editor mode info for cursor display.
/// </summary>
public class ModeInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModeInfo"/> class.
    /// </summary>
    /// <param name="cursorShape">Cursor shape info.</param>
    /// <param name="cellPercentage">Cursor size info.</param>
    /// <param name="cursorBlinking">Cursor blinking info.</param>
    /// <param name="mouseShape">Mouse pointer shape name (Neovim: reserved for future use).</param>
    public ModeInfo(CursorShape cursorShape, int cellPercentage, CursorBlinking cursorBlinking, string? mouseShape = null)
    {
        this.CursorShape = cursorShape;
        this.CellPercentage = cellPercentage;
        this.CursorBlinking = cursorBlinking;
        this.MouseShape = mouseShape;
    }

    /// <summary>
    /// Gets the cursor shape.
    /// </summary>
    public CursorShape CursorShape { get; }

    /// <summary>
    /// Gets the percentage of the cursor should occupy.
    /// </summary>
    public int CellPercentage { get; }

    /// <summary>
    /// Gets the blinking setting for the cursor.
    /// </summary>
    public CursorBlinking CursorBlinking { get; }

    /// <summary>
    /// Gets the mouse pointer shape name, if specified by the editor.
    /// This is reserved for future use in Neovim's UI protocol.
    /// </summary>
    public string? MouseShape { get; }
}
