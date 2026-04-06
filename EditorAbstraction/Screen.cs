// <copyright file="Screen.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Editor;

/// <summary>
/// Represents the editor screen state for rendering.
/// </summary>
public sealed class Screen
{
    /// <summary>
    /// Gets or sets the cell grid.
    /// </summary>
    public required Cell[,] Cells { get; set; }

    /// <summary>
    /// Gets or sets the cursor position.
    /// </summary>
    public (int Row, int Col) CursorPosition { get; set; }

    /// <summary>
    /// Gets or sets the foreground color.
    /// </summary>
    public int ForegroundColor { get; set; }

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    public int BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every row is dirty and the
    /// entire grid must be repainted. When true, <see cref="DirtyRows"/>
    /// should be ignored.
    /// </summary>
    public bool AllDirty { get; set; }

    /// <summary>
    /// Gets or sets per-row dirty flags. Each element indicates whether the
    /// corresponding row has changed since the last render. The array length
    /// matches the row count of <see cref="Cells"/>. May be null when dirty
    /// tracking is not available, in which case the renderer should treat
    /// every row as dirty.
    /// </summary>
    public bool[]? DirtyRows { get; set; }

    /// <summary>
    /// Gets or sets the popup completion menu items, or <c>null</c> if the
    /// popup menu is not visible.
    /// </summary>
    public PopupMenuItem[]? PopupItems { get; set; }

    /// <summary>
    /// Gets or sets the currently selected popup menu item index, or -1 if
    /// no item is selected.
    /// </summary>
    public int PopupSelected { get; set; } = -1;

    /// <summary>
    /// Gets or sets the popup menu anchor position (row, col) in the grid,
    /// or <c>null</c> if the popup menu is not visible.
    /// </summary>
    public (int Row, int Col)? PopupAnchor { get; set; }

    /// <summary>
    /// Gets or sets the externalized command line state, or <c>null</c> if
    /// the command line is not currently active.
    /// </summary>
    public CmdlineState? Cmdline { get; set; }
}
