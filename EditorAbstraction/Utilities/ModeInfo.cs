// <copyright file="ModeInfo.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor.Utilities;

/// <summary>
/// Editor mode info for cursor and pointer display.
/// </summary>
public class ModeInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModeInfo"/> class.
    /// </summary>
    /// <param name="cursorShape">Cursor shape info.</param>
    /// <param name="cellPercentage">Cursor size info.</param>
    /// <param name="cursorBlinking">Cursor blinking info.</param>
    /// <param name="pointerShape">Pointer shape name, if specified by the editor.</param>
    /// <param name="cursorVisible">A value indicating whether the text cursor should be shown.</param>
    /// <param name="cursorStyleEnabled">A value indicating whether mode-specific cursor styling should be applied.</param>
    /// <param name="pointerMode">Pointer auto-hide mode using terminal-style semantics.</param>
    public ModeInfo(
        CursorShape cursorShape,
        int cellPercentage,
        CursorBlinking cursorBlinking,
        string? pointerShape = null,
        bool cursorVisible = true,
        bool cursorStyleEnabled = true,
        int pointerMode = 0)
    {
        this.CursorShape = cursorShape;
        this.CellPercentage = cellPercentage;
        this.CursorBlinking = cursorBlinking;
        this.PointerShape = pointerShape;
        this.CursorVisible = cursorVisible;
        this.CursorStyleEnabled = cursorStyleEnabled;
        this.PointerMode = pointerMode;
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
    /// Gets the pointer shape name, if specified by the editor.
    /// </summary>
    public string? PointerShape { get; }

    /// <summary>
    /// Gets the pointer shape name, using Neovim's original field name.
    /// </summary>
    public string? MouseShape => this.PointerShape;

    /// <summary>
    /// Gets a value indicating whether the cursor should be visible.
    /// </summary>
    public bool CursorVisible { get; }

    /// <summary>
    /// Gets a value indicating whether mode-specific cursor styling should be honored.
    /// </summary>
    public bool CursorStyleEnabled { get; }

    /// <summary>
    /// Gets the pointer auto-hide mode using terminal-style semantics.
    /// 0 = never hide, 1 = hide when tracking is disabled, 2 = always hide, 3 = always hide even on leave.
    /// </summary>
    public int PointerMode { get; }
}
